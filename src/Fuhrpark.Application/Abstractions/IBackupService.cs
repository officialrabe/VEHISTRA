namespace Fuhrpark.Application.Abstractions;

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
