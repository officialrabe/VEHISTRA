using Fuhrpark.Domain.Common;
using Fuhrpark.Domain.Enums;

namespace Fuhrpark.Domain.Entities;

/// <summary>
/// Wartungsregel (Wartungstyp). Administratoren koennen eigene Regeln anlegen,
/// entweder global oder fuer ein einzelnes Fahrzeug.
/// </summary>
public class MaintenanceRule : AuditableEntity
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public MaintenanceIntervalType IntervalType { get; set; } = MaintenanceIntervalType.DatumOderKilometer;

    /// <summary>Intervall in Monaten (bei datumsbasierten Regeln).</summary>
    public int? IntervalMonths { get; set; }

    /// <summary>Intervall in Kilometern (bei kilometerbasierten Regeln).</summary>
    public int? IntervalKilometers { get; set; }

    /// <summary>Vorwarnzeit in Tagen.</summary>
    public int WarnDaysBefore { get; set; } = 30;

    /// <summary>Vorwarnung in Kilometern.</summary>
    public int WarnKilometersBefore { get; set; } = 1000;

    /// <summary>Null = gilt fuer alle Fahrzeuge.</summary>
    public int? VehicleId { get; set; }

    public Vehicle? Vehicle { get; set; }

    /// <summary>Null = gilt fuer alle Kategorien.</summary>
    public int? VehicleCategoryId { get; set; }

    public VehicleCategory? VehicleCategory { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsSystemRule { get; set; }

    public int SortOrder { get; set; }

    public ICollection<MaintenanceEntry> Entries { get; set; } = new List<MaintenanceEntry>();
}

/// <summary>Durchgefuehrte Wartung. Setzt den Ausgangswert fuer die naechste Faelligkeit.</summary>
public class MaintenanceEntry : AuditableEntity
{
    public int VehicleId { get; set; }

    public Vehicle? Vehicle { get; set; }

    public int? MaintenanceRuleId { get; set; }

    public MaintenanceRule? Rule { get; set; }

    /// <summary>Bezeichnung, falls keine Regel hinterlegt ist.</summary>
    public string? Title { get; set; }

    public DateTime PerformedAt { get; set; }

    public int? Mileage { get; set; }

    /// <summary>Naechste Faelligkeit nach Datum (berechnet oder manuell gesetzt).</summary>
    public DateTime? NextDueDate { get; set; }

    /// <summary>Naechste Faelligkeit nach Kilometerstand.</summary>
    public int? NextDueMileage { get; set; }

    public string? PerformedBy { get; set; }

    public int? WorkshopId { get; set; }

    public Workshop? Workshop { get; set; }

    public int? WorkshopOrderId { get; set; }

    public WorkshopOrder? WorkshopOrder { get; set; }

    public decimal? Cost { get; set; }

    public string? Comment { get; set; }
}
