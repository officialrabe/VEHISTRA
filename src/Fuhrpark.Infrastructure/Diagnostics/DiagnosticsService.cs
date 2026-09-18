using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using Fuhrpark.Application.Abstractions;
using Fuhrpark.Infrastructure.Persistence;
using Fuhrpark.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fuhrpark.Infrastructure.Diagnostics;

/// <summary>
/// Systemdiagnose fuer den Hilfe- und Supportbereich. Das erzeugte Supportpaket enthaelt
/// ausschliesslich Logs und technische Angaben - keine Passwoerter und keine Fahrzeug-
/// oder Personendaten.
/// </summary>
public sealed class DiagnosticsService : IDiagnosticsService
{
    private readonly FuhrparkDbContext _db;
    private readonly IConnectionSettingsStore _connectionStore;
    private readonly IDocumentStorage _storage;
    private readonly ApplicationVersionProvider _versionProvider;
    private readonly IClock _clock;
    private readonly ILogger<DiagnosticsService> _logger;

    public DiagnosticsService(
        FuhrparkDbContext db,
        IConnectionSettingsStore connectionStore,
        IDocumentStorage storage,
        ApplicationVersionProvider versionProvider,
        IClock clock,
        ILogger<DiagnosticsService> logger)
    {
        _db = db;
        _connectionStore = connectionStore;
        _storage = storage;
        _versionProvider = versionProvider;
        _clock = clock;
        _logger = logger;
    }

    public async Task<DiagnosticsReport> RunAsync(CancellationToken cancellationToken = default)
    {
        var settings = _connectionStore.Load();

        var report = new DiagnosticsReport
        {
            CreatedAt = _clock.Now,
            ApplicationVersion = _versionProvider.Version,
            Server = settings?.Server,
            SqlInstance = ExtractInstance(settings?.Server),
            Database = settings?.Database,
            DocumentsPath = settings?.DocumentsPath,
            UpdatePath = settings?.UpdatePath,
            BackupPath = settings?.BackupPath,
            ComputerName = Environment.MachineName,
            OperatingSystem = ApplicationPaths.DescribeOperatingSystem(),
            RuntimeVersion = RuntimeInformation.FrameworkDescription,
            LogDirectory = ApplicationPaths.Logs
        };

        // Konfiguration
        report.Checks.Add(new DiagnosticsCheck(
            "Serverkonfiguration",
            settings is not null,
            settings is null
                ? "Es ist keine Serververbindung eingerichtet."
                : $"{settings.Server} / {settings.Database}",
            settings is null ? "Bitte die Servereinstellungen im Programm hinterlegen." : null));

        // Datenbankverbindung
        try
        {
            var canConnect = await _db.Database.CanConnectAsync(cancellationToken).ConfigureAwait(false);

            report.Checks.Add(new DiagnosticsCheck(
                "Datenbank erreichbar",
                canConnect,
                canConnect ? "Verbindung erfolgreich." : "Verbindung fehlgeschlagen.",
                canConnect
                    ? null
                    : "Bitte pruefen, ob der Server laeuft, TCP/IP aktiv ist und die Firewall freigegeben wurde."));

            if (canConnect)
            {
                var schemaVersion = await _db.DatabaseVersions
                    .AsNoTracking()
                    .Where(v => v.IsCurrent)
                    .OrderByDescending(v => v.AppliedAt)
                    .Select(v => v.SchemaVersion)
                    .FirstOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false);

                report.DatabaseSchemaVersion = schemaVersion;

                var pending = await _db.Database.GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false);
                var pendingList = pending.ToList();

                report.Checks.Add(new DiagnosticsCheck(
                    "Datenbankschema aktuell",
                    pendingList.Count == 0,
                    pendingList.Count == 0
                        ? $"Schemaversion {schemaVersion ?? "unbekannt"}"
                        : $"{pendingList.Count} ausstehende Aenderung(en)",
                    pendingList.Count == 0 ? null : "Bitte das Datenbankupdate ueber die Updatefunktion ausfuehren."));

                var vehicleCount = await _db.Vehicles.CountAsync(cancellationToken).ConfigureAwait(false);
                var userCount = await _db.Users.CountAsync(u => u.IsActive && !u.IsDeleted, cancellationToken)
                    .ConfigureAwait(false);

                report.Checks.Add(new DiagnosticsCheck(
                    "Datenbestand",
                    true,
                    $"{vehicleCount} Fahrzeug(e), {userCount} aktive(r) Benutzer"));
            }
        }
        catch (Exception exception)
        {
            var (message, hints) = SqlErrorTranslator.Translate(
                exception, settings?.Server ?? "unbekannt", settings?.Database ?? "FuhrparkDB");

            report.Checks.Add(new DiagnosticsCheck(
                "Datenbank erreichbar", false, message, string.Join(" ", hints)));
        }

        // Dokumentenablage
        var storageProbe = await _storage.ProbeAsync(cancellationToken).ConfigureAwait(false);
        report.Checks.Add(new DiagnosticsCheck(
            "Dokumentenablage",
            storageProbe is { Exists: true, CanRead: true, CanWrite: true },
            storageProbe.Message ?? "Keine Angabe",
            storageProbe.CanWrite
                ? null
                : "Bitte Netzwerkfreigabe und NTFS-Berechtigungen des Dokumentenordners pruefen."));

        // Updateablage
        var updatePathReachable = !string.IsNullOrWhiteSpace(settings?.UpdatePath)
                                  && Directory.Exists(settings.UpdatePath);

        report.Checks.Add(new DiagnosticsCheck(
            "Updateablage",
            updatePathReachable || string.IsNullOrWhiteSpace(settings?.UpdatePath),
            string.IsNullOrWhiteSpace(settings?.UpdatePath)
                ? "Keine Updateablage konfiguriert (optional)."
                : updatePathReachable
                    ? $"Erreichbar: {settings.UpdatePath}"
                    : $"Nicht erreichbar: {settings.UpdatePath}",
            updatePathReachable || string.IsNullOrWhiteSpace(settings?.UpdatePath)
                ? null
                : "Bitte Netzwerkfreigabe und Berechtigungen des Updateordners pruefen."));

        // Logverzeichnis
        var logsExist = Directory.Exists(ApplicationPaths.Logs);
        report.Checks.Add(new DiagnosticsCheck(
            "Logverzeichnis",
            logsExist,
            logsExist ? ApplicationPaths.Logs : "Das Logverzeichnis existiert nicht.",
            logsExist ? null : "Das Verzeichnis wird beim naechsten Programmstart automatisch angelegt."));

        return report;
    }

    public async Task<string> CreateSupportPackageAsync(
        string targetDirectory,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(targetDirectory);

        var report = await RunAsync(cancellationToken).ConfigureAwait(false);
        var fileName = $"Supportpaket_{Environment.MachineName}_{_clock.Now:yyyyMMdd_HHmmss}.zip";
        var targetPath = Path.Combine(targetDirectory, fileName);

        await using var stream = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create);

        // Diagnosebericht
        var diagnosticsEntry = archive.CreateEntry("Diagnose.txt");
        await using (var entryStream = diagnosticsEntry.Open())
        await using (var writer = new StreamWriter(entryStream, Encoding.UTF8))
        {
            await writer.WriteAsync(FormatForClipboard(report)).ConfigureAwait(false);
        }

        // Logdateien der letzten 14 Tage
        if (Directory.Exists(ApplicationPaths.Logs))
        {
            var limit = _clock.Now.AddDays(-14);

            foreach (var file in Directory.EnumerateFiles(ApplicationPaths.Logs, "*.log"))
            {
                var info = new FileInfo(file);
                if (info.LastWriteTime < limit)
                {
                    continue;
                }

                try
                {
                    archive.CreateEntryFromFile(file, Path.Combine("Logs", info.Name));
                }
                catch (IOException exception)
                {
                    _logger.LogWarning(exception, "Logdatei konnte nicht in das Supportpaket aufgenommen werden: {File}", file);
                }
            }
        }

        _logger.LogInformation("Supportpaket erstellt: {Path}", targetPath);
        return targetPath;
    }

    public string FormatForClipboard(DiagnosticsReport report)
    {
        var builder = new StringBuilder();

        builder.AppendLine("FUHRPARKMANAGEMENT - SYSTEMDIAGNOSE");
        builder.AppendLine("Entwickelt von LSP Virtual Services");
        builder.AppendLine(new string('=', 60));
        builder.AppendLine($"Erstellt am        : {report.CreatedAt:dd.MM.yyyy HH:mm:ss}");
        builder.AppendLine($"Programmversion    : {report.ApplicationVersion}");
        builder.AppendLine($"Datenbankschema    : {report.DatabaseSchemaVersion ?? "unbekannt"}");
        builder.AppendLine($"Computername       : {report.ComputerName}");
        builder.AppendLine($"Betriebssystem     : {report.OperatingSystem}");
        builder.AppendLine($"Laufzeitumgebung   : {report.RuntimeVersion}");
        builder.AppendLine();
        builder.AppendLine("VERBINDUNG");
        builder.AppendLine(new string('-', 60));
        builder.AppendLine($"Server             : {report.Server ?? "-"}");
        builder.AppendLine($"SQL-Instanz        : {report.SqlInstance ?? "-"}");
        builder.AppendLine($"Datenbank          : {report.Database ?? "-"}");
        builder.AppendLine($"Dokumentenpfad     : {report.DocumentsPath ?? "-"}");
        builder.AppendLine($"Updatepfad         : {report.UpdatePath ?? "-"}");
        builder.AppendLine($"Backupverzeichnis  : {report.BackupPath ?? "-"}");
        builder.AppendLine($"Logverzeichnis     : {report.LogDirectory}");
        builder.AppendLine();
        builder.AppendLine("PRUEFUNGEN");
        builder.AppendLine(new string('-', 60));

        foreach (var check in report.Checks)
        {
            builder.AppendLine($"[{(check.IsSuccessful ? "OK  " : "FEHL")}] {check.Name}: {check.Result}");

            if (!check.IsSuccessful && !string.IsNullOrWhiteSpace(check.Hint))
            {
                builder.AppendLine($"        Hinweis: {check.Hint}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("Support: hallo@LeonLSP.dev · LeonLSP.dev");

        return builder.ToString();
    }

    private static string? ExtractInstance(string? server)
    {
        if (string.IsNullOrWhiteSpace(server))
        {
            return null;
        }

        var index = server.IndexOf('\\');
        return index < 0 ? "MSSQLSERVER (Standardinstanz)" : server[(index + 1)..];
    }
}
