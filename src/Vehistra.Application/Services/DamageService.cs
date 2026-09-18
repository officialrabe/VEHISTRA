using Vehistra.Application.Abstractions;
using Vehistra.Application.Common;
using Vehistra.Application.Dtos;
using Vehistra.Domain.Entities;
using Vehistra.Domain.Enums;
using Vehistra.Domain.Exceptions;
using Vehistra.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace Vehistra.Application.Services;

/// <inheritdoc />
public sealed class DamageService : IDamageService
{
    private readonly IVehistraDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly NumberGenerator _numbers;
    private readonly INotificationService _notifications;
    private readonly IClock _clock;

    public DamageService(
        IVehistraDbContext db,
        ICurrentUserService currentUser,
        NumberGenerator numbers,
        INotificationService notifications,
        IClock clock)
    {
        _db = db;
        _currentUser = currentUser;
        _numbers = numbers;
        _notifications = notifications;
        _clock = clock;
    }

    public async Task<IReadOnlyList<DamageListItem>> GetListAsync(
        DamageFilter filter,
        CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.DamageView);

        var query = _db.DamageReports.AsNoTracking().AsQueryable();

        if (filter.VehicleId is { } vehicleId)
        {
            query = query.Where(d => d.VehicleId == vehicleId);
        }

        if (filter.DriverId is { } driverId)
        {
            query = query.Where(d => d.DriverId == driverId);
        }

        if (filter.Status is { } status)
        {
            query = query.Where(d => d.Status == status);
        }
        else if (filter.OnlyOpen)
        {
            query = query.Where(d => d.Status != DamageStatus.Geschlossen);
        }

        if (filter.Priority is { } priority)
        {
            query = query.Where(d => d.Priority == priority);
        }

        if (filter.CategoryId is { } categoryId)
        {
            query = query.Where(d => d.DamageCategoryId == categoryId);
        }

        if (filter.OnlyNotDriveable)
        {
            query = query.Where(d => !d.IsDriveable);
        }

        if (filter.OnlyInsuranceCases)
        {
            query = query.Where(d => d.IsInsuranceCase);
        }

        if (filter.From is { } from)
        {
            query = query.Where(d => d.OccurredAt >= from);
        }

        if (filter.To is { } to)
        {
            query = query.Where(d => d.OccurredAt <= to);
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchText))
        {
            var text = filter.SearchText.Trim();
            query = query.Where(d =>
                d.DamageNumber.Contains(text) ||
                d.Description.Contains(text) ||
                (d.Vehicle != null && d.Vehicle.LicensePlate != null && d.Vehicle.LicensePlate.Contains(text)) ||
                (d.Vehicle != null && d.Vehicle.InternalNumber.Contains(text)));
        }

        return await query
            .OrderByDescending(d => d.OccurredAt)
            .Select(d => new DamageListItem(
                d.Id,
                d.DamageNumber,
                d.VehicleId,
                d.Vehicle != null
                    ? (d.Vehicle.LicensePlate ?? d.Vehicle.InternalNumber) + " - " + d.Vehicle.Manufacturer + " " + d.Vehicle.Model
                    : string.Empty,
                d.Driver != null ? d.Driver.FirstName + " " + d.Driver.LastName : null,
                d.OccurredAt,
                d.Description,
                d.Category != null ? d.Category.Name : null,
                d.Priority,
                d.Status,
                d.IsDriveable,
                d.CostActual,
                d.IsInsuranceCase))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<DamageReport?> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.DamageView);

        return await _db.DamageReports
            .Include(d => d.Vehicle)
            .Include(d => d.Driver)
            .Include(d => d.Category)
            .Include(d => d.Workshop)
            .Include(d => d.Attachments)
                .ThenInclude(a => a.Document)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> CreateAsync(DamageReport damage, CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.DamageCreate);

        damage.Description = Guard.NotEmpty(damage.Description, "Schadensbeschreibung");

        var vehicle = await _db.Vehicles
            .FirstOrDefaultAsync(v => v.Id == damage.VehicleId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EntityNotFoundException(nameof(Vehicle), damage.VehicleId);

        if (string.IsNullOrWhiteSpace(damage.DamageNumber))
        {
            damage.DamageNumber = await _numbers.NextDamageNumberAsync(cancellationToken).ConfigureAwait(false);
        }

        // Fahrer aus der zum Schadenszeitpunkt gueltigen Zuweisung ableiten.
        if (damage.DriverId is null)
        {
            damage.DriverId = await _db.VehicleDriverAssignments
                .Where(a => a.VehicleId == damage.VehicleId
                            && a.ValidFrom <= damage.OccurredAt
                            && (a.ValidTo == null || a.ValidTo >= damage.OccurredAt))
                .OrderByDescending(a => a.ValidFrom)
                .Select(a => (int?)a.DriverId)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        _db.DamageReports.Add(damage);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (damage.Priority == DamagePriority.Kritisch || !damage.IsDriveable)
        {
            await _notifications.CreateAsync(new Notification
            {
                Category = NotificationCategory.Schaden,
                Severity = NotificationSeverity.Kritisch,
                Title = damage.IsDriveable ? "Kritischer Schaden gemeldet" : "Fahrzeug nicht fahrbereit",
                Message = $"{vehicle.DisplayName}: {damage.Description}",
                VehicleId = vehicle.Id,
                SourceReference = $"DamageReport:{damage.Id}",
                DeduplicationKey = $"DamageCritical:{damage.Id}"
            }, cancellationToken).ConfigureAwait(false);
        }

        return damage.Id;
    }

    public async Task UpdateAsync(DamageReport damage, CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.DamageEdit);

        var existing = await _db.DamageReports
            .FirstOrDefaultAsync(d => d.Id == damage.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EntityNotFoundException(nameof(DamageReport), damage.Id);

        existing.DriverId = damage.DriverId;
        existing.OccurredAt = damage.OccurredAt;
        existing.Mileage = damage.Mileage;
        existing.Description = Guard.NotEmpty(damage.Description, "Schadensbeschreibung");
        existing.Area = damage.Area;
        existing.DamageCategoryId = damage.DamageCategoryId;
        existing.Priority = damage.Priority;
        existing.IsDriveable = damage.IsDriveable;
        existing.RepairRequired = damage.RepairRequired;
        existing.Status = damage.Status;
        existing.WorkshopId = damage.WorkshopId;
        existing.WorkshopOrderId = damage.WorkshopOrderId;
        existing.RepairedAt = damage.RepairedAt;
        existing.CostEstimate = damage.CostEstimate;
        existing.CostActual = damage.CostActual;
        existing.IsInsuranceCase = damage.IsInsuranceCase;
        existing.InsuranceClaimNumber = damage.InsuranceClaimNumber;
        existing.AccidentReportId = damage.AccidentReportId;
        existing.Comment = damage.Comment;
        existing.RowVersion = damage.RowVersion;

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task ChangeStatusAsync(
        int damageId,
        DamageStatus status,
        string? comment,
        CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(status == DamageStatus.Geschlossen
            ? Permissions.DamageClose
            : Permissions.DamageEdit);

        var damage = await _db.DamageReports
            .FirstOrDefaultAsync(d => d.Id == damageId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EntityNotFoundException(nameof(DamageReport), damageId);

        damage.Status = status;

        if (status == DamageStatus.Repariert && damage.RepairedAt is null)
        {
            damage.RepairedAt = _clock.Now;
        }

        if (status == DamageStatus.Geschlossen)
        {
            damage.ClosedAt = _clock.Now;
            damage.ClosedByUserId = _currentUser.User?.Id;
        }

        if (!string.IsNullOrWhiteSpace(comment))
        {
            damage.Comment = string.IsNullOrWhiteSpace(damage.Comment)
                ? comment
                : $"{damage.Comment}{Environment.NewLine}{comment}";
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task CloseAsync(
        int damageId,
        DateTime closedAt,
        decimal? actualCost,
        string? comment,
        CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.DamageClose);

        var damage = await _db.DamageReports
            .FirstOrDefaultAsync(d => d.Id == damageId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EntityNotFoundException(nameof(DamageReport), damageId);

        Guard.That(damage.Status != DamageStatus.Geschlossen, "Dieser Schaden ist bereits geschlossen.");

        damage.Status = DamageStatus.Geschlossen;
        damage.ClosedAt = closedAt;
        damage.ClosedByUserId = _currentUser.User?.Id;
        damage.CostActual = actualCost ?? damage.CostActual;
        damage.RepairedAt ??= closedAt;

        if (!string.IsNullOrWhiteSpace(comment))
        {
            damage.Comment = string.IsNullOrWhiteSpace(damage.Comment)
                ? comment
                : $"{damage.Comment}{Environment.NewLine}{comment}";
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<DamageCategory>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        return await _db.DamageCategories
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<DamageReport>> GetOpenForVehicleAsync(
        int vehicleId,
        CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.DamageView);

        return await _db.DamageReports
            .AsNoTracking()
            .Include(d => d.Category)
            .Where(d => d.VehicleId == vehicleId && d.Status != DamageStatus.Geschlossen)
            .OrderByDescending(d => d.Priority)
            .ThenBy(d => d.OccurredAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
