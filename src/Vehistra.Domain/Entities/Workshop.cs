using Vehistra.Domain.Common;
using Vehistra.Domain.Enums;

namespace Vehistra.Domain.Entities;

/// <summary>Werkstatt-Stammdatensatz (administrativ pflegbar).</summary>
public class Workshop : AuditableEntity, IArchivable
{
    public string Name { get; set; } = string.Empty;

    public string? Street { get; set; }

    public string? PostalCode { get; set; }

    public string? City { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? ContactPerson { get; set; }

    public string? Comment { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsArchived { get; set; }

    public DateTime? ArchivedAt { get; set; }

    public int? ArchivedByUserId { get; set; }

    public ICollection<WorkshopOrder> Orders { get; set; } = new List<WorkshopOrder>();

    public string AddressLine => string.Join(", ",
        new[] { Street, string.Join(" ", new[] { PostalCode, City }.Where(s => !string.IsNullOrWhiteSpace(s))) }
            .Where(s => !string.IsNullOrWhiteSpace(s)));
}

/// <summary>Werkstattvorgang / Auftrag an eine Werkstatt.</summary>
public class WorkshopOrder : AuditableEntity
{
    /// <summary>Fortlaufende Vorgangsnummer, z. B. WS-2026-00042.</summary>
    public string OrderNumber { get; set; } = string.Empty;

    public int VehicleId { get; set; }

    public Vehicle? Vehicle { get; set; }

    public int? DriverId { get; set; }

    public Driver? Driver { get; set; }

    public int? WorkshopId { get; set; }

    public Workshop? Workshop { get; set; }

    public DateTime CreatedOn { get; set; }

    /// <summary>Terminvorschlag des Fahrers / der Disposition.</summary>
    public DateTime? ProposedDate { get; set; }

    /// <summary>Tatsaechlich vereinbarter Termin.</summary>
    public DateTime? AppointmentDate { get; set; }

    public string? Reason { get; set; }

    public string? WorkToPerform { get; set; }

    public WorkshopOrderStatus Status { get; set; } = WorkshopOrderStatus.Geplant;

    public DateTime? VehicleHandedOverAt { get; set; }

    public DateTime? PlannedCompletionAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public DateTime? PickedUpAt { get; set; }

    public int? MileageAtHandover { get; set; }

    public decimal? CostNet { get; set; }

    public decimal? CostGross { get; set; }

    public string? InvoiceNumber { get; set; }

    public int? InvoiceDocumentId { get; set; }

    public VehicleDocument? InvoiceDocument { get; set; }

    /// <summary>Naechster Service bei Kilometerstand (Rueckmeldung der Werkstatt).</summary>
    public int? NextServiceMileage { get; set; }

    public string? Comment { get; set; }

    public ICollection<WorkshopTask> Tasks { get; set; } = new List<WorkshopTask>();

    public ICollection<WorkshopDocument> Documents { get; set; } = new List<WorkshopDocument>();

    public ICollection<DamageReport> Damages { get; set; } = new List<DamageReport>();

    public bool IsOpen => Status is not (WorkshopOrderStatus.Abgeholt or WorkshopOrderStatus.Storniert);

    public bool IsVehicleInWorkshop => Status is WorkshopOrderStatus.FahrzeugAbgegeben
        or WorkshopOrderStatus.InBearbeitung or WorkshopOrderStatus.WartetAufTeile or WorkshopOrderStatus.Fertig;
}

/// <summary>Einzelne auszufuehrende Arbeit innerhalb eines Werkstattvorgangs.</summary>
public class WorkshopTask : AuditableEntity
{
    public int WorkshopOrderId { get; set; }

    public WorkshopOrder? Order { get; set; }

    public int Position { get; set; }

    public string Description { get; set; } = string.Empty;

    public bool IsCompleted { get; set; }

    public DateTime? CompletedAt { get; set; }

    /// <summary>Verknuepfter Schaden, sofern die Arbeit aus einer Schadensmeldung stammt.</summary>
    public int? DamageReportId { get; set; }

    public DamageReport? DamageReport { get; set; }

    /// <summary>Kennzeichnet Standardarbeiten aus der Checkbox-Liste des Werkstattberichts.</summary>
    public string? StandardTaskKey { get; set; }

    public decimal? Cost { get; set; }

    public string? Comment { get; set; }
}

/// <summary>Dokument, das einem Werkstattvorgang zugeordnet ist.</summary>
public class WorkshopDocument : EntityBase
{
    public int WorkshopOrderId { get; set; }

    public WorkshopOrder? Order { get; set; }

    public int VehicleDocumentId { get; set; }

    public VehicleDocument? Document { get; set; }

    public DocumentCategory Category { get; set; } = DocumentCategory.Werkstattbericht;

    public DateTime LinkedAt { get; set; }

    public int? LinkedByUserId { get; set; }
}
