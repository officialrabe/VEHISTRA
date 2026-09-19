using Vehistra.Application.Abstractions;
using Vehistra.Application.Common;
using Vehistra.Domain.Entities;
using Vehistra.Domain.Enums;
using Vehistra.Domain.Exceptions;
using Vehistra.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace Vehistra.Application.Services;

/// <inheritdoc />
public sealed class MileageService : IMileageService
{
    private readonly IVehistraDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;

    public MileageService(IVehistraDbContext db, ICurrentUserService currentUser, IClock clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<IReadOnlyList<MileageEntry>> GetHistoryAsync(
        int vehicleId,
        int maxCount = 500,
        CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.VehicleView);

        return await _db.MileageEntries
            .AsNoTracking()
            .Where(m => m.VehicleId == vehicleId)
            .OrderByDescending(m => m.RecordedAt)
            .ThenByDescending(m => m.Id)
            .Take(maxCount)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> AddAsync(
        int vehicleId,
        int mileage,
        DateTime recordedAt,
        MileageSource source,
        string? comment = null,
        bool isCorrection = false,
        CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.MileageEdit);

        Guard.NotNegative(mileage, "Kilometerstand");

        var vehicle = await _db.Vehicles.FirstOrDefaultAsync(v => v.Id == vehicleId, cancellationToken).ConfigureAwait(false)
            ?? throw new EntityNotFoundException(nameof(Vehicle), vehicleId);

        var lastMileage = await _db.MileageEntries
            .Where(m => m.VehicleId == vehicleId && !m.IsCorrection)
            .OrderByDescending(m => m.RecordedAt)
            .ThenByDescending(m => m.Id)
            .Select(m => (int?)m.Mileage)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        // Auch der am Fahrzeug gefuehrte Stand zaehlt: Fahrzeuge koennen mit einem
        // Anfangskilometerstand angelegt oder importiert werden, ohne dass es dazu
        // bereits einen Historieneintrag gibt.
        var reference = Math.Max(lastMileage ?? 0, vehicle.CurrentMileage);

        if (!isCorrection && reference > 0 && mileage < reference)
        {
            throw new BusinessRuleException(
                $"Der neue Kilometerstand ({mileage:N0} km) liegt unter dem zuletzt erfassten Wert ({reference:N0} km). " +
                "Bitte den Wert pruefen oder die Eingabe als Korrektur kennzeichnen.");
        }

        var entry = new MileageEntry
        {
            VehicleId = vehicleId,
            Mileage = mileage,
            RecordedAt = recordedAt,
            RecordedByUserId = _currentUser.User?.Id,
            RecordedByUserName = _currentUser.User?.DisplayName,
            Source = source,
            Comment = comment,
            IsCorrection = isCorrection
        };

        _db.MileageEntries.Add(entry);

        // Den denormalisierten Stand am Fahrzeug nur fortschreiben, wenn der Eintrag der neueste ist.
        if (vehicle.CurrentMileageAt is null || recordedAt >= vehicle.CurrentMileageAt)
        {
            vehicle.CurrentMileage = mileage;
            vehicle.CurrentMileageAt = recordedAt;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return entry.Id;
    }

    public async Task<int?> GetCurrentMileageAsync(int vehicleId, CancellationToken cancellationToken = default)
    {
        return await _db.MileageEntries
            .AsNoTracking()
            .Where(m => m.VehicleId == vehicleId)
            .OrderByDescending(m => m.RecordedAt)
            .ThenByDescending(m => m.Id)
            .Select(m => (int?)m.Mileage)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<double?> GetAverageMonthlyMileageAsync(
        int vehicleId,
        CancellationToken cancellationToken = default)
    {
        var from = _clock.Today.AddMonths(-12);

        var entries = await _db.MileageEntries
            .AsNoTracking()
            .Where(m => m.VehicleId == vehicleId && m.RecordedAt >= from && !m.IsCorrection)
            .OrderBy(m => m.RecordedAt)
            .Select(m => new { m.RecordedAt, m.Mileage })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (entries.Count < 2)
        {
            return null;
        }

        var first = entries[0];
        var last = entries[^1];
        var months = (last.RecordedAt - first.RecordedAt).TotalDays / 30.44;

        return months <= 0 ? null : (last.Mileage - first.Mileage) / months;
    }
}
