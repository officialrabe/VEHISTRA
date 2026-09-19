namespace Vehistra.Domain.Security;

/// <summary>
/// Vordefinierte Rollen. Weitere Rollen koennen von Administratoren angelegt werden.
/// </summary>
public static class RoleNames
{
    public const string Administrator = "ADMINISTRATOR";
    public const string Mitarbeiter = "MITARBEITER";
    public const string Disposition = "DISPOSITION";
    public const string Fuhrparkleitung = "FUHRPARKLEITUNG";
    public const string Werkstatt = "WERKSTATT";
    public const string Verwaltung = "VERWALTUNG";
    public const string Geschaeftsleitung = "GESCHAEFTSLEITUNG";

    /// <summary>Standardrollen mit Beschreibung und zugeordneten Berechtigungen.</summary>
    public static IReadOnlyList<RoleSeedDefinition> Defaults { get; } =
    [
        new(Administrator, "Administrator", "Vollzugriff auf alle Funktionen inklusive Administration.",
            Permissions.All.Select(p => p.Name).ToArray(), IsSystemRole: true),

        new(Mitarbeiter, "Mitarbeiter", "Lesender Zugriff sowie Melden von Schaeden und Kilometerstaenden.",
        [
            Permissions.VehicleView, Permissions.DriverView, Permissions.MileageEdit,
            Permissions.InspectionView, Permissions.MaintenanceView,
            Permissions.DamageView, Permissions.DamageCreate,
            Permissions.AccidentView, Permissions.AccidentCreate,
            Permissions.WorkshopView, Permissions.LicensePlateView,
            Permissions.DocumentView, Permissions.ReportsPrint
        ], IsSystemRole: true),

        new(Disposition, "Disposition", "Fahrzeug- und Fahrerzuordnung im Tagesgeschaeft.",
        [
            Permissions.VehicleView, Permissions.VehicleEdit,
            Permissions.DriverView, Permissions.DriverEdit, Permissions.DriverAssign,
            Permissions.MileageEdit, Permissions.InspectionView, Permissions.MaintenanceView,
            Permissions.DamageView, Permissions.DamageCreate, Permissions.DamageEdit,
            Permissions.AccidentView, Permissions.AccidentCreate,
            Permissions.WorkshopView, Permissions.LicensePlateView,
            Permissions.DocumentView, Permissions.ReportsPrint, Permissions.DataExport
        ], IsSystemRole: false),

        new(Fuhrparkleitung, "Fuhrparkleitung", "Vollstaendige fachliche Verwaltung des Fuhrparks.",
        [
            Permissions.VehicleView, Permissions.VehicleCreate, Permissions.VehicleEdit,
            Permissions.VehicleRetire, Permissions.VehicleRegistration,
            Permissions.DriverView, Permissions.DriverEdit, Permissions.DriverAssign,
            Permissions.MileageEdit,
            Permissions.InspectionView, Permissions.InspectionManage,
            Permissions.MaintenanceView, Permissions.MaintenanceManage,
            Permissions.DamageView, Permissions.DamageCreate, Permissions.DamageEdit, Permissions.DamageClose,
            Permissions.AccidentView, Permissions.AccidentCreate, Permissions.AccidentEdit,
            Permissions.WorkshopView, Permissions.WorkshopManage,
            Permissions.LicensePlateView, Permissions.LicensePlateManage,
            Permissions.InsuranceView, Permissions.InsuranceManage,
            Permissions.KeyView, Permissions.KeyManage,
            Permissions.DocumentView, Permissions.DocumentManage,
            Permissions.ReportsPrint, Permissions.DataImport, Permissions.DataExport,
            Permissions.AuditView
        ], IsSystemRole: false),

        new(Werkstatt, "Werkstatt", "Bearbeitung von Werkstattvorgaengen, Schaeden und Wartung.",
        [
            Permissions.VehicleView, Permissions.DriverView, Permissions.MileageEdit,
            Permissions.InspectionView, Permissions.InspectionManage,
            Permissions.MaintenanceView, Permissions.MaintenanceManage,
            Permissions.DamageView, Permissions.DamageCreate, Permissions.DamageEdit, Permissions.DamageClose,
            Permissions.AccidentView,
            Permissions.WorkshopView, Permissions.WorkshopManage,
            Permissions.KeyView, Permissions.KeyManage,
            Permissions.DocumentView, Permissions.DocumentManage,
            Permissions.ReportsPrint
        ], IsSystemRole: false),

        new(Verwaltung, "Verwaltung", "Kaufmaennische Verwaltung, Versicherungen, Kennzeichen und Dokumente.",
        [
            Permissions.VehicleView, Permissions.VehicleEdit, Permissions.VehicleRegistration,
            Permissions.DriverView, Permissions.DriverEdit,
            Permissions.InspectionView, Permissions.MaintenanceView,
            Permissions.DamageView, Permissions.AccidentView, Permissions.AccidentEdit,
            Permissions.WorkshopView,
            Permissions.LicensePlateView, Permissions.LicensePlateManage,
            Permissions.InsuranceView, Permissions.InsuranceManage,
            Permissions.DocumentView, Permissions.DocumentManage,
            Permissions.ReportsPrint, Permissions.DataImport, Permissions.DataExport
        ], IsSystemRole: false),

        new(Geschaeftsleitung, "Geschaeftsleitung", "Lesender Gesamtueberblick inklusive Auswertungen.",
        [
            Permissions.VehicleView, Permissions.DriverView,
            Permissions.InspectionView, Permissions.MaintenanceView,
            Permissions.DamageView, Permissions.AccidentView, Permissions.WorkshopView,
            Permissions.LicensePlateView, Permissions.InsuranceView, Permissions.KeyView,
            Permissions.DocumentView, Permissions.ReportsPrint,
            Permissions.DataExport, Permissions.AuditView
        ], IsSystemRole: false)
    ];
}

/// <summary>Definition einer Standardrolle fuer das initiale Befuellen der Datenbank.</summary>
public sealed record RoleSeedDefinition(
    string Name,
    string DisplayName,
    string Description,
    IReadOnlyList<string> Permissions,
    bool IsSystemRole);
