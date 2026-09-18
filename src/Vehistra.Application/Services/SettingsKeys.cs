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

    public const string MaintenanceWarnDays = "Maintenance.WarnDays";
    public const string MaintenanceWarnKilometers = "Maintenance.WarnKilometers";

    public const string PlateReservationWarnDays = "LicensePlate.ReservationWarnDays";

    public const string DocumentsPath = "Paths.Documents";
    public const string BackupPath = "Paths.Backup";
    public const string UpdatePath = "Paths.Update";

    public const string DateFormat = "Display.DateFormat";
    public const string DateTimeFormat = "Display.DateTimeFormat";
    public const string Theme = "Display.Theme";

    public const string LoginMaxFailedAttempts = "Security.MaxFailedLoginAttempts";
    public const string LoginLockoutMinutes = "Security.LockoutMinutes";
    public const string PasswordMinimumLength = "Security.PasswordMinimumLength";

    public const string CheckForUpdatesOnStart = "Update.CheckOnStart";

    public const string WorkshopReportNotice = "Report.WorkshopNotice";
    public const string AccidentReportNotice = "Report.AccidentNotice";

    public const string DatabaseSchemaVersion = "Database.SchemaVersion";
    public const string MinimumSupportedApplicationVersion = "Database.MinimumSupportedApplicationVersion";
}
