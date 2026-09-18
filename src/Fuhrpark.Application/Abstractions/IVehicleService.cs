using Fuhrpark.Application.Common;
using Fuhrpark.Application.Dtos;
using Fuhrpark.Domain.Entities;

namespace Fuhrpark.Application.Abstractions;

/// <summary>Fachliche Verwaltung der Fahrzeuge.</summary>
public interface IVehicleService
{
    Task<PagedResult<VehicleListItem>> GetListAsync(VehicleFilter filter, CancellationToken cancellationToken = default);

    Task<Vehicle?> GetAsync(int id, CancellationToken cancellationToken = default);

    Task<VehicleHeader?> GetHeaderAsync(int id, CancellationToken cancellationToken = default);

    Task<int> CreateAsync(Vehicle vehicle, IEnumerable<int> categoryIds, CancellationToken cancellationToken = default);

    Task UpdateAsync(Vehicle vehicle, IEnumerable<int> categoryIds, CancellationToken cancellationToken = default);

    /// <summary>Aendert den Status und schreibt einen Historieneintrag.</summary>
    Task ChangeStatusAsync(
        int vehicleId,
        int newStatusId,
        string? reason,
        string? comment,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VehicleStatusHistory>> GetStatusHistoryAsync(int vehicleId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VehicleStatus>> GetStatusesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VehicleCategory>> GetCategoriesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<int>> GetCategoryIdsAsync(int vehicleId, CancellationToken cancellationToken = default);

    /// <summary>Vollstaendige Zeitleiste aller Ereignisse eines Fahrzeugs.</summary>
    Task<IReadOnlyList<VehicleTimelineEntry>> GetTimelineAsync(int vehicleId, CancellationToken cancellationToken = default);

    Task<bool> InternalNumberExistsAsync(string internalNumber, int? exceptVehicleId = null, CancellationToken cancellationToken = default);
}
