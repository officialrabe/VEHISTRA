using Vehistra.Domain.Common;
using Vehistra.Domain.Enums;

namespace Vehistra.Domain.Entities;

/// <summary>Unfall- bzw. Schadensereignis mit allen Angaben des Unfallberichts.</summary>
public class AccidentReport : AuditableEntity
{
    /// <summary>Fortlaufende Unfallnummer, z. B. UNF-2026-00007.</summary>
    public string AccidentNumber { get; set; } = string.Empty;

    public int VehicleId { get; set; }

    public Vehicle? Vehicle { get; set; }

    public int? DriverId { get; set; }

    public Driver? Driver { get; set; }

    /// <summary>Telefonnummer des Fahrers zum Unfallzeitpunkt (fuer den Bericht festgehalten).</summary>
    public string? DriverPhone { get; set; }

    public DateTime OccurredAt { get; set; }

    public string? Location { get; set; }

    public string? Street { get; set; }

    public string? PostalCode { get; set; }

    public string? City { get; set; }

    public int? Mileage { get; set; }

    public AccidentType Type { get; set; } = AccidentType.UnfallMitFremdbeteiligung;

    public string? TypeOther { get; set; }

    /// <summary>Unfallhergang in eigenen Worten.</summary>
    public string? CourseOfEvents { get; set; }

    public bool ThirdPartyInvolved { get; set; }

    public bool PersonalInjury { get; set; }

    public bool PoliceInvolved { get; set; }

    public string? PoliceStation { get; set; }

    public string? PoliceFileNumber { get; set; }

    public bool VehicleDriveable { get; set; } = true;

    public int? VehicleInsuranceId { get; set; }

    public VehicleInsurance? VehicleInsurance { get; set; }

    public string? OwnInsuranceClaimNumber { get; set; }

    /// <summary>Sichtbarer Hinweis, dass kein Schuldanerkenntnis abgegeben wurde.</summary>
    public string? Comment { get; set; }

    public DateTime? ClosedAt { get; set; }

    public int? ClosedByUserId { get; set; }

    public ICollection<AccidentParticipant> Participants { get; set; } = new List<AccidentParticipant>();

    public ICollection<AccidentWitness> Witnesses { get; set; } = new List<AccidentWitness>();

    public ICollection<AccidentAttachment> Attachments { get; set; } = new List<AccidentAttachment>();

    public ICollection<DamageReport> Damages { get; set; } = new List<DamageReport>();

    public bool IsOpen => ClosedAt is null;
}

/// <summary>Beteiligter Dritter eines Unfalls.</summary>
public class AccidentParticipant : AuditableEntity
{
    public int AccidentReportId { get; set; }

    public AccidentReport? AccidentReport { get; set; }

    public string? LicensePlate { get; set; }

    public string? LastName { get; set; }

    public string? FirstName { get; set; }

    public string? Phone { get; set; }

    public string? Street { get; set; }

    public string? PostalCode { get; set; }

    public string? City { get; set; }

    public string? InsuranceCompany { get; set; }

    public string? InsuranceNumber { get; set; }

    public string? VehicleDescription { get; set; }

    public string? Comment { get; set; }

    public string FullName => $"{FirstName} {LastName}".Trim();
}

/// <summary>Zeuge eines Unfalls.</summary>
public class AccidentWitness : AuditableEntity
{
    public int AccidentReportId { get; set; }

    public AccidentReport? AccidentReport { get; set; }

    public string? LastName { get; set; }

    public string? FirstName { get; set; }

    public string? Phone { get; set; }

    public string? Address { get; set; }

    public string? Comment { get; set; }

    public string FullName => $"{FirstName} {LastName}".Trim();
}

/// <summary>Foto, Gutachten oder Dokument zu einem Unfall.</summary>
public class AccidentAttachment : EntityBase
{
    public int AccidentReportId { get; set; }

    public AccidentReport? AccidentReport { get; set; }

    public int VehicleDocumentId { get; set; }

    public VehicleDocument? Document { get; set; }

    public DateTime LinkedAt { get; set; }

    public int? LinkedByUserId { get; set; }

    public string? Caption { get; set; }
}
