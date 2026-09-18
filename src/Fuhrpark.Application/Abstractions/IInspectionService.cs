using Fuhrpark.Application.Dtos;
using Fuhrpark.Domain.Entities;

namespace Fuhrpark.Application.Abstractions;

/// <summary>Verwaltung von Hauptuntersuchung, Abgasuntersuchung und vergleichbaren Fristen.</summary>
public interface IInspectionService
{
    Task<IReadOnlyList<InspectionListItem>> GetOverviewAsync(
        int? withinDays = null,
        bool includeRetired = false,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VehicleInspection>> GetForVehicleAsync(int vehicleId, CancellationToken cancellationToken = default);

    Task<int> AddAsync(VehicleInspection inspection, CancellationToken cancellationToken = default);

    Task UpdateAsync(VehicleInspection inspection, CancellationToken cancellationToken = default);

    Task DeleteAsync(int inspectionId, CancellationToken cancellationToken = default);
}
