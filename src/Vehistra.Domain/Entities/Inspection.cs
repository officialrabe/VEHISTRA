using Vehistra.Domain.Common;
using Vehistra.Domain.Enums;

namespace Vehistra.Domain.Entities;

/// <summary>Hauptuntersuchung / Abgasuntersuchung und vergleichbare Pruefungen.</summary>
public class VehicleInspection : AuditableEntity
{
    public int VehicleId { get; set; }

    public Vehicle? Vehicle { get; set; }

    public InspectionType Type { get; set; } = InspectionType.HauptUndAbgasuntersuchung;

    /// <summary>Datum der durchgefuehrten Pruefung.</summary>
    public DateTime InspectionDate { get; set; }

    /// <summary>Faelligkeit der naechsten Pruefung.</summary>
    public DateTime NextDueDate { get; set; }

    /// <summary>Faelligkeit der naechsten Abgasuntersuchung, falls abweichend.</summary>
    public DateTime? NextEmissionDueDate { get; set; }

    public InspectionResult Result { get; set; } = InspectionResult.Bestanden;

    public string? TestCenter { get; set; }

    public string? Inspector { get; set; }

    public int? Mileage { get; set; }

    public decimal? Cost { get; set; }

    public string? Defects { get; set; }

    public string? Comment { get; set; }

    /// <summary>Verknuepftes Pruefprotokoll im Dokumentenarchiv.</summary>
    public int? DocumentId { get; set; }

    public VehicleDocument? Document { get; set; }
}
