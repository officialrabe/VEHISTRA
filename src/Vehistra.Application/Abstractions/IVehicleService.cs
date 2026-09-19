using Vehistra.Application.Common;
using Vehistra.Application.Dtos;
using Vehistra.Domain.Entities;

namespace Vehistra.Application.Abstractions;

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

    /// <summary>
    /// Fahrzeugstatus. Standardmaessig nur die aktiven; die
    /// Stammdatenverwaltung braucht auch die stillgelegten.
    /// </summary>
    Task<IReadOnlyList<VehicleStatus>> GetStatusesAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    /// <summary>Legt einen eigenen Fahrzeugstatus an.</summary>
    Task<VehicleStatus> CreateStatusAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Aendert Name, Farbe, Reihenfolge, Zustand und die beiden Kennzeichen
    /// "zaehlt als einsatzbereit" und "zaehlt als verfuegbar". Die Zuordnung zu
    /// einem Systemstatus (Kind) bleibt unveraendert - daran haengt die
    /// Programmlogik, nicht am Namen.
    /// </summary>
    Task UpdateStatusAsync(VehicleStatus status, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loescht einen selbst angelegten Status. Mitgelieferte Status, solche mit
    /// einer Systemzuordnung und solche, die noch an Fahrzeugen oder in der
    /// Statushistorie haengen, bleiben erhalten - sie lassen sich stilllegen.
    /// </summary>
    Task DeleteStatusAsync(int statusId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Einsatzbereiche (Fahrzeugkategorien). Standardmaessig nur die aktiven;
    /// die Stammdatenverwaltung braucht auch die stillgelegten, sonst liesse
    /// sich eine deaktivierte Kategorie nicht wieder einschalten.
    /// </summary>
    Task<IReadOnlyList<VehicleCategory>> GetCategoriesAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    /// <summary>Legt einen eigenen Einsatzbereich an.</summary>
    Task<VehicleCategory> CreateCategoryAsync(
        string name,
        string? colorHex = null,
        CancellationToken cancellationToken = default);

    /// <summary>Aendert Name, Farbe, Reihenfolge oder Zustand eines Einsatzbereichs.</summary>
    Task UpdateCategoryAsync(VehicleCategory category, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loescht einen selbst angelegten Einsatzbereich. Mitgelieferte Bereiche und
    /// solche, die noch an Fahrzeugen oder Wartungsregeln haengen, bleiben
    /// erhalten - sie lassen sich stattdessen stilllegen.
    /// </summary>
    Task DeleteCategoryAsync(int categoryId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<int>> GetCategoryIdsAsync(int vehicleId, CancellationToken cancellationToken = default);

    /// <summary>Vollstaendige Zeitleiste aller Ereignisse eines Fahrzeugs.</summary>
    Task<IReadOnlyList<VehicleTimelineEntry>> GetTimelineAsync(int vehicleId, CancellationToken cancellationToken = default);

    Task<bool> InternalNumberExistsAsync(string internalNumber, int? exceptVehicleId = null, CancellationToken cancellationToken = default);
}
