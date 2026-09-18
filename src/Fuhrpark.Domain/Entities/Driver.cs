using Fuhrpark.Domain.Common;

namespace Fuhrpark.Domain.Entities;

/// <summary>Fahrer des Fuhrparks.</summary>
public class Driver : AuditableEntity, IArchivable
{
    public string? PersonnelNumber { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string? Mobile { get; set; }

    public string? Email { get; set; }

    public bool IsActive { get; set; } = true;

    public string? Comment { get; set; }

    public bool IsArchived { get; set; }

    public DateTime? ArchivedAt { get; set; }

    public int? ArchivedByUserId { get; set; }

    public ICollection<VehicleDriverAssignment> VehicleAssignments { get; set; } = new List<VehicleDriverAssignment>();

    public string FullName => $"{FirstName} {LastName}".Trim();

    public string DisplayName => string.IsNullOrWhiteSpace(PersonnelNumber)
        ? FullName
        : $"{FullName} ({PersonnelNumber})";
}

/// <summary>
/// Historisierte Zuweisung eines festen Fahrers zu einem Fahrzeug.
/// Ein offener Datensatz (<see cref="ValidTo"/> = null) ist die aktuelle Zuweisung.
/// </summary>
public class VehicleDriverAssignment : EntityBase
{
    public int VehicleId { get; set; }

    public Vehicle? Vehicle { get; set; }

    public int DriverId { get; set; }

    public Driver? Driver { get; set; }

    public DateTime ValidFrom { get; set; }

    public DateTime? ValidTo { get; set; }

    public int? AssignedByUserId { get; set; }

    public string? AssignedByUserName { get; set; }

    public string? Comment { get; set; }

    public bool IsCurrent => ValidTo is null;
}
