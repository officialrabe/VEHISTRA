using Fuhrpark.Domain.Entities;

namespace Fuhrpark.Application.Abstractions;

/// <summary>Versicherungsvertraege der Fahrzeuge.</summary>
public interface IInsuranceService
{
    Task<IReadOnlyList<VehicleInsurance>> GetForVehicleAsync(int vehicleId, CancellationToken cancellationToken = default);

    Task<VehicleInsurance?> GetActiveAsync(int vehicleId, CancellationToken cancellationToken = default);

    Task<int> CreateAsync(VehicleInsurance insurance, CancellationToken cancellationToken = default);

    Task UpdateAsync(VehicleInsurance insurance, CancellationToken cancellationToken = default);

    Task DeactivateAsync(int insuranceId, CancellationToken cancellationToken = default);
}

/// <summary>Verwaltung der Fahrzeugschluessel.</summary>
public interface IVehicleKeyService
{
    Task<IReadOnlyList<VehicleKey>> GetForVehicleAsync(int vehicleId, CancellationToken cancellationToken = default);

    Task<int> CreateAsync(VehicleKey key, CancellationToken cancellationToken = default);

    Task UpdateAsync(VehicleKey key, CancellationToken cancellationToken = default);

    Task IssueAsync(int keyId, int? driverId, string? issuedToName, DateTime issuedAt, CancellationToken cancellationToken = default);

    Task ReturnAsync(int keyId, DateTime returnedAt, CancellationToken cancellationToken = default);

    Task DeleteAsync(int keyId, CancellationToken cancellationToken = default);
}
