using Fuhrpark.Domain.Entities;
using Fuhrpark.Domain.Enums;

namespace Fuhrpark.Application.Abstractions;

/// <summary>Erfassung und Auswertung der Kilometerstaende.</summary>
public interface IMileageService
{
    Task<IReadOnlyList<MileageEntry>> GetHistoryAsync(
        int vehicleId,
        int maxCount = 500,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Erfasst einen Kilometerstand. Rueckwaerts laufende Werte werden abgelehnt,
    /// sofern sie nicht ausdruecklich als Korrektur gekennzeichnet sind.
    /// </summary>
    Task<int> AddAsync(
        int vehicleId,
        int mileage,
        DateTime recordedAt,
        MileageSource source,
        string? comment = null,
        bool isCorrection = false,
        CancellationToken cancellationToken = default);

    Task<int?> GetCurrentMileageAsync(int vehicleId, CancellationToken cancellationToken = default);

    /// <summary>Durchschnittliche Laufleistung pro Monat der letzten zwoelf Monate.</summary>
    Task<double?> GetAverageMonthlyMileageAsync(int vehicleId, CancellationToken cancellationToken = default);
}
