namespace Vehistra.Application.Services;

/// <summary>Schluessel aller Systemeinstellungen.</summary>
public static class SettingsKeys
{
    public const string CompanyName = "Company.Name";
    public const string CompanyStreet = "Company.Street";
    public const string CompanyPostalCode = "Company.PostalCode";
    public const string CompanyCity = "Company.City";
    public const string CompanyPhone = "Company.Phone";
    public const string CompanyEmail = "Company.Email";
    public const string CompanyLogoPath = "Company.LogoPath";

    public const string InspectionWarnCriticalDays = "Inspection.WarnCriticalDays";
    public const string InspectionWarnWarningDays = "Inspection.WarnWarningDays";

    /// <summary>TUEV: kritisch bereits X Tage vor dem Termin, nicht erst danach.</summary>
    public const string InspectionWarnUrgentDays = "Inspection.WarnUrgentDays";

    public const string MaintenanceWarnDays = "Maintenance.WarnDays";
    public const string MaintenanceWarnKilometers = "Maintenance.WarnKilometers";

    public const string PlateReservationWarnDays = "LicensePlate.ReservationWarnDays";

    /// <summary>Werkstatt: Warnung, wenn ein Fahrzeug laenger als X Tage dort steht.</summary>
    public const string WorkshopLongStayWarnDays = "Workshop.LongStayWarnDays";

    /// <summary>
    /// Groesste erlaubte Dokumentgroesse in Megabyte. Die Ablage selbst begrenzt
    /// zusaetzlich hart auf 50 MB; ein hoeherer Wert hier hebt das nicht auf.
    /// </summary>
    public const string DocumentMaxFileSizeMb = "Documents.MaxFileSizeMb";

    public const string DocumentsPath = "Paths.Documents";
    public const string BackupPath = "Paths.Backup";

    /// <summary>
    /// Aufbewahrungsdauer der Sicherungen in Tagen. 0 bedeutet: nichts wird
    /// geloescht. Bewusst ausgeschaltet, denn Loeschen laesst sich nicht
    /// zurueckholen.
    /// </summary>
    public const string BackupRetentionDays = "Backup.RetentionDays";
    public const string UpdatePath = "Paths.Update";

    public const string DateFormat = "Display.DateFormat";
    public const string DateTimeFormat = "Display.DateTimeFormat";
    public const string Theme = "Display.Theme";

    public const string LoginMaxFailedAttempts = "Security.MaxFailedLoginAttempts";
    public const string LoginLockoutMinutes = "Security.LockoutMinutes";
    public const string PasswordMinimumLength = "Security.PasswordMinimumLength";

    public const string CheckForUpdatesOnStart = "Update.CheckOnStart";

    /// <summary>
    /// Woher Updates kommen: "Ablage" (Voreinstellung, Freigabe im Firmennetz)
    /// oder "GitHub" (Veroeffentlichungen des Projekts, braucht Internet).
    /// </summary>
    public const string UpdateSource = "Update.Source";

    /// <summary>Repository der Veroeffentlichungen, Form "besitzer/name".</summary>
    public const string UpdateRepository = "Update.Repository";

    public const string WorkshopReportNotice = "Report.WorkshopNotice";
    public const string AccidentReportNotice = "Report.AccidentNotice";

    public const string DatabaseSchemaVersion = "Database.SchemaVersion";
    public const string MinimumSupportedApplicationVersion = "Database.MinimumSupportedApplicationVersion";
}
