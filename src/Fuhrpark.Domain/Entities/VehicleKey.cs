using Fuhrpark.Domain.Common;

namespace Fuhrpark.Domain.Entities;

/// <summary>Fahrzeugschluessel inklusive Ausgabe und Rueckgabe.</summary>
public class VehicleKey : AuditableEntity
{
    public int VehicleId { get; set; }

    public Vehicle? Vehicle { get; set; }

    public string KeyNumber { get; set; } = string.Empty;

    public int Count { get; set; } = 1;

    public string? StorageLocation { get; set; }

    public int? IssuedToDriverId { get; set; }

    public Driver? IssuedToDriver { get; set; }

    public string? IssuedToName { get; set; }

    public DateTime? IssuedAt { get; set; }

    public DateTime? ReturnedAt { get; set; }

    public string? Comment { get; set; }

    public bool IsIssued => IssuedAt is not null && ReturnedAt is null;
}
