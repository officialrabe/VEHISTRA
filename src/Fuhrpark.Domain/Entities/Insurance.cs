using Fuhrpark.Domain.Common;
using Fuhrpark.Domain.Enums;

namespace Fuhrpark.Domain.Entities;

/// <summary>Versicherungsvertrag eines Fahrzeugs.</summary>
public class VehicleInsurance : AuditableEntity
{
    public int VehicleId { get; set; }

    public Vehicle? Vehicle { get; set; }

    public string Company { get; set; } = string.Empty;

    /// <summary>Versicherungsscheinnummer.</summary>
    public string? PolicyNumber { get; set; }

    /// <summary>Vertragsnummer.</summary>
    public string? ContractNumber { get; set; }

    public InsuranceKind Kind { get; set; } = InsuranceKind.Haftpflicht;

    public DateTime? ValidFrom { get; set; }

    public DateTime? ValidTo { get; set; }

    public string? ContactPerson { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public decimal? AnnualPremium { get; set; }

    public decimal? DeductibleComprehensive { get; set; }

    public decimal? DeductiblePartial { get; set; }

    public bool IsActive { get; set; } = true;

    public string? Comment { get; set; }
}
