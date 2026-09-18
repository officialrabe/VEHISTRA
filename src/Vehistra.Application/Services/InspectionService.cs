using Vehistra.Application.Abstractions;
using Vehistra.Application.Common;
using Vehistra.Application.Dtos;
using Vehistra.Domain.Common;
using Vehistra.Domain.Entities;
using Vehistra.Domain.Enums;
using Vehistra.Domain.Exceptions;
using Vehistra.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace Vehistra.Application.Services;

/// <inheritdoc />
public sealed class InspectionService : IInspectionService
{
    private readonly IVehistraDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ISettingsService _settings;
    private readonly IMileageService _mileage;
    private readonly IClock _clock;

    public InspectionService(
        IVehistraDbContext db,
        ICurrentUserService currentUser,
        ISettingsService settings,
        IMileageService mileage,
        IClock clock)
    {
        _db = db;
        _currentUser = currentUser;
        _settings = settings;
        _mileage = mileage;
        _clock = clock;
    }

    public async Task<IReadOnlyList<InspectionListItem>> GetOverviewAsync(
        int? withinDays = null,
        bool includeRetired = false,
        CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.InspectionView);

        var thresholds = await _settings.GetInspectionThresholdsAsync(cancellationToken).ConfigureAwait(false);
        var today = _clock.Today;

        var query = _db.Vehicles.AsNoTracking().AsQueryable();

        if (!includeRetired)
        {
            query = query.Where(v => !v.IsRetired);
        }

        if (withinDays is { } days)
        {
            var limit = today.AddDays(days);
            query = query.Where(v => v.NextInspectionDue != null && v.NextInspectionDue <= limit);
        }

        var rows = await query
            .Select(v => new
            {
                v.Id,
                v.InternalNumber,
                v.LicensePlate,
                v.Manufacturer,
                v.Model,
                v.NextInspectionDue,
                Last = v.Inspections
                    .OrderByDescending(i => i.InspectionDate)
                    .Select(i => new { i.InspectionDate, i.TestCenter, i.Result })
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows
            .Select(r => new InspectionListItem(
                r.Id,
                $"{(string.IsNullOrWhiteSpace(r.LicensePlate) ? r.InternalNumber : r.LicensePlate)} - {r.Manufacturer} {r.Model}".Trim(),
                r.LicensePlate,
                r.Last?.InspectionDate,
                r.NextInspectionDue,
                DueDateCalculator.DaysUntil(r.NextInspectionDue, today),
                DueDateCalculator.Evaluate(r.NextInspectionDue, today, thresholds),
                r.Last?.TestCenter,
                r.Last?.Result))
            .OrderBy(i => i.NextDue ?? DateTime.MaxValue)
            .ToList();
    }

    public async Task<IReadOnlyList<VehicleInspection>> GetForVehicleAsync(
        int vehicleId,
        CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.InspectionView);

        return await _db.VehicleInspections
            .AsNoTracking()
            .Include(i => i.Document)
            .Where(i => i.VehicleId == vehicleId)
            .OrderByDescending(i => i.InspectionDate)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> AddAsync(VehicleInspection inspection, CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.InspectionManage);

        var vehicle = await _db.Vehicles
            .FirstOrDefaultAsync(v => v.Id == inspection.VehicleId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EntityNotFoundException(nameof(Vehicle), inspection.VehicleId);

        Guard.That(inspection.NextDueDate > inspection.InspectionDate.AddDays(-1),
            "Die naechste Faelligkeit muss nach dem Pruefdatum liegen.");

        _db.VehicleInspections.Add(inspection);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await UpdateVehicleDueDateAsync(vehicle, cancellationToken).ConfigureAwait(false);

        if (inspection.Mileage is { } mileage && mileage > 0)
        {
            await _mileage.AddAsync(
                inspection.VehicleId, mileage, inspection.InspectionDate,
                MileageSource.Hauptuntersuchung, "Erfassung bei Hauptuntersuchung",
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        return inspection.Id;
    }

    public async Task UpdateAsync(VehicleInspection inspection, CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.InspectionManage);

        var existing = await _db.VehicleInspections
            .FirstOrDefaultAsync(i => i.Id == inspection.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EntityNotFoundException(nameof(VehicleInspection), inspection.Id);

        existing.Type = inspection.Type;
        existing.InspectionDate = inspection.InspectionDate;
        existing.NextDueDate = inspection.NextDueDate;
        existing.NextEmissionDueDate = inspection.NextEmissionDueDate;
        existing.Result = inspection.Result;
        existing.TestCenter = inspection.TestCenter;
        existing.Inspector = inspection.Inspector;
        existing.Mileage = inspection.Mileage;
        existing.Cost = inspection.Cost;
        existing.Defects = inspection.Defects;
        existing.Comment = inspection.Comment;
        existing.DocumentId = inspection.DocumentId;
        existing.RowVersion = inspection.RowVersion;

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var vehicle = await _db.Vehicles
            .FirstOrDefaultAsync(v => v.Id == existing.VehicleId, cancellationToken)
            .ConfigureAwait(false);

        if (vehicle is not null)
        {
            await UpdateVehicleDueDateAsync(vehicle, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task DeleteAsync(int inspectionId, CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.InspectionManage);

        var inspection = await _db.VehicleInspections
            .FirstOrDefaultAsync(i => i.Id == inspectionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EntityNotFoundException(nameof(VehicleInspection), inspectionId);

        var vehicleId = inspection.VehicleId;
        _db.VehicleInspections.Remove(inspection);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var vehicle = await _db.Vehicles.FirstOrDefaultAsync(v => v.Id == vehicleId, cancellationToken).ConfigureAwait(false);
        if (vehicle is not null)
        {
            await UpdateVehicleDueDateAsync(vehicle, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Schreibt die naechste Faelligkeit denormalisiert an das Fahrzeug (fuer schnelle Listen).</summary>
    private async Task UpdateVehicleDueDateAsync(Vehicle vehicle, CancellationToken cancellationToken)
    {
        var next = await _db.VehicleInspections
            .Where(i => i.VehicleId == vehicle.Id
                        && (i.Type == InspectionType.Hauptuntersuchung
                            || i.Type == InspectionType.HauptUndAbgasuntersuchung))
            .OrderByDescending(i => i.InspectionDate)
            .Select(i => (DateTime?)i.NextDueDate)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        vehicle.NextInspectionDue = next;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
