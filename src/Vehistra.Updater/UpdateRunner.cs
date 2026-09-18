using System.Diagnostics;
using System.IO.Compression;
using Vehistra.Application.Abstractions;
using Vehistra.Domain.Entities;
using Vehistra.Domain.Enums;
using Vehistra.Infrastructure;
using Vehistra.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Vehistra.Updater;

/// <summary>
/// Fuehrt das Update durch: Programmdateien sichern, Installer ausfuehren,
/// Datenbank sichern und migrieren, Ergebnis protokollieren. Bei Fehlern wird
/// der vorherige Programmstand wiederhergestellt.
/// </summary>
public sealed class UpdateRunner
{
    private readonly IServiceProvider _services;
    private readonly ILogger<UpdateRunner> _logger;

    public UpdateRunner(IServiceProvider services, ILogger<UpdateRunner> logger)
    {
        _services = services;
        _logger = logger;
    }

    public async Task<UpdateRunResult> RunAsync(
        UpdateOptions options,
        IProgress<UpdateProgress> progress,
        CancellationToken cancellationToken = default)
    {
        var history = new UpdateHistory
        {
            StartedAt = DateTime.Now,
            ComputerName = Environment.MachineName,
            UserName = options.UserName ?? Environment.UserName,
            OldApplicationVersion = ReadInstalledVersion(options.ApplicationDirectory),
            NewApplicationVersion = options.TargetVersion
        };

        string? programBackup = null;

        try
        {
            // 1. Auf das Ende der Hauptanwendung warten
            progress.Report(new UpdateProgress(1, "Es wird gewartet, bis das Vehistra beendet ist ..."));
            await WaitForApplicationExitAsync(options.WaitForProcessId, cancellationToken).ConfigureAwait(false);

            // 2. Programmdateien sichern (Rollback-Grundlage)
            progress.Report(new UpdateProgress(2, "Die bisherige Programmversion wird gesichert ..."));
            programBackup = BackupProgramFiles(options.ApplicationDirectory);
            history.BackupPath = programBackup;

            // 3. Installer ausfuehren
            progress.Report(new UpdateProgress(3, "Die neuen Programmdateien werden installiert ..."));
            var exitCode = await RunInstallerAsync(options, cancellationToken).ConfigureAwait(false);

            if (exitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Der Update-Installer wurde mit dem Rückgabewert {exitCode} beendet.");
            }

            // 4. Datenbank pruefen und migrieren
            progress.Report(new UpdateProgress(4, "Die Fuhrparkdatenbank wird geprüft ..."));

            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<VehistraDbContext>();
            var administration = scope.ServiceProvider.GetRequiredService<IDatabaseAdministrationService>();
            var connectionStore = scope.ServiceProvider.GetRequiredService<IConnectionSettingsStore>();

            history.OldDatabaseVersion = await ReadSchemaVersionAsync(db, cancellationToken).ConfigureAwait(false);

            var migrationProgress = new Progress<string>(text =>
                progress.Report(new UpdateProgress(5, text)));

            var result = await administration.MigrateAsync(new MigrationOptions
            {
                CreateBackup = options.CreateBackupBeforeMigration,
                AbortWhenBackupFails = true,
                BackupDirectory = connectionStore.Load()?.BackupPath,
                ApplicationVersion = options.TargetVersion,
                UserName = options.UserName
            }, migrationProgress, cancellationToken).ConfigureAwait(false);

            if (!result.IsSuccessful)
            {
                history.Outcome = UpdateOutcome.Fehlgeschlagen;
                history.ErrorMessage = result.ErrorMessage;
                history.FinishedAt = DateTime.Now;

                await TryWriteHistoryAsync(history, cancellationToken).ConfigureAwait(false);

                // Programmdateien zuruecksetzen, damit kein beschaedigter Zustand zurueckbleibt.
                progress.Report(new UpdateProgress(6, "Die vorherige Programmversion wird wiederhergestellt ..."));
                RestoreProgramFiles(programBackup, options.ApplicationDirectory);

                return new UpdateRunResult(false, result.ErrorMessage ?? "Das Datenbankupdate ist fehlgeschlagen.",
                    result.RestoreInstructions, result.BackupPath);
            }

            history.NewDatabaseVersion = result.SchemaVersion;
            history.Outcome = UpdateOutcome.Erfolgreich;
            history.FinishedAt = DateTime.Now;
            history.Details = result.AppliedMigrations.Count == 0
                ? "Keine Datenbankänderungen erforderlich."
                : $"Angewendete Migrationen: {string.Join(", ", result.AppliedMigrations)}";

            await TryWriteHistoryAsync(history, cancellationToken).ConfigureAwait(false);
            await RegisterApplicationVersionAsync(db, options.TargetVersion, cancellationToken).ConfigureAwait(false);

            progress.Report(new UpdateProgress(6, "Das Update wurde erfolgreich abgeschlossen."));

            return new UpdateRunResult(true,
                $"Die Version {options.TargetVersion} wurde erfolgreich installiert.", null, result.BackupPath);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Das Update ist fehlgeschlagen.");

            history.Outcome = UpdateOutcome.Fehlgeschlagen;
            history.ErrorMessage = exception.Message;
            history.FinishedAt = DateTime.Now;

            await TryWriteHistoryAsync(history, cancellationToken).ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(programBackup))
            {
                progress.Report(new UpdateProgress(6, "Die vorherige Programmversion wird wiederhergestellt ..."));

                try
                {
                    RestoreProgramFiles(programBackup, options.ApplicationDirectory);
                    history.Outcome = UpdateOutcome.RollbackDurchgefuehrt;
                }
                catch (Exception restoreException)
                {
                    _logger.LogError(restoreException, "Die Wiederherstellung der Programmdateien ist fehlgeschlagen.");
                }
            }

            return new UpdateRunResult(false, exception.Message,
                BuildManualRestoreHint(programBackup), null);
        }
    }

    /// <summary>Startet die Hauptanwendung nach erfolgreichem Update.</summary>
    public void StartApplication(UpdateOptions options)
    {
        if (!File.Exists(options.MainExecutable))
        {
            _logger.LogWarning("Die Hauptanwendung wurde nicht gefunden: {Path}", options.MainExecutable);
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = options.MainExecutable,
            UseShellExecute = true,
            WorkingDirectory = options.ApplicationDirectory
        });
    }

    private static async Task WaitForApplicationExitAsync(int? processId, CancellationToken cancellationToken)
    {
        if (processId is not { } id)
        {
            return;
        }

        try
        {
            using var process = Process.GetProcessById(id);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromMinutes(2));

            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (ArgumentException)
        {
            // Der Prozess ist bereits beendet.
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException(
                "Das Vehistra wurde nicht innerhalb von zwei Minuten beendet. " +
                "Bitte schließen Sie die Anwendung und starten Sie das Update erneut.");
        }
    }

    /// <summary>Sichert die bisherigen Programmdateien als ZIP-Archiv.</summary>
    private string BackupProgramFiles(string applicationDirectory)
    {
        Directory.CreateDirectory(ApplicationPaths.UpdateBackup);

        var target = Path.Combine(ApplicationPaths.UpdateBackup,
            $"Programm_{DateTime.Now:yyyyMMdd_HHmmss}.zip");

        ZipFile.CreateFromDirectory(applicationDirectory, target, CompressionLevel.Fastest, includeBaseDirectory: false);
        _logger.LogInformation("Programmdateien gesichert: {Path}", target);

        // Aeltere Sicherungen aufraeumen, die letzten fuenf bleiben erhalten.
        foreach (var file in Directory.GetFiles(ApplicationPaths.UpdateBackup, "Programm_*.zip")
                     .OrderByDescending(f => f)
                     .Skip(5))
        {
            try
            {
                File.Delete(file);
            }
            catch (IOException)
            {
                // Aufraeumen ist optional.
            }
        }

        return target;
    }

    private void RestoreProgramFiles(string? backupPath, string applicationDirectory)
    {
        if (string.IsNullOrWhiteSpace(backupPath) || !File.Exists(backupPath))
        {
            return;
        }

        ZipFile.ExtractToDirectory(backupPath, applicationDirectory, overwriteFiles: true);
        _logger.LogWarning("Die vorherige Programmversion wurde wiederhergestellt: {Path}", backupPath);
    }

    private static async Task<int> RunInstallerAsync(UpdateOptions options, CancellationToken cancellationToken)
    {
        if (!File.Exists(options.InstallerPath))
        {
            throw new FileNotFoundException(
                $"Das Updatepaket wurde nicht gefunden: {options.InstallerPath}", options.InstallerPath);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = options.InstallerPath,
            // Unbeaufsichtigte Installation in das bestehende Verzeichnis (Inno-Setup-Schalter).
            Arguments = $"/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /DIR=\"{options.ApplicationDirectory}\"",
            UseShellExecute = true,
            Verb = "runas"
        };

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Der Update-Installer konnte nicht gestartet werden.");

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        return process.ExitCode;
    }

    private static string? ReadInstalledVersion(string applicationDirectory)
    {
        var executable = Path.Combine(applicationDirectory, "Vehistra.exe");

        if (!File.Exists(executable))
        {
            return null;
        }

        return FileVersionInfo.GetVersionInfo(executable).FileVersion;
    }

    private static async Task<string?> ReadSchemaVersionAsync(VehistraDbContext db, CancellationToken cancellationToken)
    {
        try
        {
            return await db.DatabaseVersions
                .AsNoTracking()
                .Where(v => v.IsCurrent)
                .OrderByDescending(v => v.AppliedAt)
                .Select(v => v.SchemaVersion)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private async Task RegisterApplicationVersionAsync(
        VehistraDbContext db,
        string version,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return;
        }

        var exists = await db.ApplicationVersions
            .AnyAsync(v => v.Version == version, cancellationToken)
            .ConfigureAwait(false);

        if (exists)
        {
            return;
        }

        db.ApplicationVersions.Add(new ApplicationVersion
        {
            Version = version,
            ReleasedAt = DateTime.Now,
            RecordedAt = DateTime.Now
        });

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task TryWriteHistoryAsync(UpdateHistory history, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<VehistraDbContext>();

            db.UpdateHistory.Add(history);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Der Updateverlauf konnte nicht protokolliert werden.");
        }
    }

    private static string BuildManualRestoreHint(string? programBackup) =>
        string.IsNullOrWhiteSpace(programBackup)
            ? "Bitte wenden Sie sich an den Support von LSP Virtual Services: support@vehistra.dev"
            : $"""
               Die bisherige Programmversion wurde gesichert unter:
               {programBackup}

               Sollte die Anwendung nicht starten, entpacken Sie dieses Archiv in das
               Installationsverzeichnis und wenden Sie sich an den Support:
               support@vehistra.dev
               """;
}

/// <summary>Fortschrittsmeldung des Updates.</summary>
public sealed record UpdateProgress(int Step, string Message);

/// <summary>Ergebnis des Updatelaufs.</summary>
public sealed record UpdateRunResult(bool IsSuccessful, string Message, string? RestoreInstructions, string? BackupPath);
