using Fuhrpark.Domain.Common;
using Fuhrpark.Domain.Enums;

namespace Fuhrpark.Domain.Entities;

/// <summary>Schadenskategorie (administrativ pflegbar).</summary>
public class DamageCategory : AuditableEntity
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsSystemCategory { get; set; }

    public ICollection<DamageReport> Damages { get; set; } = new List<DamageReport>();
}

/// <summary>Schadensmeldung zu einem Fahrzeug.</summary>
public class DamageReport : AuditableEntity
{
    /// <summary>Fortlaufende Schadensnummer, z. B. SCH-2026-00042.</summary>
    public string DamageNumber { get; set; } = string.Empty;

    public int VehicleId { get; set; }

    public Vehicle? Vehicle { get; set; }

    public int? DriverId { get; set; }

    public Driver? Driver { get; set; }

    public DateTime OccurredAt { get; set; }

    public int? Mileage { get; set; }

    public string Description { get; set; } = string.Empty;

    public DamageArea Area { get; set; } = DamageArea.Unbekannt;

    public int? DamageCategoryId { get; set; }

    public DamageCategory? Category { get; set; }

    public DamagePriority Priority { get; set; } = DamagePriority.Normal;

    public bool IsDriveable { get; set; } = true;

    public bool RepairRequired { get; set; } = true;

    public DamageStatus Status { get; set; } = DamageStatus.Gemeldet;

    public int? WorkshopId { get; set; }

    public Workshop? Workshop { get; set; }

    public int? WorkshopOrderId { get; set; }

    public WorkshopOrder? WorkshopOrder { get; set; }

    public DateTime? RepairedAt { get; set; }

    public decimal? CostEstimate { get; set; }

    public decimal? CostActual { get; set; }

    public bool IsInsuranceCase { get; set; }

    public string? InsuranceClaimNumber { get; set; }

    /// <summary>Verknuepfter Unfall, falls der Schaden aus einem Unfall resultiert.</summary>
    public int? AccidentReportId { get; set; }

    public AccidentReport? AccidentReport { get; set; }

    public DateTime? ClosedAt { get; set; }

    public int? ClosedByUserId { get; set; }

    public string? Comment { get; set; }

    public ICollection<DamageAttachment> Attachments { get; set; } = new List<DamageAttachment>();

    public bool IsOpen => Status is not DamageStatus.Geschlossen;

    public bool IsCritical => Priority == DamagePriority.Kritisch && IsOpen;
}

/// <summary>Foto oder Dokument zu einem Schaden.</summary>
public class DamageAttachment : EntityBase
{
    public int DamageReportId { get; set; }

    public DamageReport? DamageReport { get; set; }

    public int VehicleDocumentId { get; set; }

    public VehicleDocument? Document { get; set; }

    public DateTime LinkedAt { get; set; }

    public int? LinkedByUserId { get; set; }

    public string? Caption { get; set; }
}
