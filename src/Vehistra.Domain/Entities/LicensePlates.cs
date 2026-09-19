using Vehistra.Domain.Common;
using Vehistra.Domain.Enums;

namespace Vehistra.Domain.Entities;

/// <summary>Kennzeichen als eigenstaendiges Verwaltungsobjekt.</summary>
public class LicensePlate : AuditableEntity
{
    /// <summary>Kennzeichen in normierter Schreibweise, z. B. "FDS-AB 123".</summary>
    public string Plate { get; set; } = string.Empty;

    public LicensePlateStatus Status { get; set; } = LicensePlateStatus.Verfuegbar;

    /// <summary>Aktuell zugeordnetes Fahrzeug (denormalisiert; Historie in LicensePlateAssignments).</summary>
    public int? CurrentVehicleId { get; set; }

    public Vehicle? CurrentVehicle { get; set; }

    public string? RegistrationOffice { get; set; }

    public string? Comment { get; set; }

    public bool IsSeasonPlate { get; set; }

    public string? SeasonFrom { get; set; }

    public string? SeasonTo { get; set; }

    public ICollection<LicensePlateAssignment> Assignments { get; set; } = new List<LicensePlateAssignment>();

    public ICollection<LicensePlateReservation> Reservations { get; set; } = new List<LicensePlateReservation>();
}

/// <summary>Historisierte Zuordnung eines Kennzeichens zu einem Fahrzeug. Wird nie ueberschrieben.</summary>
public class LicensePlateAssignment : EntityBase
{
    public int LicensePlateId { get; set; }

    public LicensePlate? LicensePlate { get; set; }

    public int VehicleId { get; set; }

    public Vehicle? Vehicle { get; set; }

    public DateTime ValidFrom { get; set; }

    public DateTime? ValidTo { get; set; }

    public string? Reason { get; set; }

    public int? AssignedByUserId { get; set; }

    public string? AssignedByUserName { get; set; }

    public string? Comment { get; set; }

    public bool IsCurrent => ValidTo is null;
}

/// <summary>Reservierung eines Kennzeichens bei der Zulassungsstelle.</summary>
public class LicensePlateReservation : AuditableEntity
{
    public int LicensePlateId { get; set; }

    public LicensePlate? LicensePlate { get; set; }

    public DateTime ReservedAt { get; set; }

    public DateTime ReservedUntil { get; set; }

    public string? RegistrationOffice { get; set; }

    public string? ReservationNumber { get; set; }

    /// <summary>
    /// Optionale PIN der Zulassungsstelle. Wird verschluesselt gespeichert und niemals protokolliert.
    /// </summary>
    public string? PinEncrypted { get; set; }

    public bool IsReleased { get; set; }

    public DateTime? ReleasedAt { get; set; }

    public string? Comment { get; set; }

    public bool IsExpired(DateTime now) => !IsReleased && ReservedUntil.Date < now.Date;

    public int DaysRemaining(DateTime now) => (ReservedUntil.Date - now.Date).Days;
}
