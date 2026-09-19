using Vehistra.Domain.Enums;

namespace Vehistra.Application.Dtos;

/// <summary>Treffer der globalen Suche.</summary>
public sealed record SearchResultItem(
    string EntityType,
    int EntityId,
    string Title,
    string? Subtitle,
    string? MatchedField,
    int? VehicleId);

/// <summary>Ergebnis eines Anmeldeversuchs.</summary>
public sealed record LoginResult(
    bool IsSuccessful,
    CurrentUser? User,
    string? ErrorMessage,
    bool IsLockedOut,
    bool MustChangePassword,
    int? RemainingAttempts);

/// <summary>Faellige Wartung eines Fahrzeugs.</summary>
public sealed record MaintenanceDueItem(
    int VehicleId,
    string VehicleDisplay,
    int? MaintenanceRuleId,
    string RuleName,
    DateTime? DueDate,
    int? DueMileage,
    int? CurrentMileage,
    WarningLevel Level,
    string Description);

/// <summary>Kurzinformation zu einem Kennzeichen.</summary>
public sealed record LicensePlateListItem(
    int Id,
    string Plate,
    LicensePlateStatus Status,
    int? CurrentVehicleId,
    string? CurrentVehicleDisplay,
    DateTime? ReservedUntil,
    int? DaysUntilReservationExpires,
    WarningLevel ReservationWarning,
    string? Comment);

/// <summary>Zeile der Schadensuebersicht.</summary>
public sealed record DamageListItem(
    int Id,
    string DamageNumber,
    int VehicleId,
    string VehicleDisplay,
    string? DriverName,
    DateTime OccurredAt,
    string Description,
    string? CategoryName,
    DamagePriority Priority,
    DamageStatus Status,
    bool IsDriveable,
    decimal? CostActual,
    bool IsInsuranceCase);

/// <summary>Zeile der Unfalluebersicht.</summary>
public sealed record AccidentListItem(
    int Id,
    string AccidentNumber,
    int VehicleId,
    string VehicleDisplay,
    string? DriverName,
    DateTime OccurredAt,
    string? Location,
    AccidentType Type,
    bool PoliceInvolved,
    bool PersonalInjury,
    bool IsOpen);

/// <summary>Zeile der Werkstattuebersicht.</summary>
public sealed record WorkshopOrderListItem(
    int Id,
    string OrderNumber,
    int VehicleId,
    string VehicleDisplay,
    string? WorkshopName,
    string? DriverName,
    DateTime CreatedOn,
    DateTime? AppointmentDate,
    WorkshopOrderStatus Status,
    DateTime? CompletedAt,
    decimal? CostNet,
    string? InvoiceNumber,
    int? DaysInWorkshop);

/// <summary>Zeile der TUEV-Uebersicht.</summary>
public sealed record InspectionListItem(
    int VehicleId,
    string VehicleDisplay,
    string? LicensePlate,
    DateTime? LastInspection,
    DateTime? NextDue,
    int? DaysRemaining,
    WarningLevel Level,
    string? TestCenter,
    InspectionResult? LastResult);

/// <summary>Kurzinformation zu einem Dokument.</summary>
public sealed record DocumentListItem(
    int Id,
    int? VehicleId,
    string? VehicleDisplay,
    DocumentCategory Category,
    string Title,
    string OriginalFileName,
    long FileSizeBytes,
    DateTime? DocumentDate,
    DateTime CreatedAt,
    string? CreatedBy,
    string RelativePath);

/// <summary>Zeile des Audit-Logs.</summary>
public sealed record AuditLogListItem(
    int Id,
    DateTime Timestamp,
    string? UserName,
    string? ComputerName,
    AuditAction Action,
    string EntityName,
    string? EntityId,
    string? EntityDisplay,
    string? OldValues,
    string? NewValues,
    string? AdditionalInfo);

/// <summary>Filter fuer das Audit-Log.</summary>
public sealed class AuditFilter
{
    public DateTime? From { get; set; }

    public DateTime? To { get; set; }

    public string? UserName { get; set; }

    public string? EntityName { get; set; }

    public AuditAction? Action { get; set; }

    public string? SearchText { get; set; }

    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 100;
}

/// <summary>Benachrichtigung fuer das Notification Center.</summary>
public sealed record NotificationListItem(
    int Id,
    NotificationCategory Category,
    NotificationSeverity Severity,
    string Title,
    string Message,
    DateTime CreatedAt,
    DateTime? DueDate,
    int? VehicleId,
    bool IsRead);
