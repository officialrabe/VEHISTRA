using Fuhrpark.Domain.Common;
using Fuhrpark.Domain.Enums;

namespace Fuhrpark.Domain.Entities;

/// <summary>Interne Benachrichtigung im Notification Center.</summary>
public class Notification : EntityBase
{
    public NotificationCategory Category { get; set; } = NotificationCategory.Allgemein;

    public NotificationSeverity Severity { get; set; } = NotificationSeverity.Information;

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    /// <summary>Null = Benachrichtigung fuer alle Benutzer.</summary>
    public int? TargetUserId { get; set; }

    public User? TargetUser { get; set; }

    /// <summary>Null = Benachrichtigung fuer alle Rollen.</summary>
    public int? TargetRoleId { get; set; }

    public Role? TargetRole { get; set; }

    public int? VehicleId { get; set; }

    public Vehicle? Vehicle { get; set; }

    /// <summary>Verweis auf den ausloesenden Datensatz, z. B. "DamageReport:42".</summary>
    public string? SourceReference { get; set; }

    /// <summary>Eindeutiger Schluessel zur Vermeidung doppelter Benachrichtigungen.</summary>
    public string? DeduplicationKey { get; set; }

    public bool IsRead { get; set; }

    public DateTime? ReadAt { get; set; }

    public int? ReadByUserId { get; set; }

    public bool IsDismissed { get; set; }

    public DateTime? DismissedAt { get; set; }

    public DateTime? DueDate { get; set; }
}

/// <summary>
/// Unveraenderlicher Audit-Eintrag. Eintraege werden ausschliesslich angelegt und
/// koennen ueber die Anwendung nicht geaendert oder geloescht werden.
/// </summary>
public class AuditLog : EntityBase
{
    public DateTime Timestamp { get; set; }

    public int? UserId { get; set; }

    public string? UserName { get; set; }

    public string? ComputerName { get; set; }

    public AuditAction Action { get; set; }

    /// <summary>Name der betroffenen Entitaet, z. B. "Vehicle".</summary>
    public string EntityName { get; set; } = string.Empty;

    /// <summary>Primaerschluessel des betroffenen Datensatzes.</summary>
    public string? EntityId { get; set; }

    /// <summary>Lesbare Bezeichnung des Datensatzes, z. B. Kennzeichen.</summary>
    public string? EntityDisplay { get; set; }

    /// <summary>Geaenderte Felder als JSON (alte Werte).</summary>
    public string? OldValues { get; set; }

    /// <summary>Geaenderte Felder als JSON (neue Werte).</summary>
    public string? NewValues { get; set; }

    public string? AdditionalInfo { get; set; }

    public string? ApplicationVersion { get; set; }
}

/// <summary>Systemeinstellung als Schluessel-Wert-Paar.</summary>
public class SystemSetting : AuditableEntity
{
    public string Key { get; set; } = string.Empty;

    public string? Value { get; set; }

    public string? Description { get; set; }

    public string Category { get; set; } = "Allgemein";

    /// <summary>Datentyp fuer die Oberflaeche: string, int, bool, date, color, path, image.</summary>
    public string DataType { get; set; } = "string";

    /// <summary>Systemeinstellungen koennen nicht geloescht werden.</summary>
    public bool IsSystemSetting { get; set; }
}

/// <summary>Bekannte Anwendungsversionen inklusive Kompatibilitaetsangaben.</summary>
public class ApplicationVersion : EntityBase
{
    public string Version { get; set; } = string.Empty;

    public DateTime ReleasedAt { get; set; }

    public string? ReleaseNotes { get; set; }

    /// <summary>Mindestens erforderliche Datenbankschemaversion fuer diese Anwendungsversion.</summary>
    public string? MinimumDatabaseVersion { get; set; }

    public bool IsMandatory { get; set; }

    public DateTime RecordedAt { get; set; }
}

/// <summary>Aktuelle und historische Datenbankschemaversionen.</summary>
public class DatabaseVersion : EntityBase
{
    public string SchemaVersion { get; set; } = string.Empty;

    /// <summary>Name der zuletzt angewendeten EF-Core-Migration.</summary>
    public string? LastMigration { get; set; }

    public DateTime AppliedAt { get; set; }

    public string? AppliedByComputer { get; set; }

    public string? AppliedByUserName { get; set; }

    /// <summary>Anwendungsversion, die die Migration ausgefuehrt hat.</summary>
    public string? ApplicationVersion { get; set; }

    /// <summary>Aelteste Anwendungsversion, die mit diesem Schema noch arbeiten darf.</summary>
    public string? MinimumSupportedApplicationVersion { get; set; }

    public bool IsCurrent { get; set; }
}

/// <summary>
/// Zentrale Sperre, damit niemals zwei Clients gleichzeitig Datenbankmigrationen ausfuehren.
/// </summary>
public class MigrationLock : EntityBase
{
    /// <summary>Konstanter Schluessel; es existiert genau ein Datensatz.</summary>
    public string LockKey { get; set; } = "SCHEMA_MIGRATION";

    public bool IsLocked { get; set; }

    public DateTime? LockedAt { get; set; }

    public string? LockedByComputer { get; set; }

    public string? LockedByUserName { get; set; }

    public string? LockedByProcess { get; set; }

    /// <summary>Automatischer Ablauf, damit ein abgestuerzter Client die Sperre nicht dauerhaft haelt.</summary>
    public DateTime? ExpiresAt { get; set; }

    public string? Comment { get; set; }

    public byte[]? RowVersion { get; set; }

    public bool IsActive(DateTime now) => IsLocked && (ExpiresAt is null || ExpiresAt > now);
}

/// <summary>Protokoll aller durchgefuehrten Updates.</summary>
public class UpdateHistory : EntityBase
{
    public DateTime StartedAt { get; set; }

    public DateTime? FinishedAt { get; set; }

    public string? ComputerName { get; set; }

    public int? UserId { get; set; }

    public string? UserName { get; set; }

    public string? OldApplicationVersion { get; set; }

    public string? NewApplicationVersion { get; set; }

    public string? OldDatabaseVersion { get; set; }

    public string? NewDatabaseVersion { get; set; }

    public UpdateOutcome Outcome { get; set; } = UpdateOutcome.Erfolgreich;

    public string? BackupPath { get; set; }

    public string? ErrorMessage { get; set; }

    public string? Details { get; set; }
}

/// <summary>Protokoll der Datenbank- und Dokumentensicherungen.</summary>
public class BackupHistory : EntityBase
{
    public DateTime StartedAt { get; set; }

    public DateTime? FinishedAt { get; set; }

    public string? FilePath { get; set; }

    public long? FileSizeBytes { get; set; }

    public bool IsSuccessful { get; set; }

    public bool IsVerified { get; set; }

    public string? TriggeredByUserName { get; set; }

    public string? ComputerName { get; set; }

    /// <summary>Manuell, VorMigration oder Geplant.</summary>
    public string Kind { get; set; } = "Manuell";

    public string? ErrorMessage { get; set; }
}
