namespace Fuhrpark.Application.Abstractions;

/// <summary>
/// Erzeugt die PDF-Berichte. Alle Berichte sind vektorbasiert und fuer DIN A4 ausgelegt -
/// es werden keine Bildschirmabbilder gedruckt.
/// </summary>
public interface IReportService
{
    /// <summary>Werkstattbericht (ein Blatt DIN A4).</summary>
    Task<byte[]> CreateWorkshopReportAsync(WorkshopReportData data, CancellationToken cancellationToken = default);

    /// <summary>Unfallbericht (zwei Blaetter DIN A4 inklusive Unfallskizze).</summary>
    Task<byte[]> CreateAccidentReportAsync(AccidentReportData data, CancellationToken cancellationToken = default);

    /// <summary>Fahrzeugakte als PDF.</summary>
    Task<byte[]> CreateVehicleFileReportAsync(VehicleFileReportData data, CancellationToken cancellationToken = default);

    /// <summary>Beliebige Liste als PDF im Querformat.</summary>
    Task<byte[]> CreateTableReportAsync(TableReportData data, CancellationToken cancellationToken = default);
}

/// <summary>Kopfdaten, die in jedem Bericht erscheinen.</summary>
public sealed class ReportHeaderData
{
    public string CompanyName { get; set; } = string.Empty;

    public string? CompanyAddress { get; set; }

    public string? LogoPath { get; set; }

    public string ApplicationTitle { get; set; } = "FUHRPARKMANAGEMENT";

    public DateTime PrintedAt { get; set; } = DateTime.Now;

    public string? PrintedBy { get; set; }

    public string ApplicationVersion { get; set; } = "1.0.0";
}

/// <summary>Daten des Werkstattberichts. Ohne Werte entsteht ein Blankoformular.</summary>
public sealed class WorkshopReportData
{
    public ReportHeaderData Header { get; set; } = new();

    public bool IsBlankForm { get; set; }

    public string? LicensePlate { get; set; }

    public string? InternalNumber { get; set; }

    public string? VehicleDescription { get; set; }

    public string? DriverName { get; set; }

    public string? DriverPhone { get; set; }

    public DateTime? Date { get; set; }

    public DateTime? ProposedAppointment { get; set; }

    public string? WorkshopName { get; set; }

    public int? Mileage { get; set; }

    public string? OrderNumber { get; set; }

    /// <summary>Angekreuzte Standardarbeiten (Schluessel aus <see cref="WorkshopStandardTasks"/>).</summary>
    public ISet<string> CheckedStandardTasks { get; set; } = new HashSet<string>();

    /// <summary>Vorausgefuellte Zeilen im Bereich "Schaeden / auszufuehrende Arbeiten".</summary>
    public IReadOnlyList<WorkshopReportTaskLine> TaskLines { get; set; } = [];

    public string? Notice { get; set; }
}

/// <summary>Eine Zeile der Arbeitsliste im Werkstattbericht.</summary>
public sealed record WorkshopReportTaskLine(int Position, string Description, bool IsCompleted);

/// <summary>Die Standardarbeiten der Checkbox-Liste des Werkstattberichts.</summary>
public static class WorkshopStandardTasks
{
    public const string OilChange = "OELWECHSEL";
    public const string Inspection = "INSPEKTION";
    public const string TyreChange = "REIFENWECHSEL";
    public const string TyreCheck = "REIFEN_PRUEFEN";
    public const string Brakes = "BREMSEN";
    public const string Hu = "HU_AU";
    public const string Lights = "BELEUCHTUNG";
    public const string Battery = "BATTERIE";
    public const string Wipers = "SCHEIBENWISCHER";
    public const string AirConditioning = "KLIMASERVICE";
    public const string Exhaust = "AUSPUFF";
    public const string ErrorMemory = "FEHLERSPEICHER";

    /// <summary>Reihenfolge und Beschriftung der Checkboxen.</summary>
    public static IReadOnlyList<(string Key, string Label)> All { get; } =
    [
        (OilChange, "Ölwechsel / Service"),
        (Inspection, "Inspektion"),
        (TyreChange, "Reifenwechsel"),
        (TyreCheck, "Reifen prüfen / Luftdruck"),
        (Brakes, "Bremsen prüfen"),
        (Hu, "HU / AU (TÜV)"),
        (Lights, "Beleuchtung"),
        (Battery, "Batterie"),
        (Wipers, "Scheibenwischer"),
        (AirConditioning, "Klimaservice"),
        (Exhaust, "Auspuff"),
        (ErrorMemory, "Fehlerspeicher auslesen")
    ];
}

/// <summary>Daten des zweiseitigen Unfallberichts.</summary>
public sealed class AccidentReportData
{
    public ReportHeaderData Header { get; set; } = new();

    public bool IsBlankForm { get; set; }

    public string? AccidentNumber { get; set; }

    public string? LicensePlate { get; set; }

    public string? InternalNumber { get; set; }

    public string? VehicleDescription { get; set; }

    public string? DriverName { get; set; }

    public string? DriverPhone { get; set; }

    public int? Mileage { get; set; }

    public DateTime? OccurredAt { get; set; }

    public string? Location { get; set; }

    /// <summary>Angekreuzte Schadensart.</summary>
    public string? DamageKindKey { get; set; }

    public string? CourseOfEvents { get; set; }

    public AccidentReportParticipant? Participant { get; set; }

    public bool? PoliceInvolved { get; set; }

    public bool? PersonalInjury { get; set; }

    public bool? VehicleDriveable { get; set; }

    public string? PoliceStation { get; set; }

    public string? PoliceFileNumber { get; set; }

    public string? WitnessName { get; set; }

    public string? WitnessPhone { get; set; }

    public string? Notice { get; set; }
}

/// <summary>Beteiligter Dritter im Unfallbericht.</summary>
public sealed class AccidentReportParticipant
{
    public string? LicensePlate { get; set; }

    public string? Name { get; set; }

    public string? Phone { get; set; }

    public string? Address { get; set; }

    public string? InsuranceCompany { get; set; }

    public string? InsuranceNumber { get; set; }
}

/// <summary>Daten der Fahrzeugakte.</summary>
public sealed class VehicleFileReportData
{
    public ReportHeaderData Header { get; set; } = new();

    public string VehicleDisplay { get; set; } = string.Empty;

    public IReadOnlyList<(string Label, string? Value)> MasterData { get; set; } = [];

    public IReadOnlyList<(string Title, IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyList<string?>> Rows)> Sections { get; set; } = [];
}

/// <summary>Beliebige Tabelle als PDF.</summary>
public sealed class TableReportData
{
    public ReportHeaderData Header { get; set; } = new();

    public string Title { get; set; } = string.Empty;

    public string? Subtitle { get; set; }

    public IReadOnlyList<string> Columns { get; set; } = [];

    public IReadOnlyList<IReadOnlyList<string?>> Rows { get; set; } = [];

    public bool Landscape { get; set; } = true;
}
