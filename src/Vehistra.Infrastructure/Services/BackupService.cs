using Vehistra.Application.Abstractions;
using Vehistra.Application.Services;
using Vehistra.Domain.Entities;
using Vehistra.Domain.Security;
using Vehistra.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Vehistra.Infrastructure.Services;

/// <summary>
/// Erstellt Datenbanksicherungen und prueft sie anschliessend. Wie gesichert wird,
/// haengt vom Anbieter ab (siehe <see cref="IBackupEngine"/>): bei SQL Server
/// BACKUP DATABASE auf dem Server, beim Solo-Platz VACUUM INTO als Dateikopie.
/// </summary>
public sealed class BackupService : IBackupService
{
    private readonly VehistraDbContext _db;
    private readonly IConnectionSettingsStore _connectionStore;
    private readonly ISettingsService _settings;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;
    private readonly ILogger<BackupService> _logger;

    public BackupService(
        VehistraDbContext db,
        IConnectionSettingsStore connectionStore,
        ISettingsService settings,
        ICurrentUserService currentUser,
        IClock clock,
        ILogger<BackupService> logger)
    {
        _db = db;
        _connectionStore = connectionStore;
        _settings = settings;
        _currentUser = currentUser;
        _clock = clock;
        _logger = logger;
    }

    public async Task<BackupResult> CreateBackupAsync(
        BackupRequest request,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        // Ein Backup vor einer Migration laeuft technisch, ohne dass ein Benutzer angemeldet ist.
        if (request.Kind != "VorMigration" && _currentUser.IsAuthenticated)
        {
            _currentUser.DemandPermission(Permissions.BackupManage);
        }

        var settings = _connectionStore.Load();
        if (settings is null)
        {
            return new BackupResult(false, null, null, false,
                "Es ist keine Serververbindung konfiguriert.");
        }

        var directory = request.Directory ?? settings.BackupPath;
        if (string.IsNullOrWhiteSpace(directory))
        {
            return new BackupResult(false, null, null, false,
                "Es ist kein Backupverzeichnis konfiguriert. Bitte in den Einstellungen hinterlegen.");
        }

        var history = new BackupHistory
        {
            StartedAt = _clock.Now,
            Kind = request.Kind,
            ComputerName = Environment.MachineName,
            TriggeredByUserName = _currentUser.User?.UserName ?? Environment.UserName
        };

        var engine = CreateEngine(settings);
        var fileName = engine.BuildFileName(settings, request.Kind, _clock.Now);
        var backupPath = engine.CombinePath(directory, fileName);

        try
        {
            progress?.Report($"Sicherung wird erstellt: {backupPath}");

            var description = $"Vehistra {request.Kind} {_clock.Now:dd.MM.yyyy HH:mm}";

            await engine.CreateAsync(_db.Database, settings, backupPath, description, cancellationToken)
                .ConfigureAwait(false);

            history.FilePath = backupPath;
            history.IsSuccessful = true;
            history.FinishedAt = _clock.Now;

            long? size = null;
            try
            {
                // Die Groesse laesst sich nur ermitteln, wenn der Pfad auch vom Client erreichbar ist.
                if (File.Exists(backupPath))
                {
                    size = new FileInfo(backupPath).Length;
                    history.FileSizeBytes = size;
                }
            }
            catch (IOException)
            {
                // Groesse ist optional.
            }

            var verified = false;
            if (request.VerifyAfterBackup)
            {
                progress?.Report("Sicherung wird geprueft ...");
                var verifyResult = await VerifyBackupInternalAsync(backupPath, cancellationToken).ConfigureAwait(false);
                verified = verifyResult.IsSuccessful;
                history.IsVerified = verified;

                if (!verified)
                {
                    history.ErrorMessage = verifyResult.Message;
                }
            }

            _db.BackupHistory.Add(history);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Datenbanksicherung erstellt: {Path}", backupPath);

            var meldung = verified
                ? "Die Sicherung wurde erstellt und erfolgreich geprueft."
                : "Die Sicherung wurde erstellt.";

            // Erst sichern, dann aufraeumen - nie umgekehrt. Scheitert das
            // Aufraeumen, bleibt die neue Sicherung davon unberuehrt.
            try
            {
                var aufgeraeumt = await CleanUpInternalAsync(
                    new BackupCleanupRequest { Directory = directory, PreviewOnly = false },
                    pruefeBerechtigung: false,
                    cancellationToken).ConfigureAwait(false);

                if (aufgeraeumt.Candidates.Any(c => c.IsDeleted))
                {
                    progress?.Report(aufgeraeumt.Message);
                    meldung += Environment.NewLine + aufgeraeumt.Message;
                }
            }
            catch (Exception aufraeumFehler)
            {
                _logger.LogWarning(aufraeumFehler,
                    "Die Sicherung wurde erstellt, das Aufraeumen alter Sicherungen ist fehlgeschlagen.");
            }

            return new BackupResult(true, backupPath, size, verified, meldung);
        }
        catch (Exception exception)
        {
            var (message, hints) = SqlErrorTranslator.Translate(exception, settings.Server, settings.Database);

            history.IsSuccessful = false;
            history.FinishedAt = _clock.Now;
            history.ErrorMessage = exception.Message;

            try
            {
                _db.BackupHistory.Add(history);
                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception saveException)
            {
                _logger.LogError(saveException, "Der fehlgeschlagene Sicherungslauf konnte nicht protokolliert werden.");
            }

            _logger.LogError(exception, "Die Datenbanksicherung ist fehlgeschlagen.");

            var detail = string.Join(Environment.NewLine, hints.Prepend(message));
            return new BackupResult(false, backupPath, null, false,
                "Die Datenbanksicherung konnte nicht erstellt werden." + Environment.NewLine + detail,
                exception.Message);
        }
    }

    public async Task<BackupResult> VerifyBackupAsync(string backupPath, CancellationToken cancellationToken = default)
    {
        if (_currentUser.IsAuthenticated)
        {
            _currentUser.DemandPermission(Permissions.BackupManage);
        }

        return await VerifyBackupInternalAsync(backupPath, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<BackupInfo>> GetHistoryAsync(
        int maxCount = 50,
        CancellationToken cancellationToken = default)
    {
        if (_currentUser.IsAuthenticated)
        {
            _currentUser.DemandPermission(Permissions.BackupManage);
        }

        return await _db.BackupHistory
            .AsNoTracking()
            .OrderByDescending(b => b.StartedAt)
            .Take(maxCount)
            .Select(b => new BackupInfo(
                b.StartedAt, b.FinishedAt, b.FilePath, b.FileSizeBytes,
                b.IsSuccessful, b.IsVerified, b.Kind, b.TriggeredByUserName, b.ErrorMessage))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Waehlt die Sicherungsmaschinerie passend zum Anbieter.</summary>
    private static IBackupEngine CreateEngine(ServerConnectionSettings settings) =>
        settings.Provider == DatabaseProvider.Sqlite
            ? new SqliteBackupEngine()
            : new SqlServerBackupEngine();

    public async Task<BackupCleanupResult> CleanUpAsync(
        BackupCleanupRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return await CleanUpInternalAsync(request, pruefeBerechtigung: true, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Raeumt das Sicherungsverzeichnis auf. Bewusst eng gefasst, weil Loeschen
    /// endgueltig ist: nur das angegebene Verzeichnis ohne Unterordner, nur
    /// Dateien, deren Name aus diesem Programm stammt, nie eine Sicherung vor
    /// einer Migration, und die neuesten bleiben immer liegen.
    /// </summary>
    private async Task<BackupCleanupResult> CleanUpInternalAsync(
        BackupCleanupRequest request,
        bool pruefeBerechtigung,
        CancellationToken cancellationToken)
    {
        if (pruefeBerechtigung && _currentUser.IsAuthenticated)
        {
            _currentUser.DemandPermission(Permissions.BackupManage);
        }

        var tage = request.RetentionDays
            ?? await _settings.GetIntAsync(SettingsKeys.BackupRetentionDays, 0, cancellationToken).ConfigureAwait(false);

        if (tage <= 0)
        {
            return new BackupCleanupResult(0, 0, false, [],
                "Es ist keine Aufbewahrungsdauer eingestellt. Es wird keine Sicherung geloescht.");
        }

        var settings = _connectionStore.Load();
        if (settings is null)
        {
            return new BackupCleanupResult(tage, 0, false, [],
                "Es ist keine Serververbindung konfiguriert.");
        }

        var directory = request.Directory ?? settings.BackupPath;
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            // Bei SQL Server liegen die Sicherungen auf dem Server; ist der Pfad
            // von hier nicht erreichbar, wird nichts geloescht.
            return new BackupCleanupResult(tage, 0, false, [],
                "Das Sicherungsverzeichnis ist von diesem Arbeitsplatz nicht erreichbar. " +
                "Es wurde nichts geloescht.");
        }

        var engine = CreateEngine(settings);
        var grenze = _clock.Today.AddDays(-tage);

        var eigene = new List<(string Path, DateTime CreatedAt, string Kind, long Size)>();

        foreach (var pfad in Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly))
        {
            var name = Path.GetFileName(pfad);

            if (!engine.TryReadOwnFileName(settings, name, out var erstellt, out var art))
            {
                continue;
            }

            long groesse = 0;
            try
            {
                groesse = new FileInfo(pfad).Length;
            }
            catch (IOException)
            {
                // Groesse ist nur Information.
            }

            eigene.Add((pfad, erstellt, art, groesse));
        }

        var sortiert = eigene.OrderByDescending(e => e.CreatedAt).ToList();
        var behalten = sortiert.Take(BackupRetention.MinimumKept).Select(e => e.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var kandidaten = new List<BackupCleanupCandidate>();

        foreach (var eintrag in sortiert)
        {
            if (behalten.Contains(eintrag.Path)
                || string.Equals(eintrag.Kind, BackupRetention.NeverDeletedKind, StringComparison.OrdinalIgnoreCase)
                || eintrag.CreatedAt.Date > grenze)
            {
                continue;
            }

            var alter = (_clock.Today - eintrag.CreatedAt.Date).Days;

            if (request.PreviewOnly)
            {
                kandidaten.Add(new BackupCleanupCandidate(
                    eintrag.Path, eintrag.CreatedAt, alter, eintrag.Size, eintrag.Kind, false));
                continue;
            }

            try
            {
                File.Delete(eintrag.Path);

                // Begleitdateien einer Kopie, falls sie jemand geoeffnet hat.
                foreach (var anhang in new[] { eintrag.Path + "-wal", eintrag.Path + "-shm" })
                {
                    if (File.Exists(anhang))
                    {
                        File.Delete(anhang);
                    }
                }

                _logger.LogInformation(
                    "Sicherung nach {Tage} Tagen Aufbewahrung geloescht: {Path} ({Alter} Tage alt, {Bytes} Bytes)",
                    tage, eintrag.Path, alter, eintrag.Size);

                kandidaten.Add(new BackupCleanupCandidate(
                    eintrag.Path, eintrag.CreatedAt, alter, eintrag.Size, eintrag.Kind, true));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(exception, "Die Sicherung konnte nicht geloescht werden: {Path}", eintrag.Path);

                kandidaten.Add(new BackupCleanupCandidate(
                    eintrag.Path, eintrag.CreatedAt, alter, eintrag.Size, eintrag.Kind, false, exception.Message));
            }
        }

        var geloescht = kandidaten.Count(c => c.IsDeleted);
        var fehlgeschlagen = kandidaten.Count(c => !c.IsDeleted && c.Problem is not null);

        var meldung = request.PreviewOnly
            ? kandidaten.Count == 0
                ? $"Es gibt keine Sicherung, die aelter als {tage} Tage ist. " +
                  $"Die {BackupRetention.MinimumKept} neuesten bleiben ohnehin erhalten."
                : $"{kandidaten.Count} Sicherung(en) sind aelter als {tage} Tage und wuerden entfernt."
            : geloescht == 0
                ? $"Es wurde keine Sicherung entfernt (Aufbewahrung {tage} Tage)."
                : $"{geloescht} alte Sicherung(en) wurden entfernt (Aufbewahrung {tage} Tage)." +
                  (fehlgeschlagen > 0 ? $" {fehlgeschlagen} Datei(en) liessen sich nicht loeschen." : string.Empty);

        return new BackupCleanupResult(
            tage,
            Math.Min(sortiert.Count, BackupRetention.MinimumKept),
            !request.PreviewOnly,
            kandidaten,
            meldung);
    }

    private async Task<BackupResult> VerifyBackupInternalAsync(string backupPath, CancellationToken cancellationToken)
    {
        var engine = CreateEngine(_connectionStore.Load() ?? new ServerConnectionSettings());

        try
        {
            await engine.VerifyAsync(_db.Database, backupPath, cancellationToken).ConfigureAwait(false);

            return new BackupResult(true, backupPath, null, true, engine.VerifyDescription);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Die Pruefung der Sicherung ist fehlgeschlagen: {Path}", backupPath);

            return new BackupResult(false, backupPath, null, false,
                "Die Sicherungsdatei konnte nicht geprueft werden.", exception.Message);
        }
    }


}
