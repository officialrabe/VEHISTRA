using Fuhrpark.Domain.Common;
using Fuhrpark.Domain.Enums;

namespace Fuhrpark.Domain.Entities;

/// <summary>
/// Historie der An- und Abmeldungen eines Fahrzeugs. Abmeldung und Ausmusterung sind getrennt:
/// ein abgemeldetes Fahrzeug kann wieder angemeldet werden.
/// </summary>
public class VehicleRegistration : AuditableEntity
{
    public int VehicleId { get; set; }

    public Vehicle? Vehicle { get; set; }

    public RegistrationEventType EventType { get; set; }

    public DateTime RegisteredAt { get; set; }

    public DateTime? DeregisteredAt { get; set; }

    public string? LicensePlate { get; set; }

    public string? NewLicensePlate { get; set; }

    public string? RegistrationOffice { get; set; }

    public string? Reason { get; set; }

    public string? Comment { get; set; }
}

/// <summary>Ausmusterung eines Fahrzeugs. Das Fahrzeug bleibt inklusive aller Historien erhalten.</summary>
public class VehicleRetirement : AuditableEntity
{
    public int VehicleId { get; set; }

    public Vehicle? Vehicle { get; set; }

    public DateTime RetiredAt { get; set; }

    public RetirementReason Reason { get; set; }

    public string? ReasonText { get; set; }

    public bool IsDeregistered { get; set; }

    public DateTime? DeregisteredAt { get; set; }

    public bool LicensePlateRemoved { get; set; }

    public int? LastMileage { get; set; }

    public bool IsSold { get; set; }

    public decimal? SalePrice { get; set; }

    public string? Buyer { get; set; }

    public bool IsScrapped { get; set; }

    public bool IsTotalLoss { get; set; }

    public bool IsLeasingReturn { get; set; }

    public bool IsSparePartDonor { get; set; }

    public string? Comment { get; set; }
}
