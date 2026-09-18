using Vehistra.Application.Abstractions;
using Vehistra.Application.Common;
using Vehistra.Domain.Entities;
using Vehistra.Domain.Enums;
using Vehistra.Domain.Exceptions;
using Vehistra.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace Vehistra.Application.Services;

/// <inheritdoc />
public sealed class VehicleLifecycleService : IVehicleLifecycleService
{
    private readonly IVehistraDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ILicensePlateService _plates;
    private readonly IClock _clock;

    public VehicleLifecycleService(
        IVehistraDbContext db,
        ICurrentUserService currentUser,
        ILicensePlateService plates,
        IClock clock)
    {
        _db = db;
        _currentUser = currentUser;
        _plates = plates;
        _clock = clock;
    }

    public async Task<IReadOnlyList<VehicleRegistration>> GetRegistrationsAsync(
        int vehicleId,
        CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.VehicleView);

        return await _db.VehicleRegistrations
            .AsNoTracking()
            .Where(r => r.VehicleId == vehicleId)
            .OrderByDescending(r => r.RegisteredAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> RegisterAsync(
        int vehicleId,
        DateTime registeredAt,
        int? licensePlateId,
        string? registrationOffice,
        string? comment,
        CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.VehicleRegistration);

        var vehicle = await _db.Vehicles
            .FirstOrDefaultAsync(v => v.Id == vehicleId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EntityNotFoundException(nameof(Vehicle), vehicleId);

        Guard.That(!vehicle.IsRetired, "Ein ausgemustertes Fahrzeug kann nicht angemeldet werden.");

        var hadPreviousRegistration = await _db.VehicleRegistrations
            .AnyAsync(r => r.VehicleId == vehicleId, cancellationToken)
            .ConfigureAwait(false);

        if (licensePlateId is { } plateId)
        {
            await _plates.AssignToVehicleAsync(plateId, vehicleId, registeredAt, "Anmeldung", cancellationToken)
                .ConfigureAwait(false);
        }

        var registration = new VehicleRegistration
        {
            VehicleId = vehicleId,
            EventType = hadPreviousRegistration ? RegistrationEventType.Wiederanmeldung : RegistrationEventType.Anmeldung,
            RegisteredAt = registeredAt,
            LicensePlate = vehicle.LicensePlate,
            RegistrationOffice = registrationOffice,
            Comment = comment
        };

        _db.VehicleRegistrations.Add(registration);

        vehicle.IsRegistered = true;
        vehicle.FirstRegistration ??= registeredAt;

        await MoveToStatusAsync(vehicle, VehicleStatusKind.Aktiv, "Anmeldung", comment, cancellationToken)
            .ConfigureAwait(false);

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return registration.Id;
    }

    public async Task DeregisterAsync(
        int vehicleId,
        DateTime deregisteredAt,
        string? reason,
        bool releaseLicensePlate,
        string? comment,
        CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.VehicleRegistration);

        var vehicle = await _db.Vehicles
            .FirstOrDefaultAsync(v => v.Id == vehicleId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EntityNotFoundException(nameof(Vehicle), vehicleId);

        var open = await _db.VehicleRegistrations
            .Where(r => r.VehicleId == vehicleId && r.DeregisteredAt == null)
            .OrderByDescending(r => r.RegisteredAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (open is not null)
        {
            open.DeregisteredAt = deregisteredAt;
            open.Reason = reason ?? open.Reason;
        }
        else
        {
            _db.VehicleRegistrations.Add(new VehicleRegistration
            {
                VehicleId = vehicleId,
                EventType = RegistrationEventType.Abmeldung,
                RegisteredAt = vehicle.FirstRegistration ?? deregisteredAt,
                DeregisteredAt = deregisteredAt,
                LicensePlate = vehicle.LicensePlate,
                Reason = reason,
                Comment = comment
            });
        }

        if (releaseLicensePlate)
        {
            var plate = await _db.LicensePlates
                .FirstOrDefaultAsync(p => p.CurrentVehicleId == vehicleId, cancellationToken)
                .ConfigureAwait(false);

            if (plate is not null)
            {
                await _plates.ReleaseFromVehicleAsync(
                    plate.Id, deregisteredAt, reason ?? "Abmeldung",
                    LicensePlateStatus.Verfuegbar, cancellationToken).ConfigureAwait(false);
            }
        }

        vehicle.IsRegistered = false;
        await MoveToStatusAsync(vehicle, VehicleStatusKind.Abgemeldet, reason ?? "Abmeldung", comment, cancellationToken)
            .ConfigureAwait(false);

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<VehicleRetirement?> GetRetirementAsync(
        int vehicleId,
        CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.VehicleView);

        return await _db.VehicleRetirements
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.VehicleId == vehicleId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> RetireAsync(VehicleRetirement retirement, CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.VehicleRetire);

        var vehicle = await _db.Vehicles
            .FirstOrDefaultAsync(v => v.Id == retirement.VehicleId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EntityNotFoundException(nameof(Vehicle), retirement.VehicleId);

        var existing = await _db.VehicleRetirements
            .AnyAsync(r => r.VehicleId == retirement.VehicleId, cancellationToken)
            .ConfigureAwait(false);
        Guard.That(!existing, "Dieses Fahrzeug ist bereits ausgemustert.");

        var openOrders = await _db.WorkshopOrders
            .CountAsync(w => w.VehicleId == retirement.VehicleId
                             && w.Status != WorkshopOrderStatus.Abgeholt
                             && w.Status != WorkshopOrderStatus.Storniert, cancellationToken)
            .ConfigureAwait(false);
        Guard.That(openOrders == 0,
            "Zu diesem Fahrzeug existieren noch offene Werkstattvorgaenge. Bitte diese zuerst abschliessen.");

        retirement.LastMileage ??= vehicle.CurrentMileage;

        _db.VehicleRetirements.Add(retirement);

        // Kennzeichen freigeben, falls es entfernt wurde.
        if (retirement.LicensePlateRemoved)
        {
            var plate = await _db.LicensePlates
                .FirstOrDefaultAsync(p => p.CurrentVehicleId == vehicle.Id, cancellationToken)
                .ConfigureAwait(false);

            if (plate is not null)
            {
                await _plates.ReleaseFromVehicleAsync(
                    plate.Id, retirement.RetiredAt, "Ausmusterung",
                    LicensePlateStatus.Verfuegbar, cancellationToken).ConfigureAwait(false);
            }
        }

        // Laufende Fahrerzuweisung beenden - die Historie bleibt erhalten.
        var openAssignments = await _db.VehicleDriverAssignments
            .Where(a => a.VehicleId == vehicle.Id && a.ValidTo == null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var assignment in openAssignments)
        {
            assignment.ValidTo = retirement.RetiredAt;
        }

        vehicle.CurrentDriverId = null;
        vehicle.IsRetired = true;

        if (retirement.IsDeregistered)
        {
            vehicle.IsRegistered = false;
        }

        await MoveToStatusAsync(vehicle, VehicleStatusKind.Ausgemustert,
            retirement.Reason.ToString(), retirement.Comment, cancellationToken).ConfigureAwait(false);

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return retirement.Id;
    }

    public async Task UndoRetirementAsync(int vehicleId, string reason, CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.VehicleRetire);

        var vehicle = await _db.Vehicles
            .FirstOrDefaultAsync(v => v.Id == vehicleId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EntityNotFoundException(nameof(Vehicle), vehicleId);

        var retirement = await _db.VehicleRetirements
            .FirstOrDefaultAsync(r => r.VehicleId == vehicleId, cancellationToken)
            .ConfigureAwait(false);

        Guard.That(retirement is not null, "Fuer dieses Fahrzeug liegt keine Ausmusterung vor.");

        _db.VehicleRetirements.Remove(retirement!);
        vehicle.IsRetired = false;

        await MoveToStatusAsync(vehicle, VehicleStatusKind.AusserBetrieb,
            Guard.NotEmpty(reason, "Grund"), "Ausmusterung zurueckgenommen", cancellationToken).ConfigureAwait(false);

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Vehicle>> GetRetiredVehiclesAsync(
        RetirementReason? reason = null,
        CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.VehicleView);

        var query = _db.Vehicles
            .AsNoTracking()
            .Include(v => v.Retirement)
            .Include(v => v.Status)
            .Where(v => v.IsRetired);

        if (reason is { } retirementReason)
        {
            query = query.Where(v => v.Retirement != null && v.Retirement.Reason == retirementReason);
        }

        return await query
            .OrderByDescending(v => v.Retirement!.RetiredAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Setzt einen Systemstatus und schreibt den Historieneintrag.</summary>
    private async Task MoveToStatusAsync(
        Vehicle vehicle,
        VehicleStatusKind kind,
        string? reason,
        string? comment,
        CancellationToken cancellationToken)
    {
        var statusId = await _db.VehicleStatuses
            .Where(s => s.Kind == kind)
            .Select(s => s.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (statusId == 0 || vehicle.VehicleStatusId == statusId)
        {
            return;
        }

        _db.VehicleStatusHistory.Add(new VehicleStatusHistory
        {
            VehicleId = vehicle.Id,
            OldStatusId = vehicle.VehicleStatusId,
            NewStatusId = statusId,
            ChangedAt = _clock.Now,
            ChangedByUserId = _currentUser.User?.Id,
            ChangedByUserName = _currentUser.User?.DisplayName,
            Reason = reason,
            Comment = comment
        });

        vehicle.VehicleStatusId = statusId;
    }
}
