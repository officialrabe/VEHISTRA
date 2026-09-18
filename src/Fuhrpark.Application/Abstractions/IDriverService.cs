using Fuhrpark.Domain.Entities;

namespace Fuhrpark.Application.Abstractions;

/// <summary>Fahrerverwaltung inklusive historisierter Fahrzeugzuweisung.</summary>
public interface IDriverService
{
    Task<IReadOnlyList<Driver>> GetDriversAsync(bool includeInactive = false, CancellationToken cancellationToken = default);

    Task<Driver?> GetAsync(int id, CancellationToken cancellationToken = default);

    Task<int> CreateAsync(Driver driver, CancellationToken cancellationToken = default);

    Task UpdateAsync(Driver driver, CancellationToken cancellationToken = default);

    Task SetActiveAsync(int driverId, bool isActive, CancellationToken cancellationToken = default);

    /// <summary>
    /// Weist einem Fahrzeug einen festen Fahrer zu. Eine bestehende Zuweisung wird beendet
    /// und bleibt als Historie erhalten.
    /// </summary>
    Task AssignDriverAsync(
        int vehicleId,
        int? driverId,
        DateTime validFrom,
        string? comment,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VehicleDriverAssignment>> GetAssignmentsForVehicleAsync(
        int vehicleId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VehicleDriverAssignment>> GetAssignmentsForDriverAsync(
        int driverId,
        CancellationToken cancellationToken = default);

    /// <summary>Beantwortet: "Welcher Fahrer hatte dieses Fahrzeug an einem bestimmten Tag?"</summary>
    Task<Driver?> GetDriverAtAsync(int vehicleId, DateTime moment, CancellationToken cancellationToken = default);
}
