using Vehistra.Application.Dtos;
using Vehistra.Domain.Entities;
using Vehistra.Domain.Enums;

namespace Vehistra.Application.Abstractions;

/// <summary>Werkstattmanagement.</summary>
public interface IWorkshopService
{
    Task<IReadOnlyList<WorkshopOrderListItem>> GetOrdersAsync(
        WorkshopFilter filter,
        CancellationToken cancellationToken = default);

    Task<WorkshopOrder?> GetOrderAsync(int id, CancellationToken cancellationToken = default);

    Task<int> CreateOrderAsync(
        WorkshopOrder order,
        IEnumerable<int>? damageIds = null,
        CancellationToken cancellationToken = default);

    Task UpdateOrderAsync(WorkshopOrder order, CancellationToken cancellationToken = default);

    Task ChangeStatusAsync(int orderId, WorkshopOrderStatus status, CancellationToken cancellationToken = default);

    /// <summary>Uebernimmt die Rueckmeldung der Werkstatt (Kosten, Fertigstellung, naechster Service).</summary>
    Task RecordFeedbackAsync(WorkshopFeedback feedback, CancellationToken cancellationToken = default);

    Task<int> AddTaskAsync(WorkshopTask task, CancellationToken cancellationToken = default);

    Task UpdateTaskAsync(WorkshopTask task, CancellationToken cancellationToken = default);

    Task RemoveTaskAsync(int taskId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Workshop>> GetWorkshopsAsync(bool includeInactive = false, CancellationToken cancellationToken = default);

    Task<int> CreateWorkshopAsync(Workshop workshop, CancellationToken cancellationToken = default);

    Task UpdateWorkshopAsync(Workshop workshop, CancellationToken cancellationToken = default);
}

/// <summary>Filter fuer die Werkstattuebersicht.</summary>
public sealed class WorkshopFilter
{
    public int? VehicleId { get; set; }

    public int? WorkshopId { get; set; }

    public WorkshopOrderStatus? Status { get; set; }

    public bool OnlyOpen { get; set; } = true;

    public bool OnlyInWorkshop { get; set; }

    public DateTime? From { get; set; }

    public DateTime? To { get; set; }

    public string? SearchText { get; set; }
}

/// <summary>Rueckmeldung der Werkstatt zu einem Auftrag.</summary>
public sealed class WorkshopFeedback
{
    public int OrderId { get; set; }

    public decimal? CostNet { get; set; }

    public decimal? CostGross { get; set; }

    public DateTime? CompletedAt { get; set; }

    public int? NextServiceMileage { get; set; }

    public string? InvoiceNumber { get; set; }

    public int? MileageAtHandover { get; set; }

    public string? Comment { get; set; }

    /// <summary>Ids der erledigten Arbeiten.</summary>
    public IReadOnlyList<int> CompletedTaskIds { get; set; } = [];
}
