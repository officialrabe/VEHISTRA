using Vehistra.Domain.Common;
using Vehistra.Domain.Enums;

namespace Vehistra.Domain.Entities;

/// <summary>Fahrzeugstammdaten.</summary>
public class Vehicle : AuditableEntity, IArchivable
{
    /// <summary>Interne Fahrzeugnummer (eindeutig im Unternehmen).</summary>
    public string InternalNumber { get; set; } = string.Empty;

    /// <summary>Aktuelles Kennzeichen (denormalisiert fuer schnelle Suche; Historie in LicensePlateAssignments).</summary>
    public string? LicensePlate { get; set; }

    /// <summary>Fahrzeug-Identifizierungsnummer.</summary>
    public string? Vin { get; set; }

    public string Manufacturer { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public string? Variant { get; set; }

    public int? BuildYear { get; set; }

    public DateTime? FirstRegistration { get; set; }

    public FuelType FuelType { get; set; } = FuelType.Unbekannt;

    public TransmissionType Transmission { get; set; } = TransmissionType.Unbekannt;

    /// <summary>Leistung in kW.</summary>
    public int? PowerKw { get; set; }

    public string? Color { get; set; }

    public int? Seats { get; set; }

    /// <summary>Zuletzt bekannter Kilometerstand (abgeleitet aus MileageEntries).</summary>
    public int CurrentMileage { get; set; }

    public DateTime? CurrentMileageAt { get; set; }

    /// <summary>Herstellerschluesselnummer.</summary>
    public string? Hsn { get; set; }

    /// <summary>Typschluesselnummer.</summary>
    public string? Tsn { get; set; }

    public DateTime? PurchaseDate { get; set; }

    public decimal? PurchasePrice { get; set; }

    public bool IsLeased { get; set; }

    public string? LeasingCompany { get; set; }

    public string? LeasingContractNumber { get; set; }

    public DateTime? LeasingStart { get; set; }

    public DateTime? LeasingEnd { get; set; }

    public string? Comment { get; set; }

    public int VehicleStatusId { get; set; }

    public VehicleStatus? Status { get; set; }

    /// <summary>Aktueller fester Fahrer (denormalisiert; Historie in VehicleDriverAssignments).</summary>
    public int? CurrentDriverId { get; set; }

    public Driver? CurrentDriver { get; set; }

    /// <summary>Naechste faellige Hauptuntersuchung (denormalisiert aus VehicleInspections).</summary>
    public DateTime? NextInspectionDue { get; set; }

    public bool IsRegistered { get; set; } = true;

    public bool IsRetired { get; set; }

    public bool IsArchived { get; set; }

    public DateTime? ArchivedAt { get; set; }

    public int? ArchivedByUserId { get; set; }

    public ICollection<VehicleCategoryAssignment> CategoryAssignments { get; set; } = new List<VehicleCategoryAssignment>();

    public ICollection<VehicleStatusHistory> StatusHistory { get; set; } = new List<VehicleStatusHistory>();

    public ICollection<VehicleDriverAssignment> DriverAssignments { get; set; } = new List<VehicleDriverAssignment>();

    public ICollection<MileageEntry> MileageEntries { get; set; } = new List<MileageEntry>();

    public ICollection<VehicleInspection> Inspections { get; set; } = new List<VehicleInspection>();

    public ICollection<MaintenanceEntry> MaintenanceEntries { get; set; } = new List<MaintenanceEntry>();

    public ICollection<DamageReport> Damages { get; set; } = new List<DamageReport>();

    public ICollection<AccidentReport> Accidents { get; set; } = new List<AccidentReport>();

    public ICollection<WorkshopOrder> WorkshopOrders { get; set; } = new List<WorkshopOrder>();

    public ICollection<VehicleDocument> Documents { get; set; } = new List<VehicleDocument>();

    public ICollection<LicensePlateAssignment> LicensePlateAssignments { get; set; } = new List<LicensePlateAssignment>();

    public ICollection<VehicleRegistration> Registrations { get; set; } = new List<VehicleRegistration>();

    public ICollection<VehicleInsurance> Insurances { get; set; } = new List<VehicleInsurance>();

    public ICollection<VehicleKey> Keys { get; set; } = new List<VehicleKey>();

    public VehicleRetirement? Retirement { get; set; }

    /// <summary>Anzeigename fuer Listen und Berichte.</summary>
    public string DisplayName =>
        $"{(string.IsNullOrWhiteSpace(LicensePlate) ? InternalNumber : LicensePlate)} - {Manufacturer} {Model}".Trim();
}

/// <summary>Einsatzbereich / Kategorie eines Fahrzeugs (administrativ pflegbar).</summary>
public class VehicleCategory : AuditableEntity
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Farbcode fuer Badges (z. B. #2E7D32).</summary>
    public string? ColorHex { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsSystemCategory { get; set; }

    public ICollection<VehicleCategoryAssignment> Assignments { get; set; } = new List<VehicleCategoryAssignment>();
}

/// <summary>Ein Fahrzeug kann mehreren Einsatzbereichen zugeordnet sein.</summary>
public class VehicleCategoryAssignment : EntityBase
{
    public int VehicleId { get; set; }

    public Vehicle? Vehicle { get; set; }

    public int VehicleCategoryId { get; set; }

    public VehicleCategory? Category { get; set; }

    /// <summary>Primaerer Einsatzbereich des Fahrzeugs.</summary>
    public bool IsPrimary { get; set; }

    public DateTime AssignedAt { get; set; }

    public int? AssignedByUserId { get; set; }
}

/// <summary>Fahrzeugstatus als Stammdatum (Standardwerte werden beim Einrichten angelegt).</summary>
public class VehicleStatus : AuditableEntity
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Zuordnung zu einem bekannten Systemstatus, sofern vorhanden.</summary>
    public VehicleStatusKind? Kind { get; set; }

    public string? ColorHex { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsSystemStatus { get; set; }

    /// <summary>Fahrzeuge in diesem Status gelten als einsatzbereit.</summary>
    public bool CountsAsOperational { get; set; }

    /// <summary>Fahrzeuge in diesem Status gelten als verfuegbar (nicht im Einsatz).</summary>
    public bool CountsAsAvailable { get; set; }
}

/// <summary>Historie aller Statuswechsel eines Fahrzeugs. Wird niemals ueberschrieben.</summary>
public class VehicleStatusHistory : EntityBase
{
    public int VehicleId { get; set; }

    public Vehicle? Vehicle { get; set; }

    public int? OldStatusId { get; set; }

    public VehicleStatus? OldStatus { get; set; }

    public int NewStatusId { get; set; }

    public VehicleStatus? NewStatus { get; set; }

    public DateTime ChangedAt { get; set; }

    public int? ChangedByUserId { get; set; }

    public string? ChangedByUserName { get; set; }

    public string? Reason { get; set; }

    public string? Comment { get; set; }
}
