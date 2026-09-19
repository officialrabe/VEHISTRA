using Vehistra.Domain.Common;
using Vehistra.Domain.Enums;

namespace Vehistra.Domain.Entities;

/// <summary>Historisierter Kilometerstand. Der aktuelle Stand wird daraus abgeleitet.</summary>
public class MileageEntry : AuditableEntity
{
    public int VehicleId { get; set; }

    public Vehicle? Vehicle { get; set; }

    public int Mileage { get; set; }

    public DateTime RecordedAt { get; set; }

    public int? RecordedByUserId { get; set; }

    public string? RecordedByUserName { get; set; }

    public MileageSource Source { get; set; } = MileageSource.ManuelleEingabe;

    public string? Comment { get; set; }

    /// <summary>Kennzeichnet nachtraeglich korrigierte Eintraege (Korrekturen werden nicht geloescht).</summary>
    public bool IsCorrection { get; set; }
}
