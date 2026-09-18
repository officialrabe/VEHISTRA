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

    Task<IReadOnlyList<DamageCategory>> GetCategoriesAsync(CancellationToken cancellationToken = default);

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
