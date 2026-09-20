using System.Reflection;
using Vehistra.Application.Abstractions;
using Vehistra.Domain.Entities;
using Vehistra.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Vehistra.Infrastructure.Services;

/// <summary>
/// Verwaltet Datenbankverbindung und Schemamigrationen.
/// Vor einer Migration werden immer eine Sicherung erstellt und die zentrale Sperre gesetzt.
/// </summary>
public sealed class DatabaseAdministrationService : IDatabaseAdministrationService
{
    private readonly VehistraDbContext _db;
    private readonly IConnectionSettingsStore _connectionStore;
    private readonly IBackupService _backup;
    private readonly MigrationLockManager _lockManager;
    private readonly IClock _clock;
    private readonly ILogger<DatabaseAdministrationService> _logger;

    public DatabaseAdministrationService(
        VehistraDbContext db,
        IConnectionSettingsStore connectionStore,
        IBackupService backup,
        MigrationLockManager lockManager,
        IClock clock,
        ILogger<DatabaseAdministrationService> logger)
    {
        _db = db;
        _connectionStore = connectionStore;
        _backup = backup;
        _lockManager = lockManager;
        _clock = clock;
        _logger = logger;
    }

    public async Task<DatabaseConnectionResult> TestConnectionAsync(
        ServerConnectionSettings settings,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var connectionString = _connectionStore.BuildConnectionString(settings);

            if (settings.Provider == DatabaseProvider.Sqlite)
            {
                await using var datei = new SqliteConnection(connectionString);
                await datei.OpenAsync(cancellationToken).ConfigureAwait(false);

                await using var abfrage = datei.CreateCommand();
                abfrage.CommandText = "SELECT sqlite_version()";
                var sqliteVersion = await abfrage.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);

                return new DatabaseConnectionResult(
                    true,
                    "Die Datenbankdatei wurde geöffnet.",
                    $"SQLite {sqliteVersion} · {settings.DatabaseFile}");
            }

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT @@VERSION";
            var version = (string?)await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);

            var firstLine = version?.Split('\n').FirstOrDefault()?.Trim();

            return new DatabaseConnectionResult(
                true,
                $"Die Verbindung zu '{settings.Server}' wurde erfolgreich hergestellt.",
                firstLine);
        }
        catch (Exception exception)
        {
            var (message, hints) = SqlErrorTranslator.Translate(exception, settings.Server, settings.Database);
            _logger.LogWarning(exception, "Verbindungstest fehlgeschlagen: {Server}", settings.Server);

            return new DatabaseConnectionResult(false, message, exception.Message, hints);
        }
    }

    public async Task<bool> DatabaseExistsAsync(
        ServerConnectionSettings settings,
        CancellationToken cancellationToken = default)
    {
        if (settings.Provider == DatabaseProvider.Sqlite)
        {
            // Beim Solo-Platz ist die Datenbank eine Datei. "Vorhanden" heisst:
            // die Datei liegt da und ist nicht leer - eine frisch angelegte
            // Nulldatei zaehlt nicht als eingerichtete Datenbank.
            var datei = settings.DatabaseFile;

            return !string.IsNullOrWhiteSpace(datei)
                && File.Exists(datei)
                && new FileInfo(datei).Length > 0;
        }

        var masterSettings = settings.Clone();
        masterSettings.Database = "master";

        await using var connection = new SqlConnection(_connectionStore.BuildConnectionString(masterSettings));
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sys.databases WHERE name = @name";
        command.Parameters.Add(new SqlParameter("@name", settings.Database));

        var count = (int?)await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) ?? 0;
        return count > 0;
    }

    public async Task CreateDatabaseAsync(
        ServerConnectionSettings settings,
        CancellationToken cancellationToken = default)
    {
        if (await DatabaseExistsAsync(settings, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        if (settings.Provider == DatabaseProvider.Sqlite)
        {
            // Die Datei entsteht beim Verbinden; die Tabellen legt anschliessend
            // die Migration an. Hier wird nur sichergestellt, dass das
            // Verzeichnis vorhanden und beschreibbar ist.
            await using var datei = new SqliteConnection(_connectionStore.BuildConnectionString(settings));
            await datei.OpenAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Datenbankdatei angelegt: {Datei}", settings.DatabaseFile);
            return;
        }

        ValidateDatabaseName(settings.Database);

        var masterSettings = settings.Clone();
        masterSettings.Database = "master";

        await using var connection = new SqlConnection(_connectionStore.BuildConnectionString(masterSettings));
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE [{settings.Database.Replace("]", "]]")}]";
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Datenbank {Database} auf {Server} angelegt.", settings.Database, settings.Server);
    }

    public async Task<IReadOnlyList<string>> GetPendingMigrationsAsync(CancellationToken cancellationToken = default)
    {
        var pending = await _db.Database.GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false);
        return pending.ToList();
    }

    public async Task<IReadOnlyList<string>> GetAppliedMigrationsAsync(CancellationToken cancellationToken = default)
    {
        var applied = await _db.Database.GetAppliedMigrationsAsync(cancellationToken).ConfigureAwait(false);
        return applied.ToList();
    }

    public async Task<MigrationRunResult> MigrateAsync(
        MigrationOptions options,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var settings = _connectionStore.Load();
        var database = settings?.Database ?? "VehistraDB";

        // 1. Verbindung pruefen
        progress?.Report("Verbindung zur Datenbank wird geprueft ...");

        if (!await _db.Database.CanConnectAsync(cancellationToken).ConfigureAwait(false))
        {
            return new MigrationRunResult(false, [], null, null,
                "Die Verbindung zur Fuhrparkdatenbank konnte nicht hergestellt werden.");
        }

        // 2. Schema pruefen
        progress?.Report("Datenbankschema wird geprueft ...");
        var pending = await GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false);

        if (pending.Count == 0)
        {
            // Nichts zu tun - aber der Stand gehoert trotzdem vermerkt. Auf
            // einer frisch eingerichteten Datenbank lief nie eine Migration,
            // an die sich ein Vermerk haengen koennte; ohne diese Zeile stuende
            // in "Updates" und im Supportpaket dauerhaft "unbekannt".
            var currentVersion = await GetSchemaVersionAsync(cancellationToken).ConfigureAwait(false)
                ?? await RecordSchemaVersionAsync(options, cancellationToken).ConfigureAwait(false);

            progress?.Report("Das Datenbankschema ist bereits aktuell.");
            return new MigrationRunResult(true, [], null, currentVersion, null);
        }

        _logger.LogInformation("{Count} ausstehende Migration(en): {Migrations}", pending.Count, string.Join(", ", pending));

        // 3. Migrationssperre erwerben
        progress?.Report("Zentrale Migrationssperre wird gesetzt ...");

        await using var handle = await _lockManager
            .AcquireAsync(options.UserName, options.LockWaitTimeout, progress, cancellationToken)
            .ConfigureAwait(false);

        if (handle is null)
        {
            var state = await _lockManager.GetStateAsync(cancellationToken).ConfigureAwait(false);
            return new MigrationRunResult(false, [], null, null,
                "Die Fuhrparkdatenbank wird derzeit von einem anderen Arbeitsplatz aktualisiert " +
                $"({state.Computer ?? "unbekannt"}). Bitte spaeter erneut versuchen.");
        }

        // Nach dem Erwerb der Sperre erneut pruefen - ein anderer Client koennte bereits migriert haben.
        pending = await GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false);
        if (pending.Count == 0)
        {
            var currentVersion = await GetSchemaVersionAsync(cancellationToken).ConfigureAwait(false);
            return new MigrationRunResult(true, [], null, currentVersion, null);
        }

        // 4. Sicherung erstellen
        string? backupPath = null;

        if (options.CreateBackup)
        {
            progress?.Report("Sicherung der Datenbank wird erstellt ...");

            var backup = await _backup.CreateBackupAsync(new BackupRequest
            {
                Directory = options.BackupDirectory,
                Kind = "VorMigration",
                VerifyAfterBackup = true,
                Comment = $"Automatische Sicherung vor Migration auf {options.ApplicationVersion}"
            }, progress, cancellationToken).ConfigureAwait(false);

            backupPath = backup.FilePath;

            if (!backup.IsSuccessful && options.AbortWhenBackupFails)
            {
                _logger.LogError("Migration abgebrochen: Es konnte keine Sicherung erstellt werden.");

                return new MigrationRunResult(false, [], backupPath, null,
                    "Vor dem Datenbankupdate konnte kein Backup erstellt werden." + Environment.NewLine +
                    "Das Update wurde aus Sicherheitsgruenden abgebrochen." + Environment.NewLine + Environment.NewLine +
                    backup.Message);
            }

            // 5. Sicherung pruefen
            if (backup.IsSuccessful && !backup.IsVerified && options.AbortWhenBackupFails)
            {
                return new MigrationRunResult(false, [], backupPath, null,
                    "Die erstellte Sicherung konnte nicht geprueft werden." + Environment.NewLine +
                    "Das Update wurde aus Sicherheitsgruenden abgebrochen.");
            }
        }

        // 6. Migration ausfuehren
        try
        {
            progress?.Report($"{pending.Count} Datenbankaenderung(en) werden angewendet ...");
            await _db.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Die Datenbankmigration ist fehlgeschlagen.");

            var restoreHint = backupPath is null
                ? "Es wurde keine Sicherung erstellt. Bitte den Support kontaktieren."
                : BuildRestoreInstructions(database, backupPath);

            return new MigrationRunResult(false, [], backupPath, null,
                "Die Datenbankaktualisierung ist fehlgeschlagen." + Environment.NewLine + exception.Message,
                restoreHint);
        }

        // 7. Ergebnis pruefen und Version speichern
        progress?.Report("Ergebnis wird geprueft ...");

        var stillPending = await GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false);
        if (stillPending.Count > 0)
        {
            return new MigrationRunResult(false, pending, backupPath, null,
                "Nach der Aktualisierung sind weiterhin Datenbankaenderungen offen.",
                backupPath is null ? null : BuildRestoreInstructions(database, backupPath));
        }

        var schemaVersion = await RecordSchemaVersionAsync(options, cancellationToken).ConfigureAwait(false);
        progress?.Report("Die Datenbank wurde erfolgreich aktualisiert.");

        return new MigrationRunResult(true, pending, backupPath, schemaVersion, null);
    }

    public async Task<string?> EnsureSchemaVersionRecordedAsync(
        MigrationOptions options,
        CancellationToken cancellationToken = default)
    {
        var vermerkt = await GetSchemaVersionAsync(cancellationToken).ConfigureAwait(false);

        if (vermerkt is not null)
        {
            return vermerkt;
        }

        // Solange Aenderungen offen sind, entspricht das Schema nicht dieser
        // Programmversion - dann waere jeder Vermerk gelogen.
        var offen = await GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false);

        if (offen.Count > 0)
        {
            return null;
        }

        return await RecordSchemaVersionAsync(options, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string?> GetSchemaVersionAsync(CancellationToken cancellationToken)
    {
        return await _db.DatabaseVersions
            .AsNoTracking()
            .Where(v => v.IsCurrent)
            .OrderByDescending(v => v.AppliedAt)
            .Select(v => v.SchemaVersion)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<string> RecordSchemaVersionAsync(MigrationOptions options, CancellationToken cancellationToken)
    {
        var applied = await GetAppliedMigrationsAsync(cancellationToken).ConfigureAwait(false);
        var last = applied.LastOrDefault();

        var schemaVersion = options.ApplicationVersion
            ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)
            ?? "1.0.0";

        var previous = await _db.DatabaseVersions
            .Where(v => v.IsCurrent)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var entry in previous)
        {
            entry.IsCurrent = false;
        }

        _db.DatabaseVersions.Add(new DatabaseVersion
        {
            SchemaVersion = schemaVersion,
            LastMigration = last,
            AppliedAt = _clock.Now,
            AppliedByComputer = Environment.MachineName,
            AppliedByUserName = options.UserName ?? Environment.UserName,
            ApplicationVersion = options.ApplicationVersion,
            IsCurrent = true
        });

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return schemaVersion;
    }

    private static string BuildRestoreInstructions(string database, string backupPath) =>
        $"""
         WIEDERHERSTELLUNG DER DATENBANK

         Vor dem Update wurde folgende Sicherung erstellt:
         {backupPath}

         So stellen Sie den Zustand vor dem Update wieder her:

         1. Alle Arbeitsplaetze beenden das Vehistra.
         2. Auf dem Server das SQL Server Management Studio als Administrator starten.
         3. Eine neue Abfrage oeffnen und folgende Anweisungen ausfuehren:

            USE master;
            ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
            RESTORE DATABASE [{database}] FROM DISK = N'{backupPath}' WITH REPLACE;
            ALTER DATABASE [{database}] SET MULTI_USER;

         4. Anschliessend die zuvor installierte Programmversion wiederherstellen.
         5. Bitte den Support von LSP Virtual Services informieren: support@vehistra.dev

         Die ausfuehrliche Anleitung finden Sie in BACKUP-UND-WIEDERHERSTELLUNG.pdf.
         """;

    /// <summary>Verhindert die Uebernahme unzulaessiger Zeichen in CREATE DATABASE.</summary>
    private static void ValidateDatabaseName(string database)
    {
        if (string.IsNullOrWhiteSpace(database) || database.Length > 128)
        {
            throw new ArgumentException("Der Datenbankname ist ungueltig.", nameof(database));
        }

        if (!database.All(c => char.IsLetterOrDigit(c) || c is '_' or '-'))
        {
            throw new ArgumentException(
                "Der Datenbankname darf nur Buchstaben, Ziffern, Unterstrich und Bindestrich enthalten.",
                nameof(database));
        }
    }
}
