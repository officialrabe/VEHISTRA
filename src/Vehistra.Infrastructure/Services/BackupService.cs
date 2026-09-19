using Vehistra.Application.Abstractions;
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
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;
    private readonly ILogger<BackupService> _logger;

    public BackupService(
        VehistraDbContext db,
        IConnectionSettingsStore connectionStore,
        ICurrentUserService currentUser,
        IClock clock,
        ILogger<BackupService> logger)
    {
        _db = db;
        _connectionStore = connectionStore;
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

            return new BackupResult(true, backupPath, size, verified,
                verified
                    ? "Die Sicherung wurde erstellt und erfolgreich geprueft."
                    : "Die Sicherung wurde erstellt.");
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
