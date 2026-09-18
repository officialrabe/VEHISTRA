namespace Fuhrpark.Domain.Security;

/// <summary>
/// Zentrale Liste aller Berechtigungen. Die Werte werden in der Tabelle <c>Permissions</c>
/// gespeichert und sowohl in der Oberflaeche als auch in den Anwendungsdiensten geprueft.
/// </summary>
public static class Permissions
{
    public const string VehicleView = "Vehicle.View";
    public const string VehicleCreate = "Vehicle.Create";
    public const string VehicleEdit = "Vehicle.Edit";
    public const string VehicleRetire = "Vehicle.Retire";
    public const string VehicleRegistration = "Vehicle.Registration";

    public const string DriverView = "Driver.View";
    public const string DriverEdit = "Driver.Edit";
    public const string DriverAssign = "Driver.Assign";

    public const string MileageEdit = "Mileage.Edit";

    public const string InspectionView = "Inspection.View";
    public const string InspectionManage = "Inspection.Manage";

    public const string MaintenanceView = "Maintenance.View";
    public const string MaintenanceManage = "Maintenance.Manage";

    public const string DamageView = "Damage.View";
    public const string DamageCreate = "Damage.Create";
    public const string DamageEdit = "Damage.Edit";
    public const string DamageClose = "Damage.Close";

    public const string AccidentView = "Accident.View";
    public const string AccidentCreate = "Accident.Create";
    public const string AccidentEdit = "Accident.Edit";

    public const string WorkshopView = "Workshop.View";
    public const string WorkshopManage = "Workshop.Manage";

    public const string LicensePlateView = "LicensePlate.View";
    public const string LicensePlateManage = "LicensePlate.Manage";

    public const string InsuranceView = "Insurance.View";
    public const string InsuranceManage = "Insurance.Manage";

    public const string KeyView = "Key.View";
    public const string KeyManage = "Key.Manage";

    public const string DocumentView = "Document.View";
    public const string DocumentManage = "Document.Manage";

    public const string ReportsPrint = "Reports.Print";

    public const string DataImport = "Data.Import";
    public const string DataExport = "Data.Export";

    public const string UsersManage = "Users.Manage";
    public const string RolesManage = "Roles.Manage";
    public const string AuditView = "Audit.View";
    public const string BackupManage = "Backup.Manage";
    public const string SettingsManage = "Settings.Manage";
    public const string UpdatesManage = "Updates.Manage";

    /// <summary>Alle bekannten Berechtigungen mit Anzeigename und Gruppierung.</summary>
    public static IReadOnlyList<PermissionDefinition> All { get; } =
    [
        new(VehicleView, "Fahrzeuge anzeigen", "Fahrzeuge"),
        new(VehicleCreate, "Fahrzeuge anlegen", "Fahrzeuge"),
        new(VehicleEdit, "Fahrzeuge bearbeiten", "Fahrzeuge"),
        new(VehicleRetire, "Fahrzeuge ausmustern", "Fahrzeuge"),
        new(VehicleRegistration, "An- und Abmeldung verwalten", "Fahrzeuge"),

        new(DriverView, "Fahrer anzeigen", "Fahrer"),
        new(DriverEdit, "Fahrer bearbeiten", "Fahrer"),
        new(DriverAssign, "Feste Fahrer zuweisen", "Fahrer"),

        new(MileageEdit, "Kilometerstaende erfassen", "Fahrzeuge"),

        new(InspectionView, "TUEV/HU anzeigen", "Fristen"),
        new(InspectionManage, "TUEV/HU verwalten", "Fristen"),

        new(MaintenanceView, "Wartung anzeigen", "Wartung"),
        new(MaintenanceManage, "Wartung verwalten", "Wartung"),

        new(DamageView, "Schaeden anzeigen", "Schaeden"),
        new(DamageCreate, "Schaeden melden", "Schaeden"),
        new(DamageEdit, "Schaeden bearbeiten", "Schaeden"),
        new(DamageClose, "Schaeden abschliessen", "Schaeden"),

        new(AccidentView, "Unfaelle anzeigen", "Unfaelle"),
        new(AccidentCreate, "Unfaelle erfassen", "Unfaelle"),
        new(AccidentEdit, "Unfaelle bearbeiten", "Unfaelle"),

        new(WorkshopView, "Werkstattvorgaenge anzeigen", "Werkstatt"),
        new(WorkshopManage, "Werkstattvorgaenge verwalten", "Werkstatt"),

        new(LicensePlateView, "Kennzeichen anzeigen", "Kennzeichen"),
        new(LicensePlateManage, "Kennzeichen verwalten", "Kennzeichen"),

        new(InsuranceView, "Versicherungen anzeigen", "Versicherung"),
        new(InsuranceManage, "Versicherungen verwalten", "Versicherung"),

        new(KeyView, "Schluessel anzeigen", "Schluessel"),
        new(KeyManage, "Schluessel verwalten", "Schluessel"),

        new(DocumentView, "Dokumente anzeigen", "Dokumente"),
        new(DocumentManage, "Dokumente verwalten", "Dokumente"),

        new(ReportsPrint, "Berichte drucken", "Berichte"),

        new(DataImport, "Daten importieren", "Daten"),
        new(DataExport, "Daten exportieren", "Daten"),

        new(UsersManage, "Benutzer verwalten", "Administration"),
        new(RolesManage, "Rollen und Rechte verwalten", "Administration"),
        new(AuditView, "Audit-Log einsehen", "Administration"),
        new(BackupManage, "Backups verwalten", "Administration"),
        new(SettingsManage, "Einstellungen verwalten", "Administration"),
        new(UpdatesManage, "Updates installieren", "Administration")
    ];
}

/// <summary>Beschreibung einer Berechtigung fuer Stammdaten und Oberflaeche.</summary>
public sealed record PermissionDefinition(string Name, string DisplayName, string Group);
