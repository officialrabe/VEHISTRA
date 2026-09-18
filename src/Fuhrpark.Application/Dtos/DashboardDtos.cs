using Fuhrpark.Domain.Enums;

namespace Fuhrpark.Application.Dtos;

/// <summary>Alle Kennzahlen des Dashboards.</summary>
public sealed class DashboardData
{
    public DateTime GeneratedAt { get; init; }

    public VehicleCounters Vehicles { get; init; } = new();

    public InspectionCounters Inspections { get; init; } = new();

    public DamageCounters Damages { get; init; } = new();

    public WorkshopCounters Workshop { get; init; } = new();

    public LicensePlateCounters LicensePlates { get; init; } = new();

    public MaintenanceCounters Maintenance { get; init; } = new();

    public IReadOnlyList<AttentionItem> AttentionItems { get; init; } = [];
}

public sealed class VehicleCounters
{
    public int Total { get; init; }

    public int Active { get; init; }

    public int Available { get; init; }

    public int InUse { get; init; }

    public int WithoutDriver { get; init; }

    public int InWorkshop { get; init; }

    public int Damaged { get; init; }

    public int NotDriveable { get; init; }

    public int Deregistered { get; init; }

    public int Retired { get; init; }
}

public sealed class InspectionCounters
{
    public int Expired { get; init; }

    public int Within7Days { get; init; }

    public int Within14Days { get; init; }

    public int Within30Days { get; init; }

    public int Within60Days { get; init; }
}

public sealed class DamageCounters
{
    public int Open { get; init; }

    public int New { get; init; }

    public int Critical { get; init; }

    public int NotDriveable { get; init; }
}

public sealed class WorkshopCounters
{
    public int CurrentlyInWorkshop { get; init; }

    public int AppointmentsToday { get; init; }

    public int UpcomingAppointments { get; init; }

    public int Overdue { get; init; }
}

public sealed class LicensePlateCounters
{
    public int Available { get; init; }

    public int Reserved { get; init; }

    public int ReservationsExpiringSoon { get; init; }

    public int ReservationsExpired { get; init; }
}

public sealed class MaintenanceCounters
{
    public int ServiceDue { get; init; }

    public int ServiceDueSoon { get; init; }

    public int MileageIntervalReached { get; init; }
}

/// <summary>Eintrag im Bereich "Aufmerksamkeit erforderlich".</summary>
public sealed record AttentionItem(
    NotificationCategory Category,
    WarningLevel Level,
    string Text,
    int? VehicleId,
    string? Reference,
    DateTime? DueDate);
