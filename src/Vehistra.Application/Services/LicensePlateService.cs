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
public sealed class LicensePlateService : ILicensePlateService
{
    private readonly IVehistraDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;

    public LicensePlateService(IVehistraDbContext db, ICurrentUserService currentUser, IClock clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<IReadOnlyList<LicensePlateListItem>> GetListAsync(
        LicensePlateStatus? status = null,
        string? searchText = null,
        CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.LicensePlateView);

        var today = _clock.Today;
        var query = _db.LicensePlates.AsNoTracking().AsQueryable();

        if (status is { } plateStatus)
        {
            query = query.Where(p => p.Status == plateStatus);
        }

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var text = searchText.Trim();
            query = query.Where(p => p.Plate.Contains(text));
        }

        var rows = await query
            .OrderBy(p => p.Plate)
            .Select(p => new
            {
                p.Id,
                p.Plate,
                p.Status,
                p.CurrentVehicleId,
                p.Comment,
                VehicleDisplay = p.CurrentVehicle != null
                    ? p.CurrentVehicle.InternalNumber + " - " + p.CurrentVehicle.Manufacturer + " " + p.CurrentVehicle.Model
                    : null,
                ReservedUntil = p.Reservations
                    .Where(r => !r.IsReleased)
                    .OrderByDescending(r => r.ReservedUntil)
                    .Select(r => (DateTime?)r.ReservedUntil)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.Select(r =>
        {
            var days = r.ReservedUntil is null ? (int?)null : (r.ReservedUntil.Value.Date - today).Days;
            var level = days switch
            {
                null => WarningLevel.Inaktiv,
                < 0 => WarningLevel.Kritisch,
                <= 7 => WarningLevel.BaldFaellig,
                <= 30 => WarningLevel.Hinweis,
                _ => WarningLevel.Ok
            };

            return new LicensePlateListItem(
                r.Id, r.Plate, r.Status, r.CurrentVehicleId, r.VehicleDisplay,
                r.ReservedUntil, days, level, r.Comment);
        }).ToList();
    }

    public async Task<LicensePlate?> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.LicensePlateView);

        return await _db.LicensePlates
            .Include(p => p.CurrentVehicle)
            .Include(p => p.Reservations)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> CreateAsync(LicensePlate plate, CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.LicensePlateManage);

        plate.Plate = LicensePlateFormatter.Normalize(Guard.NotEmpty(plate.Plate, "Kennzeichen"));

        var duplicate = await _db.LicensePlates
            .AnyAsync(p => p.Plate == plate.Plate, cancellationToken)
            .ConfigureAwait(false);
        Guard.That(!duplicate, $"Das Kennzeichen '{plate.Plate}' ist bereits erfasst.");

        _db.LicensePlates.Add(plate);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return plate.Id;
    }

    public async Task UpdateAsync(LicensePlate plate, CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.LicensePlateManage);

        var existing = await _db.LicensePlates
            .FirstOrDefaultAsync(p => p.Id == plate.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EntityNotFoundException(nameof(LicensePlate), plate.Id);

        var normalized = LicensePlateFormatter.Normalize(Guard.NotEmpty(plate.Plate, "Kennzeichen"));

        var duplicate = await _db.LicensePlates
            .AnyAsync(p => p.Plate == normalized && p.Id != plate.Id, cancellationToken)
            .ConfigureAwait(false);
        Guard.That(!duplicate, $"Das Kennzeichen '{normalized}' ist bereits erfasst.");

        existing.Plate = normalized;
        existing.Status = plate.Status;
        existing.RegistrationOffice = plate.RegistrationOffice;
        existing.Comment = plate.Comment;
        existing.IsSeasonPlate = plate.IsSeasonPlate;
        existing.SeasonFrom = plate.SeasonFrom;
        existing.SeasonTo = plate.SeasonTo;
        existing.RowVersion = plate.RowVersion;

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AssignToVehicleAsync(
        int licensePlateId,
        int vehicleId,
        DateTime validFrom,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.LicensePlateManage);

        var plate = await _db.LicensePlates
            .FirstOrDefaultAsync(p => p.Id == licensePlateId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EntityNotFoundException(nameof(LicensePlate), licensePlateId);

        var vehicle = await _db.Vehicles
            .FirstOrDefaultAsync(v => v.Id == vehicleId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EntityNotFoundException(nameof(Vehicle), vehicleId);

        Guard.That(plate.CurrentVehicleId is null || plate.CurrentVehicleId == vehicleId,
            "Dieses Kennzeichen ist bereits einem anderen Fahrzeug zugeordnet. " +
            "Bitte die bestehende Zuordnung zuerst aufloesen.");

        // Bisheriges Kennzeichen des Fahrzeugs historisieren.
        var openForVehicle = await _db.LicensePlateAssignments
            .Include(a => a.LicensePlate)
            .Where(a => a.VehicleId == vehicleId && a.ValidTo == null && a.LicensePlateId != licensePlateId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var assignment in openForVehicle)
        {
            assignment.ValidTo = validFrom;

            if (assignment.LicensePlate is { } previous)
            {
                previous.CurrentVehicleId = null;
                previous.Status = LicensePlateStatus.Verfuegbar;
            }
        }

        var alreadyAssigned = await _db.LicensePlateAssignments
            .AnyAsync(a => a.LicensePlateId == licensePlateId && a.VehicleId == vehicleId && a.ValidTo == null,
                cancellationToken)
            .ConfigureAwait(false);

        if (!alreadyAssigned)
        {
            _db.LicensePlateAssignments.Add(new LicensePlateAssignment
            {
                LicensePlateId = licensePlateId,
                VehicleId = vehicleId,
                ValidFrom = validFrom,
                Reason = reason,
                AssignedByUserId = _currentUser.User?.Id,
                AssignedByUserName = _currentUser.User?.DisplayName
            });
        }

        plate.CurrentVehicleId = vehicleId;
        plate.Status = LicensePlateStatus.Vergeben;
        vehicle.LicensePlate = plate.Plate;

        // Offene Reservierungen sind mit der Zuteilung erledigt.
        var reservations = await _db.LicensePlateReservations
            .Where(r => r.LicensePlateId == licensePlateId && !r.IsReleased)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var reservation in reservations)
        {
            reservation.IsReleased = true;
            reservation.ReleasedAt = validFrom;
            reservation.Comment = string.IsNullOrWhiteSpace(reservation.Comment)
                ? "Kennzeichen wurde zugeteilt."
                : $"{reservation.Comment}{Environment.NewLine}Kennzeichen wurde zugeteilt.";
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task ReleaseFromVehicleAsync(
        int licensePlateId,
        DateTime validTo,
        string? reason,
        LicensePlateStatus newStatus = LicensePlateStatus.Verfuegbar,
        CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.LicensePlateManage);

        var plate = await _db.LicensePlates
            .FirstOrDefaultAsync(p => p.Id == licensePlateId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EntityNotFoundException(nameof(LicensePlate), licensePlateId);

        var open = await _db.LicensePlateAssignments
            .Where(a => a.LicensePlateId == licensePlateId && a.ValidTo == null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var assignment in open)
        {
            assignment.ValidTo = validTo;
            assignment.Reason = reason ?? assignment.Reason;

            var vehicle = await _db.Vehicles
                .FirstOrDefaultAsync(v => v.Id == assignment.VehicleId, cancellationToken)
                .ConfigureAwait(false);

            if (vehicle is not null && vehicle.LicensePlate == plate.Plate)
            {
                vehicle.LicensePlate = null;
            }
        }

        plate.CurrentVehicleId = null;
        plate.Status = newStatus;

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<LicensePlateAssignment>> GetHistoryAsync(
        int licensePlateId,
        CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.LicensePlateView);

        return await _db.LicensePlateAssignments
            .AsNoTracking()
            .Include(a => a.Vehicle)
            .Where(a => a.LicensePlateId == licensePlateId)
            .OrderByDescending(a => a.ValidFrom)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<LicensePlateAssignment>> GetHistoryForVehicleAsync(
        int vehicleId,
        CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.VehicleView);

        return await _db.LicensePlateAssignments
            .AsNoTracking()
            .Include(a => a.LicensePlate)
            .Where(a => a.VehicleId == vehicleId)
            .OrderByDescending(a => a.ValidFrom)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> CreateReservationAsync(
        LicensePlateReservation reservation,
        CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.LicensePlateManage);

        var plate = await _db.LicensePlates
            .FirstOrDefaultAsync(p => p.Id == reservation.LicensePlateId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EntityNotFoundException(nameof(LicensePlate), reservation.LicensePlateId);

        Guard.That(reservation.ReservedUntil.Date >= reservation.ReservedAt.Date,
            "Das Ende der Reservierung darf nicht vor dem Beginn liegen.");

        var hasOpen = await _db.LicensePlateReservations
            .AnyAsync(r => r.LicensePlateId == reservation.LicensePlateId && !r.IsReleased, cancellationToken)
            .ConfigureAwait(false);
        Guard.That(!hasOpen, "Fuer dieses Kennzeichen existiert bereits eine laufende Reservierung.");

        _db.LicensePlateReservations.Add(reservation);

        if (plate.Status == LicensePlateStatus.Verfuegbar)
        {
            plate.Status = LicensePlateStatus.Reserviert;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return reservation.Id;
    }

    public async Task UpdateReservationAsync(
        LicensePlateReservation reservation,
        CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.LicensePlateManage);

        var existing = await _db.LicensePlateReservations
            .FirstOrDefaultAsync(r => r.Id == reservation.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EntityNotFoundException(nameof(LicensePlateReservation), reservation.Id);

        existing.ReservedAt = reservation.ReservedAt;
        existing.ReservedUntil = reservation.ReservedUntil;
        existing.RegistrationOffice = reservation.RegistrationOffice;
        existing.ReservationNumber = reservation.ReservationNumber;
        existing.PinEncrypted = reservation.PinEncrypted;
        existing.Comment = reservation.Comment;
        existing.RowVersion = reservation.RowVersion;

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task ReleaseReservationAsync(int reservationId, CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.LicensePlateManage);

        var reservation = await _db.LicensePlateReservations
            .Include(r => r.LicensePlate)
            .FirstOrDefaultAsync(r => r.Id == reservationId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EntityNotFoundException(nameof(LicensePlateReservation), reservationId);

        reservation.IsReleased = true;
        reservation.ReleasedAt = _clock.Now;

        if (reservation.LicensePlate is { Status: LicensePlateStatus.Reserviert } plate)
        {
            plate.Status = LicensePlateStatus.Verfuegbar;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<LicensePlateReservation>> GetReservationsAsync(
        bool onlyActive = true,
        CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.LicensePlateView);

        var query = _db.LicensePlateReservations
            .AsNoTracking()
            .Include(r => r.LicensePlate)
            .AsQueryable();

        if (onlyActive)
        {
            query = query.Where(r => !r.IsReleased);
        }

        return await query
            .OrderBy(r => r.ReservedUntil)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
