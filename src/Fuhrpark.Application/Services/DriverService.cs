using Fuhrpark.Application.Abstractions;
using Fuhrpark.Application.Common;
using Fuhrpark.Domain.Entities;
using Fuhrpark.Domain.Exceptions;
using Fuhrpark.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace Fuhrpark.Application.Services;

/// <inheritdoc />
public sealed class DriverService : IDriverService
{
    private readonly IFuhrparkDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;

    public DriverService(IFuhrparkDbContext db, ICurrentUserService currentUser, IClock clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<IReadOnlyList<Driver>> GetDriversAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.DriverView);

        var query = _db.Drivers.AsNoTracking().Where(d => !d.IsArchived);

        if (!includeInactive)
        {
            query = query.Where(d => d.IsActive);
        }

        return await query
            .OrderBy(d => d.LastName)
            .ThenBy(d => d.FirstName)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Driver?> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.DriverView);

        return await _db.Drivers
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> CreateAsync(Driver driver, CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.DriverEdit);

        driver.LastName = Guard.NotEmpty(driver.LastName, "Nachname");

        if (!string.IsNullOrWhiteSpace(driver.PersonnelNumber))
        {
            var duplicate = await _db.Drivers
                .AnyAsync(d => d.PersonnelNumber == driver.PersonnelNumber, cancellationToken)
                .ConfigureAwait(false);
            Guard.That(!duplicate, $"Die Personalnummer '{driver.PersonnelNumber}' ist bereits vergeben.");
        }

        _db.Drivers.Add(driver);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return driver.Id;
    }

    public async Task UpdateAsync(Driver driver, CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.DriverEdit);

        var existing = await _db.Drivers.FirstOrDefaultAsync(d => d.Id == driver.Id, cancellationToken).ConfigureAwait(false)
            ?? throw new EntityNotFoundException(nameof(Driver), driver.Id);

        if (!string.IsNullOrWhiteSpace(driver.PersonnelNumber))
        {
            var duplicate = await _db.Drivers
                .AnyAsync(d => d.PersonnelNumber == driver.PersonnelNumber && d.Id != driver.Id, cancellationToken)
                .ConfigureAwait(false);
            Guard.That(!duplicate, $"Die Personalnummer '{driver.PersonnelNumber}' ist bereits vergeben.");
        }

        existing.PersonnelNumber = driver.PersonnelNumber;
        existing.FirstName = driver.FirstName;
        existing.LastName = Guard.NotEmpty(driver.LastName, "Nachname");
        existing.Phone = driver.Phone;
        existing.Mobile = driver.Mobile;
        existing.Email = driver.Email;
        existing.IsActive = driver.IsActive;
        existing.Comment = driver.Comment;
        existing.RowVersion = driver.RowVersion;

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SetActiveAsync(int driverId, bool isActive, CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.DriverEdit);

        var driver = await _db.Drivers.FirstOrDefaultAsync(d => d.Id == driverId, cancellationToken).ConfigureAwait(false)
            ?? throw new EntityNotFoundException(nameof(Driver), driverId);

        if (!isActive)
        {
            var assignedVehicles = await _db.Vehicles
                .CountAsync(v => v.CurrentDriverId == driverId && !v.IsRetired, cancellationToken)
                .ConfigureAwait(false);

            Guard.That(assignedVehicles == 0,
                "Dem Fahrer sind noch Fahrzeuge fest zugewiesen. Bitte zuerst die Zuweisungen beenden.");
        }

        driver.IsActive = isActive;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AssignDriverAsync(
        int vehicleId,
        int? driverId,
        DateTime validFrom,
        string? comment,
        CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.DriverAssign);

        var vehicle = await _db.Vehicles.FirstOrDefaultAsync(v => v.Id == vehicleId, cancellationToken).ConfigureAwait(false)
            ?? throw new EntityNotFoundException(nameof(Vehicle), vehicleId);

        if (driverId is { } newDriverId)
        {
            var driverExists = await _db.Drivers
                .AnyAsync(d => d.Id == newDriverId && d.IsActive, cancellationToken)
                .ConfigureAwait(false);
            Guard.That(driverExists, "Der gewaehlte Fahrer existiert nicht oder ist nicht aktiv.");
        }

        if (vehicle.CurrentDriverId == driverId)
        {
            return;
        }

        // Laufende Zuweisung beenden - der Datensatz bleibt als Historie erhalten.
        var open = await _db.VehicleDriverAssignments
            .Where(a => a.VehicleId == vehicleId && a.ValidTo == null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var assignment in open)
        {
            assignment.ValidTo = validFrom;
        }

        if (driverId is { } assignedDriverId)
        {
            _db.VehicleDriverAssignments.Add(new VehicleDriverAssignment
            {
                VehicleId = vehicleId,
                DriverId = assignedDriverId,
                ValidFrom = validFrom,
                AssignedByUserId = _currentUser.User?.Id,
                AssignedByUserName = _currentUser.User?.DisplayName,
                Comment = comment
            });
        }

        vehicle.CurrentDriverId = driverId;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<VehicleDriverAssignment>> GetAssignmentsForVehicleAsync(
        int vehicleId,
        CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.VehicleView);

        return await _db.VehicleDriverAssignments
            .AsNoTracking()
            .Include(a => a.Driver)
            .Where(a => a.VehicleId == vehicleId)
            .OrderByDescending(a => a.ValidFrom)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<VehicleDriverAssignment>> GetAssignmentsForDriverAsync(
        int driverId,
        CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.DriverView);

        return await _db.VehicleDriverAssignments
            .AsNoTracking()
            .Include(a => a.Vehicle)
            .Where(a => a.DriverId == driverId)
            .OrderByDescending(a => a.ValidFrom)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Driver?> GetDriverAtAsync(
        int vehicleId,
        DateTime moment,
        CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.VehicleView);

        return await _db.VehicleDriverAssignments
            .AsNoTracking()
            .Include(a => a.Driver)
            .Where(a => a.VehicleId == vehicleId
                        && a.ValidFrom <= moment
                        && (a.ValidTo == null || a.ValidTo >= moment))
            .OrderByDescending(a => a.ValidFrom)
            .Select(a => a.Driver)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
