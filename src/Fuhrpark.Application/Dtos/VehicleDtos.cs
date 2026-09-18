using Fuhrpark.Domain.Enums;

namespace Fuhrpark.Application.Dtos;

/// <summary>Zeile der Fahrzeuguebersicht.</summary>
public sealed class VehicleListItem
{
    public int Id { get; init; }

    public string InternalNumber { get; init; } = string.Empty;

    public string? LicensePlate { get; init; }

    public string Manufacturer { get; init; } = string.Empty;

    public string Model { get; init; } = string.Empty;

    public string? Categories { get; init; }

    public string StatusName { get; init; } = string.Empty;

    public VehicleStatusKind? StatusKind { get; init; }

    public string? StatusColorHex { get; init; }

    public string? DriverName { get; init; }

    public int CurrentMileage { get; init; }

    public DateTime? NextInspectionDue { get; init; }

    public WarningLevel InspectionWarning { get; init; }

    public int OpenDamageCount { get; init; }

    public bool HasCriticalDamage { get; init; }

    public string? WorkshopStatus { get; init; }

    public DateTime? NextServiceDue { get; init; }

    public int? NextServiceMileage { get; init; }

    public WarningLevel ServiceWarning { get; init; }

    public bool IsRetired { get; init; }

    public bool IsRegistered { get; init; }

    public string DisplayName => $"{(string.IsNullOrWhiteSpace(LicensePlate) ? InternalNumber : LicensePlate)} - {Manufacturer} {Model}".Trim();
}

/// <summary>Filter fuer die Fahrzeugliste.</summary>
public sealed class VehicleFilter
{
    public string? SearchText { get; set; }

    public int? StatusId { get; set; }

    public int? CategoryId { get; set; }

    public int? DriverId { get; set; }

    /// <summary>true = nur ausgemusterte, false = nur aktive, null = alle.</summary>
    public bool? IsRetired { get; set; } = false;

    public bool? IsRegistered { get; set; }

    public bool OnlyWithOpenDamages { get; set; }

    public bool OnlyInWorkshop { get; set; }

    public bool OnlyInspectionDue { get; set; }

    public int? InspectionDueWithinDays { get; set; }

    public string SortColumn { get; set; } = nameof(VehicleListItem.InternalNumber);

    public bool SortDescending { get; set; }

    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 100;
}

/// <summary>Kopfdaten der Fahrzeugakte.</summary>
public sealed class VehicleHeader
{
    public int Id { get; init; }

    public string InternalNumber { get; init; } = string.Empty;

    public string? LicensePlate { get; init; }

    public string Manufacturer { get; init; } = string.Empty;

    public string Model { get; init; } = string.Empty;

    public string? Variant { get; init; }

    public string StatusName { get; init; } = string.Empty;

    public string? StatusColorHex { get; init; }

    public string? DriverName { get; init; }

    public string? DriverPhone { get; init; }

    public int CurrentMileage { get; init; }

    public DateTime? NextInspectionDue { get; init; }

    public WarningLevel InspectionWarning { get; init; }

    public int OpenDamageCount { get; init; }

    public bool IsRetired { get; init; }
}

/// <summary>Eintrag der Fahrzeughistorie (Timeline).</summary>
public sealed record VehicleTimelineEntry(
    DateTime Timestamp,
    string Category,
    string Action,
    string? OldValue,
    string? NewValue,
    string? UserName,
    string? Reference);
