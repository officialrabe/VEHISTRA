using Vehistra.Domain.Entities;
using Vehistra.Domain.Enums;

namespace Vehistra.Application.Abstractions;

/// <summary>An- und Abmeldung sowie Ausmusterung von Fahrzeugen.</summary>
public interface IVehicleLifecycleService
{
    Task<IReadOnlyList<VehicleRegistration>> GetRegistrationsAsync(int vehicleId, CancellationToken cancellationToken = default);

    /// <summary>Meldet ein Fahrzeug an (Erstzulassung oder Wiederanmeldung).</summary>
    Task<int> RegisterAsync(
        int vehicleId,
        DateTime registeredAt,
        int? licensePlateId,
        string? registrationOffice,
        string? comment,
        CancellationToken cancellationToken = default);

    /// <summary>Meldet ein Fahrzeug ab. Das Fahrzeug bleibt vollstaendig erhalten.</summary>
    Task DeregisterAsync(
        int vehicleId,
        DateTime deregisteredAt,
        string? reason,
        bool releaseLicensePlate,
        string? comment,
        CancellationToken cancellationToken = default);

    Task<VehicleRetirement?> GetRetirementAsync(int vehicleId, CancellationToken cancellationToken = default);

    /// <summary>Mustert ein Fahrzeug aus. Historien bleiben vollstaendig erhalten.</summary>
    Task<int> RetireAsync(VehicleRetirement retirement, CancellationToken cancellationToken = default);

    /// <summary>Nimmt eine Ausmusterung zurueck (z. B. versehentlich erfasst).</summary>
    Task UndoRetirementAsync(int vehicleId, string reason, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Vehicle>> GetRetiredVehiclesAsync(
        RetirementReason? reason = null,
        CancellationToken cancellationToken = default);
}
