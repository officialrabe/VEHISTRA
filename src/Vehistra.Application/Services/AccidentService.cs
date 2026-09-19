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
public sealed class AccidentService : IAccidentService
{
    private readonly IVehistraDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly NumberGenerator _numbers;
    private readonly IDamageService _damages;
    private readonly IClock _clock;

    public AccidentService(
        IVehistraDbContext db,
        ICurrentUserService currentUser,
        NumberGenerator numbers,
        IDamageService damages,
        IClock clock)
    {
        _db = db;
        _currentUser = currentUser;
        _numbers = numbers;
        _damages = damages;
        _clock = clock;
    }

    public Task<IReadOnlyList<AccidentListItem>> GetListAsync(
        int? vehicleId = null,
        bool onlyOpen = false,
        CancellationToken cancellationToken = default) =>
        ListeAsync(vehicleId, null, onlyOpen, cancellationToken);

    public Task<IReadOnlyList<AccidentListItem>> GetForDriverAsync(
        int driverId,
        CancellationToken cancellationToken = default) =>
        ListeAsync(null, driverId, false, cancellationToken);

    private async Task<IReadOnlyList<AccidentListItem>> ListeAsync(
        int? vehicleId,
        int? driverId,
        bool onlyOpen,
        CancellationToken cancellationToken)
    {
        _currentUser.DemandPermission(Permissions.AccidentView);

        var query = _db.AccidentReports.AsNoTracking().AsQueryable();

        if (vehicleId is { } id)
        {
            query = query.Where(a => a.VehicleId == id);
        }

        if (driverId is { } fahrer)
        {
            query = query.Where(a => a.DriverId == fahrer);
        }

        if (onlyOpen)
        {
            query = query.Where(a => a.ClosedAt == null);
        }

        return await query
            .OrderByDescending(a => a.OccurredAt)
            .Select(a => new AccidentListItem(
                a.Id,
                a.AccidentNumber,
                a.VehicleId,
                a.Vehicle != null
                    ? (a.Vehicle.LicensePlate ?? a.Vehicle.InternalNumber) + " - " + a.Vehicle.Manufacturer + " " + a.Vehicle.Model
                    : string.Empty,
                a.Driver != null ? a.Driver.FirstName + " " + a.Driver.LastName : null,
                a.OccurredAt,
                a.Location,
                a.Type,
                a.PoliceInvolved,
                a.PersonalInjury,
                a.ClosedAt == null))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<AccidentReport?> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.AccidentView);

        return await _db.AccidentReports
            .Include(a => a.Vehicle)
            .Include(a => a.Driver)
            .Include(a => a.VehicleInsurance)
            .Include(a => a.Participants)
            .Include(a => a.Witnesses)
            .Include(a => a.Attachments)
                .ThenInclude(at => at.Document)
            .Include(a => a.Damages)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> CreateAsync(AccidentReport accident, CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.AccidentCreate);

        var vehicle = await _db.Vehicles
            .FirstOrDefaultAsync(v => v.Id == accident.VehicleId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EntityNotFoundException(nameof(Vehicle), accident.VehicleId);

        if (string.IsNullOrWhiteSpace(accident.AccidentNumber))
        {
            accident.AccidentNumber = await _numbers.NextAccidentNumberAsync(cancellationToken).ConfigureAwait(false);
        }

        accident.DriverId ??= await _db.VehicleDriverAssignments
            .Where(a => a.VehicleId == accident.VehicleId
                        && a.ValidFrom <= accident.OccurredAt
                        && (a.ValidTo == null || a.ValidTo >= accident.OccurredAt))
            .OrderByDescending(a => a.ValidFrom)
            .Select(a => (int?)a.DriverId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (accident.DriverId is { } driverId && string.IsNullOrWhiteSpace(accident.DriverPhone))
        {
            accident.DriverPhone = await _db.Drivers
                .Where(d => d.Id == driverId)
                .Select(d => d.Phone ?? d.Mobile)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        accident.VehicleInsuranceId ??= await _db.VehicleInsurances
            .Where(i => i.VehicleId == accident.VehicleId && i.IsActive)
            .OrderByDescending(i => i.ValidFrom)
            .Select(i => (int?)i.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        _db.AccidentReports.Add(accident);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return accident.Id;
    }

    public async Task UpdateAsync(AccidentReport accident, CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.AccidentEdit);

        var existing = await _db.AccidentReports
            .FirstOrDefaultAsync(a => a.Id == accident.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EntityNotFoundException(nameof(AccidentReport), accident.Id);

        existing.DriverId = accident.DriverId;
        existing.DriverPhone = accident.DriverPhone;
        existing.OccurredAt = accident.OccurredAt;
        existing.Location = accident.Location;
        existing.Street = accident.Street;
        existing.PostalCode = accident.PostalCode;
        existing.City = accident.City;
        existing.Mileage = accident.Mileage;
        existing.Type = accident.Type;
        existing.TypeOther = accident.TypeOther;
        existing.CourseOfEvents = accident.CourseOfEvents;
        existing.ThirdPartyInvolved = accident.ThirdPartyInvolved;
        existing.PersonalInjury = accident.PersonalInjury;
        existing.PoliceInvolved = accident.PoliceInvolved;
        existing.PoliceStation = accident.PoliceStation;
        existing.PoliceFileNumber = accident.PoliceFileNumber;
        existing.VehicleDriveable = accident.VehicleDriveable;
        existing.VehicleInsuranceId = accident.VehicleInsuranceId;
        existing.OwnInsuranceClaimNumber = accident.OwnInsuranceClaimNumber;
        existing.Comment = accident.Comment;
        existing.RowVersion = accident.RowVersion;

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task CloseAsync(int accidentId, CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.AccidentEdit);

        var accident = await _db.AccidentReports
            .FirstOrDefaultAsync(a => a.Id == accidentId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EntityNotFoundException(nameof(AccidentReport), accidentId);

        var openDamages = await _db.DamageReports
            .CountAsync(d => d.AccidentReportId == accidentId && d.Status != DamageStatus.Geschlossen, cancellationToken)
            .ConfigureAwait(false);

        Guard.That(openDamages == 0,
            "Zu diesem Unfall existieren noch offene Schaeden. Bitte diese zuerst abschliessen.");

        accident.ClosedAt = _clock.Now;
        accident.ClosedByUserId = _currentUser.User?.Id;

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> AddParticipantAsync(
        AccidentParticipant participant,
        CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.AccidentEdit);

        _db.AccidentParticipants.Add(participant);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return participant.Id;
    }

    public async Task RemoveParticipantAsync(int participantId, CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.AccidentEdit);

        var participant = await _db.AccidentParticipants
            .FirstOrDefaultAsync(p => p.Id == participantId, cancellationToken)
            .ConfigureAwait(false);

        if (participant is null)
        {
            return;
        }

        _db.AccidentParticipants.Remove(participant);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> AddWitnessAsync(AccidentWitness witness, CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.AccidentEdit);

        _db.AccidentWitnesses.Add(witness);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return witness.Id;
    }

    public async Task RemoveWitnessAsync(int witnessId, CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.AccidentEdit);

        var witness = await _db.AccidentWitnesses
            .FirstOrDefaultAsync(w => w.Id == witnessId, cancellationToken)
            .ConfigureAwait(false);

        if (witness is null)
        {
            return;
        }

        _db.AccidentWitnesses.Remove(witness);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> CreateDamageFromAccidentAsync(
        int accidentId,
        string description,
        CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.DamageCreate);

        var accident = await _db.AccidentReports
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == accidentId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EntityNotFoundException(nameof(AccidentReport), accidentId);

        var accidentCategoryId = await _db.DamageCategories
            .Where(c => c.Name == "Unfall")
            .Select(c => (int?)c.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var damage = new DamageReport
        {
            VehicleId = accident.VehicleId,
            DriverId = accident.DriverId,
            OccurredAt = accident.OccurredAt,
            Mileage = accident.Mileage,
            Description = Guard.NotEmpty(description, "Schadensbeschreibung"),
            DamageCategoryId = accidentCategoryId,
            Priority = accident.VehicleDriveable ? DamagePriority.Hoch : DamagePriority.Kritisch,
            IsDriveable = accident.VehicleDriveable,
            RepairRequired = true,
            Status = DamageStatus.Gemeldet,
            IsInsuranceCase = true,
            AccidentReportId = accidentId,
            Comment = $"Automatisch erzeugt aus Unfall {accident.AccidentNumber}."
        };

        return await _damages.CreateAsync(damage, cancellationToken).ConfigureAwait(false);
    }
}
