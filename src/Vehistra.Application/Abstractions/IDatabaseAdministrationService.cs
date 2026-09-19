namespace Vehistra.Application.Abstractions;

/// <summary>Verwaltungsaufgaben rund um die Datenbank (Migration, Backup, Diagnose).</summary>
public interface IDatabaseAdministrationService
{
    Task<DatabaseConnectionResult> TestConnectionAsync(
        ServerConnectionSettings settings,
        CancellationToken cancellationToken = default);

    Task<bool> DatabaseExistsAsync(ServerConnectionSettings settings, CancellationToken cancellationToken = default);

    Task CreateDatabaseAsync(ServerConnectionSettings settings, CancellationToken cancellationToken = default);

    /// <summary>Liste der noch nicht angewendeten EF-Core-Migrationen.</summary>
    Task<IReadOnlyList<string>> GetPendingMigrationsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetAppliedMigrationsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Fuehrt ausstehende Migrationen aus. Erstellt zuvor ein Backup und setzt die zentrale
    /// Migrationssperre. Schlaegt das Backup fehl, wird die Migration abgebrochen.
    /// </summary>
    Task<MigrationRunResult> MigrateAsync(
        MigrationOptions options,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default);
}

/// <summary>Ergebnis eines Verbindungstests mit verstaendlicher Fehlermeldung.</summary>
public sealed record DatabaseConnectionResult(
    bool IsSuccessful,
    string Message,
    string? TechnicalDetails = null,
    IReadOnlyList<string>? Hints = null)
{
    public static DatabaseConnectionResult Success(string message) => new(true, message);
}

/// <summary>Optionen fuer den Migrationslauf.</summary>
public sealed class MigrationOptions
{
    /// <summary>Vor der Migration ein Backup erstellen (Pflicht im Produktivbetrieb).</summary>
    public bool CreateBackup { get; set; } = true;

    /// <summary>Migration abbrechen, wenn kein Backup erstellt werden konnte.</summary>
    public bool AbortWhenBackupFails { get; set; } = true;

    public string? BackupDirectory { get; set; }

    public string? ApplicationVersion { get; set; }

    public string? UserName { get; set; }

    /// <summary>Wartezeit auf eine fremde Migrationssperre.</summary>
    public TimeSpan LockWaitTimeout { get; set; } = TimeSpan.FromMinutes(10);
}

/// <summary>Ergebnis eines Migrationslaufs.</summary>
public sealed record MigrationRunResult(
    bool IsSuccessful,
    IReadOnlyList<string> AppliedMigrations,
    string? BackupPath,
    string? SchemaVersion,
    string? ErrorMessage,
    string? RestoreInstructions = null);
