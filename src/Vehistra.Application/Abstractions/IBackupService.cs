namespace Vehistra.Application.Abstractions;

/// <summary>Erstellt und prueft Datenbanksicherungen ueber SQL Server.</summary>
public interface IBackupService
{
    Task<BackupResult> CreateBackupAsync(
        BackupRequest request,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>Prueft eine Sicherungsdatei mit RESTORE VERIFYONLY.</summary>
    Task<BackupResult> VerifyBackupAsync(string backupPath, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BackupInfo>> GetHistoryAsync(int maxCount = 50, CancellationToken cancellationToken = default);

    /// <summary>
    /// Entfernt Sicherungen, die aelter sind als die eingestellte
    /// Aufbewahrungsdauer. Ohne eingestellte Dauer wird nichts geloescht.
    /// </summary>
    Task<BackupCleanupResult> CleanUpAsync(
        BackupCleanupRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>Feste Grenzen des Aufraeumens - unabhaengig von der Einstellung.</summary>
public static class BackupRetention
{
    /// <summary>So viele Sicherungen bleiben immer liegen, egal wie alt sie sind.</summary>
    public const int MinimumKept = 3;

    /// <summary>Diese Art wird nie automatisch geloescht: sie entstand vor einer Migration.</summary>
    public const string NeverDeletedKind = "VorMigration";
}

public sealed class BackupCleanupRequest
{
    /// <summary>Verzeichnis; leer bedeutet das eingestellte Sicherungsverzeichnis.</summary>
    public string? Directory { get; set; }

    /// <summary>Nur anzeigen, was entfernt wuerde. Voreinstellung: ja.</summary>
    public bool PreviewOnly { get; set; } = true;

    /// <summary>Abweichende Aufbewahrungsdauer; leer bedeutet die Einstellung.</summary>
    public int? RetentionDays { get; set; }
}

/// <summary>Eine Sicherungsdatei, die die Aufbewahrungsdauer ueberschritten hat.</summary>
public sealed record BackupCleanupCandidate(
    string FilePath,
    DateTime CreatedAt,
    int AgeInDays,
    long SizeBytes,
    string Kind,
    bool IsDeleted,
    string? Problem = null);

public sealed record BackupCleanupResult(
    int RetentionDays,
    int KeptCount,
    bool WasExecuted,
    IReadOnlyList<BackupCleanupCandidate> Candidates,
    string Message)
{
    public long FreedBytes => Candidates.Where(c => c.IsDeleted).Sum(c => c.SizeBytes);

    /// <summary>Sicherungen, die entfernt werden koennten oder wurden.</summary>
    public int Count => Candidates.Count;
}

public sealed class BackupRequest
{
    /// <summary>Zielverzeichnis auf dem Datenbankserver.</summary>
    public string? Directory { get; set; }

    /// <summary>Manuell, VorMigration oder Geplant.</summary>
    public string Kind { get; set; } = "Manuell";

    public bool VerifyAfterBackup { get; set; } = true;

    public string? Comment { get; set; }
}

public sealed record BackupResult(
    bool IsSuccessful,
    string? FilePath,
    long? SizeBytes,
    bool IsVerified,
    string Message,
    string? TechnicalDetails = null);

public sealed record BackupInfo(
    DateTime StartedAt,
    DateTime? FinishedAt,
    string? FilePath,
    long? SizeBytes,
    bool IsSuccessful,
    bool IsVerified,
    string Kind,
    string? TriggeredBy,
    string? ErrorMessage);
