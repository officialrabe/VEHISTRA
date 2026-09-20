using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using Vehistra.Application.Abstractions;
using Vehistra.Domain.Security;
using Vehistra.Infrastructure;
using Vehistra.Infrastructure.Persistence;
using Vehistra.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Vehistra.Application.Services;

namespace Vehistra.ServerCheck;

/// <summary>
/// Fuehrt alle Pruefungen der Serverinstallation aus. Jede Meldung ist in einfacher
/// Sprache gehalten und nennt den naechsten konkreten Schritt.
/// </summary>
public sealed class ServerCheckRunner
{
    private const string GroupConfiguration = "Konfiguration";
    private const string GroupNetwork = "Netzwerk";
    private const string GroupDatabase = "Datenbank";
    private const string GroupFolders = "Ordner und Freigaben";
    private const string GroupSystem = "System";

    private readonly IServiceProvider _services;
    private readonly IConnectionSettingsStore _connectionStore;
    private readonly ApplicationVersionProvider _versionProvider;
    private readonly ILogger<ServerCheckRunner> _logger;

    public ServerCheckRunner(
        IServiceProvider services,
        IConnectionSettingsStore connectionStore,
        ApplicationVersionProvider versionProvider,
        ILogger<ServerCheckRunner> logger)
    {
        _services = services;
        _connectionStore = connectionStore;
        _versionProvider = versionProvider;
        _logger = logger;
    }

    /// <summary>Fuehrt alle Pruefungen aus und meldet den Fortschritt an die Oberflaeche.</summary>
    public async Task<IReadOnlyList<CheckResult>> RunAsync(
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var results = new List<CheckResult>();

        progress?.Report("Systemangaben werden gesammelt ...");
        AddSystemChecks(results);

        progress?.Report("Die Serverkonfiguration wird gelesen ...");
        var settings = _connectionStore.Load();

        // Was fehlen kann, haengt von der Betriebsart ab: im Netzwerkbetrieb der
        // Servername, beim Solo-Platz die Datenbankdatei.
        var unvollstaendig = settings is null
            || (settings.IsSingleWorkstation
                ? string.IsNullOrWhiteSpace(settings.DatabaseFile)
                : string.IsNullOrWhiteSpace(settings.Server));

        if (unvollstaendig)
        {
            results.Add(CheckResult.Problem(GroupConfiguration, "Serverkonfiguration",
                "Auf diesem Computer ist noch keine Verbindung zur Fuhrparkdatenbank hinterlegt.",
                "Bitte zuerst VehistraServerSetup.exe ausführen (auf dem Server) " +
                "oder Vehistra.exe starten und die Servereinstellungen eintragen.",
                $"Erwartete Konfigurationsdatei: {_connectionStore.ConfigFilePath}"));

            results.Add(CheckResult.Skipped(GroupNetwork, "Erreichbarkeit",
                "Ohne Serverkonfiguration kann die Erreichbarkeit nicht geprüft werden."));

            results.Add(CheckResult.Skipped(GroupDatabase, "Datenbank",
                "Ohne Serverkonfiguration kann die Datenbank nicht geprüft werden."));

            return results;
        }

        // Nach der Pruefung oben steht die Konfiguration fest.
        ArgumentNullException.ThrowIfNull(settings);

        results.Add(CheckResult.Ok(GroupConfiguration, "Betriebsart",
            settings.IsSingleWorkstation
                ? $"Solo-Platz: die Datenbank liegt als Datei auf diesem Computer ({settings.DatabaseFile})."
                : $"Netzwerkbetrieb: Server „{settings.Server}“, Datenbank „{settings.Database}“.",
            $"Datei: {_connectionStore.ConfigFilePath} · zuletzt geändert am " +
            $"{settings.ConfiguredAt:dd.MM.yyyy HH:mm} durch {settings.ConfiguredBy}"));

        if (settings.IsSingleWorkstation)
        {
            results.Add(CheckResult.Ok(GroupNetwork, "Netzwerk",
                "Beim Solo-Platz wird kein Netzwerk benötigt.",
                "Es gibt keinen Server, keine Freigabe und keine Firewallregel zu prüfen."));

            await AddDatabaseChecksAsync(results, settings, cancellationToken).ConfigureAwait(false);
            AddFolderChecks(results, settings);

            progress?.Report("Prüfung abgeschlossen.");
            return results;
        }

        results.Add(settings.UseWindowsAuthentication
            ? CheckResult.Ok(GroupConfiguration, "Anmeldeverfahren",
                "Es wird die Windows-Authentifizierung verwendet (empfohlen).")
            : CheckResult.Warning(GroupConfiguration, "Anmeldeverfahren",
                $"Es wird die SQL-Server-Anmeldung mit dem Konto „{settings.SqlUserName}“ verwendet.",
                "Die Windows-Authentifizierung ist sicherer, weil dabei kein Passwort gespeichert werden muss.",
                "Das gespeicherte Passwort liegt DPAPI-verschlüsselt vor und ist an diesen Computer gebunden."));

        progress?.Report("Die Erreichbarkeit des Servers wird geprüft ...");
        await AddNetworkChecksAsync(results, settings, cancellationToken).ConfigureAwait(false);

        progress?.Report("Die Datenbank wird geprüft ...");
        await AddDatabaseChecksAsync(results, settings, cancellationToken).ConfigureAwait(false);

        progress?.Report("Ordner und Freigaben werden geprüft ...");
        AddFolderChecks(results, settings);

        progress?.Report("Prüfung abgeschlossen.");
        return results;
    }

    private void AddSystemChecks(List<CheckResult> results)
    {
        results.Add(CheckResult.Ok(GroupSystem, "Computer",
            $"Computername: {Environment.MachineName}",
            $"Angemeldeter Benutzer: {Environment.UserDomainName}\\{Environment.UserName}"));

        results.Add(CheckResult.Ok(GroupSystem, "Betriebssystem",
            ApplicationPaths.DescribeOperatingSystem()));

        results.Add(CheckResult.Ok(GroupSystem, "Programmversion",
            $"VehistraServerCheck {_versionProvider.Version}",
            RuntimeInformation.FrameworkDescription));

        foreach (var drive in GetFixedDrives())
        {
            var freeGigabytes = drive.AvailableFreeSpace / 1024d / 1024d / 1024d;

            results.Add(freeGigabytes switch
            {
                < 2 => CheckResult.Problem(GroupSystem, $"Speicherplatz {drive.Name}",
                    $"Auf Laufwerk {drive.Name} sind nur noch {freeGigabytes:N1} GB frei.",
                    "Unter 2 GB können Sicherungen und Dokumente nicht mehr zuverlässig gespeichert werden.",
                    "Bitte nicht mehr benötigte Dateien entfernen oder das Laufwerk vergrößern."),

                < 10 => CheckResult.Warning(GroupSystem, $"Speicherplatz {drive.Name}",
                    $"Auf Laufwerk {drive.Name} sind noch {freeGigabytes:N1} GB frei.",
                    "Für Datenbank, Dokumente und Sicherungen werden mindestens 10 GB empfohlen."),

                _ => CheckResult.Ok(GroupSystem, $"Speicherplatz {drive.Name}",
                    $"Auf Laufwerk {drive.Name} sind {freeGigabytes:N1} GB frei.")
            });
        }

        results.Add(Directory.Exists(ApplicationPaths.Logs)
            ? CheckResult.Ok(GroupSystem, "Protokollordner",
                $"Der Protokollordner ist vorhanden: {ApplicationPaths.Logs}",
                $"{CountLogFiles()} Protokolldatei(en) enthalten.")
            : CheckResult.Warning(GroupSystem, "Protokollordner",
                $"Der Protokollordner fehlt: {ApplicationPaths.Logs}",
                "Er wird beim nächsten Programmstart automatisch angelegt."));
    }

    private async Task AddNetworkChecksAsync(
        List<CheckResult> results,
        ServerConnectionSettings settings,
        CancellationToken cancellationToken)
    {
        var host = ExtractHost(settings.Server);
        var instance = ExtractInstance(settings.Server);
        var isLocal = IsLocalHost(host);

        if (isLocal)
        {
            results.Add(CheckResult.Ok(GroupNetwork, "Serverstandort",
                "Die Datenbank liegt auf diesem Computer. Eine Netzwerkprüfung ist nicht nötig."));
        }
        else
        {
            var reachable = await PingAsync(host, cancellationToken).ConfigureAwait(false);

            results.Add(reachable
                ? CheckResult.Ok(GroupNetwork, "Server erreichbar",
                    $"Der Server „{host}“ antwortet im Netzwerk.")
                : CheckResult.Warning(GroupNetwork, "Server erreichbar",
                    $"Der Server „{host}“ antwortet nicht auf eine Netzwerkanfrage (Ping).",
                    "Ist der Server eingeschaltet?",
                    "Ist dieser Computer mit dem Firmennetz verbunden?",
                    "Ist der Servername richtig geschrieben?",
                    "Viele Server beantworten keine Pings. Wenn die folgende Verbindungsprüfung " +
                    "erfolgreich ist, ist dieser Hinweis unkritisch."));
        }

        var port = instance is null ? 1433 : (int?)null;

        if (port is not null)
        {
            var open = await CanConnectToPortAsync(host, port.Value, cancellationToken).ConfigureAwait(false);

            results.Add(open
                ? CheckResult.Ok(GroupNetwork, "SQL-Server-Port",
                    $"Der SQL-Server-Port {port} auf „{host}“ ist erreichbar.")
                : CheckResult.Problem(GroupNetwork, "SQL-Server-Port",
                    $"Der SQL-Server-Port {port} auf „{host}“ ist nicht erreichbar.",
                    "Läuft der Dienst „SQL Server“ auf dem Server?",
                    "Ist im SQL Server Configuration Manager das Protokoll TCP/IP aktiviert?",
                    "Ist die Windows-Firewall des Servers für Port 1433 freigegeben?"));
        }
        else
        {
            results.Add(CheckResult.Ok(GroupNetwork, "Benannte Instanz",
                $"Es wird die benannte Instanz „{instance}“ verwendet.",
                "Benannte Instanzen verwenden wechselnde Ports. Dafür muss auf dem Server der " +
                "Dienst „SQL Server-Browser“ laufen (UDP-Port 1434)."));
        }
    }

    private async Task AddDatabaseChecksAsync(
        List<CheckResult> results,
        ServerConnectionSettings settings,
        CancellationToken cancellationToken)
    {
        using var scope = _services.CreateScope();
        var administration = scope.ServiceProvider.GetRequiredService<IDatabaseAdministrationService>();

        var connection = await administration.TestConnectionAsync(settings, cancellationToken).ConfigureAwait(false);

        results.Add(new CheckResult(
            GroupDatabase,
            "Verbindung",
            connection.IsSuccessful ? CheckState.Ok : CheckState.Problem,
            connection.Message,
            connection.Hints,
            connection.TechnicalDetails));

        if (!connection.IsSuccessful)
        {
            results.Add(CheckResult.Skipped(GroupDatabase, "Datenbankinhalt",
                "Solange keine Verbindung besteht, kann der Inhalt nicht geprüft werden."));
            return;
        }

        try
        {
            var exists = await administration.DatabaseExistsAsync(settings, cancellationToken).ConfigureAwait(false);

            if (!exists)
            {
                results.Add(CheckResult.Problem(GroupDatabase, "Datenbank",
                    $"Die Datenbank „{settings.Database}“ ist auf dem Server nicht vorhanden.",
                    "Bitte auf dem Server VehistraServerSetup.exe ausführen.",
                    "Alternativ ist im Programm ein falscher Datenbankname eingetragen."));
                return;
            }

            results.Add(CheckResult.Ok(GroupDatabase, "Datenbank",
                $"Die Datenbank „{settings.Database}“ ist vorhanden."));

            var db = scope.ServiceProvider.GetRequiredService<VehistraDbContext>();

            var pending = (await administration.GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false))
                .ToList();
            var applied = (await administration.GetAppliedMigrationsAsync(cancellationToken).ConfigureAwait(false))
                .ToList();

            results.Add(pending.Count == 0
                ? CheckResult.Ok(GroupDatabase, "Datenbankstruktur",
                    "Die Datenbankstruktur ist auf dem aktuellen Stand.",
                    $"{applied.Count} Änderung(en) angewendet, zuletzt: {applied.LastOrDefault() ?? "–"}")
                : CheckResult.Warning(GroupDatabase, "Datenbankstruktur",
                    $"Es stehen noch {pending.Count} Datenbankänderung(en) aus.",
                    "Diese werden beim nächsten Programmstart automatisch angewendet.",
                    "Vor der Änderung wird automatisch eine Sicherung erstellt.",
                    "Ausstehend: " + string.Join(", ", pending)));

            await AddDatabaseContentChecksAsync(results, db, settings, cancellationToken).ConfigureAwait(false);
            await AddPermissionChecksAsync(results, db, settings, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            var (message, hints) = SqlErrorTranslator.Translate(exception, settings.Server, settings.Database);
            _logger.LogError(exception, "Die Datenbankprüfung ist fehlgeschlagen.");

            results.Add(new CheckResult(GroupDatabase, "Datenbankprüfung", CheckState.Problem,
                message, hints, exception.Message));
        }
    }

    private static async Task AddDatabaseContentChecksAsync(
        List<CheckResult> results,
        VehistraDbContext db,
        ServerConnectionSettings settings,
        CancellationToken cancellationToken)
    {
        var tableCount = await DatabaseFacts
            .GetTableCountAsync(db, settings.Provider, cancellationToken)
            .ConfigureAwait(false);

        results.Add(tableCount > 0
            ? CheckResult.Ok("Datenbank", "Tabellen", $"Die Datenbank enthält {tableCount} Tabellen.")
            : CheckResult.Problem("Datenbank", "Tabellen",
                "Die Datenbank enthält keine Tabellen.",
                "Bitte auf dem Server VehistraServerSetup.exe erneut ausführen."));

        var administrators = await db.Users
            .CountAsync(u => u.IsActive && u.UserRoles.Any(r => r.Role!.Name == RoleNames.Administrator), cancellationToken)
            .ConfigureAwait(false);

        results.Add(administrators > 0
            ? CheckResult.Ok("Datenbank", "Administratorkonto",
                $"Es sind {administrators} aktive(s) Administratorkonto(s) vorhanden.")
            : CheckResult.Problem("Datenbank", "Administratorkonto",
                "Es gibt kein aktives Administratorkonto.",
                "Ohne Administratorkonto kann sich niemand am Programm anmelden.",
                "Bitte auf dem Server VehistraServerSetup.exe ausführen und Schritt 10 wiederholen."));

        var vehicles = await db.Vehicles.CountAsync(cancellationToken).ConfigureAwait(false);
        var drivers = await db.Drivers.CountAsync(cancellationToken).ConfigureAwait(false);

        results.Add(CheckResult.Ok("Datenbank", "Datenbestand",
            $"{vehicles} Fahrzeug(e) und {drivers} Fahrer erfasst."));

        var databaseSize = await DatabaseFacts
            .GetSizeMegabytesAsync(db, settings, cancellationToken)
            .ConfigureAwait(false);

        if (databaseSize is { } groesse)
        {
            results.Add(CheckResult.Ok("Datenbank", "Größe",
                $"Die Datenbank belegt {groesse:N1} MB."));
        }
    }

    private static async Task AddPermissionChecksAsync(
        List<CheckResult> results,
        VehistraDbContext db,
        ServerConnectionSettings settings,
        CancellationToken cancellationToken)
    {
        var (canRead, canWrite) = await DatabaseFacts
            .GetPermissionsAsync(db, settings.Provider, cancellationToken)
            .ConfigureAwait(false);

        results.Add(canRead && canWrite
            ? CheckResult.Ok("Datenbank", "Berechtigungen",
                "Das verwendete Konto darf Daten lesen und schreiben.")
            : CheckResult.Problem("Datenbank", "Berechtigungen",
                canRead
                    ? "Das verwendete Konto darf Daten lesen, aber nicht schreiben."
                    : "Dem verwendeten Konto fehlen Leseberechtigungen auf der Datenbank.",
                settings.IsSingleWorkstation
                    ? "Bitte die Schreibrechte des Windows-Kontos auf den Ordner der Datenbankdatei prüfen."
                    : "Das Konto benötigt die Datenbankrollen „db_datareader“ und „db_datawriter“.",
                settings.IsSingleWorkstation
                    ? "Ist die Datei schreibgeschützt?"
                    : "Diese werden im SQL Server Management Studio unter Sicherheit › Benutzer vergeben.",
                settings.IsSingleWorkstation
                    ? "Sichert gerade ein Sicherungsprogramm die Datei?"
                    : "Kapitel 14 der Serveranleitung beschreibt die Schritte im Detail."));
    }


    private static void AddFolderChecks(List<CheckResult> results, ServerConnectionSettings settings)
    {
        AddFolderCheck(results, settings.DocumentsPath, "Dokumentenordner",
            "Hier liegen alle Fahrzeugdokumente. Ohne diesen Ordner können keine Dateien geöffnet werden.");

        AddFolderCheck(results, settings.BackupPath, "Backupordner",
            "Hier legt der SQL Server die Datenbanksicherungen ab.");

        AddFolderCheck(results, settings.UpdatePath, "Updateordner",
            "Aus diesem Ordner holen sich die Arbeitsplätze neue Programmversionen.");

        if (string.IsNullOrWhiteSpace(settings.UpdatePath) || !Directory.Exists(settings.UpdatePath))
        {
            return;
        }

        var manifest = Path.Combine(settings.UpdatePath, "latest.json");

        results.Add(File.Exists(manifest)
            ? CheckResult.Ok(GroupFolders, "Updateinformationen",
                "Die Datei „latest.json“ ist vorhanden.",
                $"Zuletzt geändert: {File.GetLastWriteTime(manifest):dd.MM.yyyy HH:mm}")
            : CheckResult.Warning(GroupFolders, "Updateinformationen",
                "Im Updateordner fehlt die Datei „latest.json“.",
                "Ohne diese Datei finden die Arbeitsplätze keine neuen Versionen.",
                "Sie wird von CreateRelease.ps1 erzeugt und in den Updateordner kopiert."));
    }

    private static void AddFolderCheck(
        List<CheckResult> results,
        string? path,
        string name,
        string purpose)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            results.Add(CheckResult.Warning(GroupFolders, name,
                $"Für den {name} ist kein Pfad hinterlegt.",
                purpose,
                "Der Pfad wird im Programm unter Einstellungen › Pfade eingetragen."));
            return;
        }

        if (!Directory.Exists(path))
        {
            results.Add(CheckResult.Problem(GroupFolders, name,
                $"Der {name} ist nicht erreichbar: {path}",
                purpose,
                "Ist der Server eingeschaltet und die Freigabe verfügbar?",
                "Besitzt das angemeldete Windows-Konto Zugriff auf die Freigabe?",
                "Ist der Pfad richtig geschrieben (z. B. \\\\FUHRPARK-SRV01\\Fuhrpark\\Dokumente)?"));
            return;
        }

        var probe = Path.Combine(path, $".pruefung_{Guid.NewGuid():N}.tmp");

        try
        {
            File.WriteAllText(probe, "Schreibtest");
            File.Delete(probe);

            results.Add(CheckResult.Ok(GroupFolders, name,
                $"Der {name} ist erreichbar und beschreibbar: {path}"));
        }
        catch (Exception exception)
        {
            results.Add(CheckResult.Problem(GroupFolders, name,
                $"In den {name} kann nicht geschrieben werden: {path}",
                purpose,
                "Bitte die Freigabe- und NTFS-Berechtigungen des Ordners prüfen.",
                "Die Benutzer benötigen mindestens „Ändern“ auf dem Dokumentenordner.",
                "Technischer Hinweis: " + exception.Message));
        }
    }

    /// <summary>Erzeugt den Textbericht fuer Zwischenablage und Datei.</summary>
    public string FormatReport(IReadOnlyList<CheckResult> results)
    {
        var builder = new StringBuilder();

        builder.AppendLine("VEHISTRA - SERVERPRUEFUNG");
        builder.AppendLine("===================================");
        builder.AppendLine($"Erstellt am      : {DateTime.Now:dd.MM.yyyy HH:mm:ss}");
        builder.AppendLine($"Computer         : {Environment.MachineName}");
        builder.AppendLine($"Betriebssystem   : {ApplicationPaths.DescribeOperatingSystem()}");
        builder.AppendLine($"Programmversion  : {_versionProvider.Version}");
        builder.AppendLine();

        var problems = results.Count(r => r.State == CheckState.Problem);
        var warnings = results.Count(r => r.State == CheckState.Warning);

        builder.AppendLine(problems == 0 && warnings == 0
            ? "ERGEBNIS: Alle Pruefungen sind in Ordnung."
            : $"ERGEBNIS: {problems} Problem(e), {warnings} Hinweis(e).");
        builder.AppendLine();

        foreach (var group in results.GroupBy(r => r.Group))
        {
            builder.AppendLine(group.Key.ToUpperInvariant());
            builder.AppendLine(new string('-', group.Key.Length));

            foreach (var result in group)
            {
                builder.AppendLine($"[{result.StateCaption}] {result.Name}: {result.Message}");

                if (result.Hints is not null)
                {
                    foreach (var hint in result.Hints)
                    {
                        builder.AppendLine($"    - {hint}");
                    }
                }

                if (!string.IsNullOrWhiteSpace(result.TechnicalDetails))
                {
                    builder.AppendLine($"    ({result.TechnicalDetails})");
                }
            }

            builder.AppendLine();
        }

        builder.AppendLine("Dieser Bericht enthaelt keine Passwoerter und keine personenbezogenen Daten.");
        builder.AppendLine("Support: support@vehistra.dev - LSP Virtual Services, vehistra.dev");

        return builder.ToString();
    }

    private static IEnumerable<DriveInfo> GetFixedDrives()
    {
        try
        {
            return DriveInfo.GetDrives()
                .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
                .ToList();
        }
        catch (Exception)
        {
            return [];
        }
    }

    private static int CountLogFiles()
    {
        try
        {
            return Directory.GetFiles(ApplicationPaths.Logs, "*.log").Length;
        }
        catch (Exception)
        {
            return 0;
        }
    }

    private static async Task<bool> PingAsync(string host, CancellationToken cancellationToken)
    {
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(host, TimeSpan.FromSeconds(3), cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return reply.Status == IPStatus.Success;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static async Task<bool> CanConnectToPortAsync(string host, int port, CancellationToken cancellationToken)
    {
        try
        {
            using var client = new TcpClient();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(4));

            await client.ConnectAsync(host, port, timeout.Token).ConfigureAwait(false);
            return client.Connected;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static string ExtractHost(string server)
    {
        var value = server.Split('\\')[0].Trim();

        // "tcp:SERVER,1433" oder "SERVER,1433" auf den reinen Namen reduzieren.
        if (value.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase))
        {
            value = value[4..];
        }

        var comma = value.IndexOf(',');
        return comma < 0 ? value : value[..comma];
    }

    private static string? ExtractInstance(string server)
    {
        var parts = server.Split('\\');
        return parts.Length > 1 ? parts[1].Trim() : null;
    }

    private static bool IsLocalHost(string host) =>
        host is "." or "(local)" or "localhost" or "127.0.0.1" ||
        host.Equals(Environment.MachineName, StringComparison.OrdinalIgnoreCase);
}
