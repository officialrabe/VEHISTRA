using Fuhrpark.Application.Dtos;
using Fuhrpark.Domain.Entities;

namespace Fuhrpark.Application.Abstractions;

/// <summary>Wartungsregeln und durchgefuehrte Wartungen.</summary>
public interface IMaintenanceService
{
    Task<IReadOnlyList<MaintenanceRule>> GetRulesAsync(
        int? vehicleId = null,
        CancellationToken cancellationToken = default);

    Task<int> CreateRuleAsync(MaintenanceRule rule, CancellationToken cancellationToken = default);

    Task UpdateRuleAsync(MaintenanceRule rule, CancellationToken cancellationToken = default);

    Task DeleteRuleAsync(int ruleId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MaintenanceEntry>> GetEntriesAsync(int vehicleId, CancellationToken cancellationToken = default);

    /// <summary>Traegt eine durchgefuehrte Wartung ein und berechnet die naechste Faelligkeit.</summary>
    Task<int> AddEntryAsync(MaintenanceEntry entry, CancellationToken cancellationToken = default);

    /// <summary>Ermittelt alle faelligen und bald faelligen Wartungen ueber den gesamten Fuhrpark.</summary>
    Task<IReadOnlyList<MaintenanceDueItem>> GetDueItemsAsync(
        int? vehicleId = null,
        CancellationToken cancellationToken = default);
}
