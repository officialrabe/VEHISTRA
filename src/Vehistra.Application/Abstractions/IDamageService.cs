using Vehistra.Application.Dtos;
using Vehistra.Domain.Entities;
using Vehistra.Domain.Enums;

namespace Vehistra.Application.Abstractions;

/// <summary>Schadensmanagement.</summary>
public interface IDamageService
{
    Task<IReadOnlyList<DamageListItem>> GetListAsync(
        DamageFilter filter,
        CancellationToken cancellationToken = default);

    Task<DamageReport?> GetAsync(int id, CancellationToken cancellationToken = default);

    Task<int> CreateAsync(DamageReport damage, CancellationToken cancellationToken = default);

    Task UpdateAsync(DamageReport damage, CancellationToken cancellationToken = default);

    Task ChangeStatusAsync(int damageId, DamageStatus status, string? comment, CancellationToken cancellationToken = default);

    Task CloseAsync(int damageId, DateTime closedAt, decimal? actualCost, string? comment, CancellationToken cancellationToken = default);

    /// <summary>
    /// Schadenskategorien. Standardmaessig nur die aktiven; die
    /// Stammdatenverwaltung braucht auch die stillgelegten.
    /// </summary>
    Task<IReadOnlyList<DamageCategory>> GetCategoriesAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    /// <summary>Legt eine eigene Schadenskategorie an.</summary>
    Task<DamageCategory> CreateCategoryAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Aendert Name, Beschreibung, Reihenfolge oder Zustand. Mitgelieferte
    /// Kategorien lassen sich nicht umbenennen: an "Unfall" haengt die
    /// Schadensmeldung aus einem Unfall.
    /// </summary>
    Task UpdateCategoryAsync(DamageCategory category, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loescht eine selbst angelegte Kategorie, die noch an keiner
    /// Schadensmeldung haengt. Mitgelieferte Kategorien bleiben erhalten.
    /// </summary>
    Task DeleteCategoryAsync(int categoryId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DamageReport>> GetOpenForVehicleAsync(int vehicleId, CancellationToken cancellationToken = default);
}

/// <summary>Filter fuer die Schadensuebersicht.</summary>
public sealed class DamageFilter
{
    public int? VehicleId { get; set; }

    public int? DriverId { get; set; }

    public DamageStatus? Status { get; set; }

    public DamagePriority? Priority { get; set; }

    public int? CategoryId { get; set; }

    public bool OnlyOpen { get; set; } = true;

    public bool OnlyNotDriveable { get; set; }

    public bool OnlyInsuranceCases { get; set; }

    public DateTime? From { get; set; }

    public DateTime? To { get; set; }

    public string? SearchText { get; set; }
}
