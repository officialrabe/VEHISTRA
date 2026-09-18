using Fuhrpark.Application.Dtos;
using Fuhrpark.Domain.Entities;
using Fuhrpark.Domain.Enums;

namespace Fuhrpark.Application.Abstractions;

/// <summary>Kennzeichenverwaltung inklusive Reservierungen und Historie.</summary>
public interface ILicensePlateService
{
    Task<IReadOnlyList<LicensePlateListItem>> GetListAsync(
        LicensePlateStatus? status = null,
        string? searchText = null,
        CancellationToken cancellationToken = default);

    Task<LicensePlate?> GetAsync(int id, CancellationToken cancellationToken = default);

    Task<int> CreateAsync(LicensePlate plate, CancellationToken cancellationToken = default);

    Task UpdateAsync(LicensePlate plate, CancellationToken cancellationToken = default);

    /// <summary>Weist ein Kennzeichen einem Fahrzeug zu und historisiert die bisherige Zuordnung.</summary>
    Task AssignToVehicleAsync(
        int licensePlateId,
        int vehicleId,
        DateTime validFrom,
        string? reason,
        CancellationToken cancellationToken = default);

    /// <summary>Loest die aktuelle Zuordnung (z. B. bei Abmeldung).</summary>
    Task ReleaseFromVehicleAsync(
        int licensePlateId,
        DateTime validTo,
        string? reason,
        LicensePlateStatus newStatus = LicensePlateStatus.Verfuegbar,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LicensePlateAssignment>> GetHistoryAsync(int licensePlateId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LicensePlateAssignment>> GetHistoryForVehicleAsync(int vehicleId, CancellationToken cancellationToken = default);

    Task<int> CreateReservationAsync(LicensePlateReservation reservation, CancellationToken cancellationToken = default);

    Task UpdateReservationAsync(LicensePlateReservation reservation, CancellationToken cancellationToken = default);

    Task ReleaseReservationAsync(int reservationId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LicensePlateReservation>> GetReservationsAsync(
        bool onlyActive = true,
        CancellationToken cancellationToken = default);
}
