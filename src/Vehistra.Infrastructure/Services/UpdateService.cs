using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Vehistra.Application.Abstractions;
using Vehistra.Domain.Exceptions;
using Vehistra.Domain.Security;
using Microsoft.Extensions.Logging;

namespace Vehistra.Infrastructure.Services;

/// <summary>
/// Prueft die zentrale Updateablage (latest.json) auf dem Windows Server und
/// startet die Updatekomponente. Es werden keine Daten aus dem Internet geladen.
/// </summary>
public sealed class UpdateService : IUpdateService
{
    public const string ManifestFileName = "latest.json";
    public const string UpdaterFileName = "Vehistra.Updater.exe";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IConnectionSettingsStore _connectionStore;
    private readonly ICurrentUserService _currentUser;
    private readonly ApplicationVersionProvider _versionProvider;
    private readonly ILogger<UpdateService> _logger;

    public UpdateService(
        IConnectionSettingsStore connectionStore,
        ICurrentUserService currentUser,
        ApplicationVersionProvider versionProvider,
        ILogger<UpdateService> logger)
    {
        _connectionStore = connectionStore;
        _currentUser = currentUser;
        _versionProvider = versionProvider;
        _logger = logger;
    }

    public async Task<UpdateCheckResult> CheckForUpdateAsync(
        string? updatePath = null,
        CancellationToken cancellationToken = default)
    {
        var installedVersion = _versionProvider.Version;
        var path = updatePath ?? _connectionStore.Load()?.UpdatePath;

        if (string.IsNullOrWhiteSpace(path))
        {
            return new UpdateCheckResult(false, installedVersion, null, false, null, null,
                "Es ist keine Updateablage konfiguriert.", false);
        }

        if (!Directory.Exists(path))
        {
            _logger.LogWarning("Updateablage nicht erreichbar: {Path}", path);
            return new UpdateCheckResult(false, installedVersion, null, false, null, null,
                $"Die Updateablage '{path}' ist nicht erreichbar.", false);
        }

        var manifestPath = Path.Combine(path, ManifestFileName);
        if (!File.Exists(manifestPath))
        {
            return new UpdateCheckResult(false, installedVersion, null, false, null, null,
                $"In der Updateablage wurde keine Datei '{ManifestFileName}' gefunden.", true);
        }

        UpdateManifest? manifest;

        try
        {
            var json = await File.ReadAllTextAsync(manifestPath, cancellationToken).ConfigureAwait(false);
            manifest = JsonSerializer.Deserialize<UpdateManifest>(json, JsonOptions);
        }
        catch (Exception exception) when (exception is JsonException or IOException)
        {
            _logger.LogError(exception, "latest.json konnte nicht gelesen werden.");
            return new UpdateCheckResult(false, installedVersion, null, false, null, null,
                "Die Datei latest.json konnte nicht gelesen werden.", true);
        }

        if (manifest is null || string.IsNullOrWhiteSpace(manifest.Version))
        {
            return new UpdateCheckResult(false, installedVersion, null, false, null, null,
                "Die Datei latest.json enthaelt keine gueltige Versionsangabe.", true);
        }

        if (!TryCompareVersions(installedVersion, manifest.Version, out var comparison))
        {
            return new UpdateCheckResult(false, installedVersion, manifest.Version, false, manifest, null,
                "Die Versionsangaben konnten nicht verglichen werden.", true);
        }

        if (comparison >= 0)
        {
            return new UpdateCheckResult(false, installedVersion, manifest.Version, false, manifest, null,
                "Die installierte Version ist aktuell.", true);
        }

        var installerPath = Path.Combine(path, manifest.Installer.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(installerPath))
        {
            return new UpdateCheckResult(false, installedVersion, manifest.Version, manifest.Mandatory, manifest, null,
                $"Das Updatepaket '{manifest.Installer}' wurde in der Updateablage nicht gefunden.", true);
        }

        // Ist der Sprung von der installierten Version aus erlaubt?
        if (!string.IsNullOrWhiteSpace(manifest.MinimumVersion)
            && TryCompareVersions(installedVersion, manifest.MinimumVersion, out var minimumComparison)
            && minimumComparison < 0)
        {
            return new UpdateCheckResult(true, installedVersion, manifest.Version, manifest.Mandatory, manifest,
                installerPath,
                $"Fuer dieses Update wird mindestens Version {manifest.MinimumVersion} benoetigt. " +
                "Bitte zuvor die Zwischenversion installieren.", true);
        }

        _logger.LogInformation("Update verfuegbar: {Installed} -> {Available}", installedVersion, manifest.Version);

        return new UpdateCheckResult(true, installedVersion, manifest.Version, manifest.Mandatory, manifest,
            installerPath, null, true);
    }

    public async Task<string?> ReadReleaseNotesAsync(
        UpdateManifest manifest,
        string updatePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(manifest.ReleaseNotes))
        {
            return null;
        }

        var notesPath = Path.Combine(updatePath, manifest.ReleaseNotes.Replace('/', Path.DirectorySeparatorChar));

        if (!File.Exists(notesPath))
        {
            return null;
        }

        return await File.ReadAllTextAsync(notesPath, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Pruefsummenname neben dem Paket, wie CreateRelease.ps1 ihn schreibt.
    /// </summary>
    public const string ChecksumFileName = "checksums.sha256";

    public async Task<InstallerVerification> VerifyInstallerAsync(
        string installerPath,
        string? expectedChecksum,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(installerPath))
        {
            return new InstallerVerification(false, ChecksumSource.Keine, null, null,
                $"Das Updatepaket wurde nicht gefunden: {installerPath}");
        }

        var source = ChecksumSource.Manifest;
        var expected = Normalisiere(expectedChecksum);

        if (expected is null)
        {
            // Zweiter Weg: die Pruefsummendatei, die neben dem Paket liegt.
            expected = await ReadFromChecksumFileAsync(installerPath, cancellationToken).ConfigureAwait(false);
            source = expected is null ? ChecksumSource.Keine : ChecksumSource.Pruefsummendatei;
        }

        string actual;

        try
        {
            await using var stream = File.OpenRead(installerPath);
            var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
            actual = Convert.ToHexString(hash).ToLowerInvariant();
        }
        catch (IOException exception)
        {
            _logger.LogError(exception, "Das Updatepaket konnte nicht gelesen werden: {Path}", installerPath);

            return new InstallerVerification(false, source, expected, null,
                "Das Updatepaket konnte nicht gelesen werden. Liegt es auf einer Netzwerkfreigabe, " +
                "die gerade nicht erreichbar ist?");
        }

        if (expected is null)
        {
            // Ungeprueft heisst abgelehnt: ein Paket, das mit Administratorrechten
            // laeuft, wird nicht auf Zuruf ausgefuehrt.
            _logger.LogError("Zum Updatepaket {Path} gibt es keine erwartete Pruefsumme.", installerPath);

            return new InstallerVerification(false, ChecksumSource.Keine, null, actual,
                "Zu diesem Updatepaket gibt es keine Pruefsumme. Bitte den Eintrag \"checksum\" in " +
                $"latest.json ergaenzen oder die Datei {ChecksumFileName} neben das Paket legen. " +
                $"Die Pruefsumme des vorliegenden Pakets lautet: {actual}");
        }

        if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogError(
                "Die Pruefsumme des Updatepakets stimmt nicht: {Path}. Erwartet {Expected}, vorhanden {Actual}.",
                installerPath, expected, actual);

            return new InstallerVerification(false, source, expected, actual,
                "Die Pruefsumme des Updatepakets stimmt nicht mit der Angabe in der Updateablage ueberein. " +
                "Das Paket wird nicht ausgefuehrt." + Environment.NewLine +
                $"Erwartet: {expected}" + Environment.NewLine +
                $"Vorhanden: {actual}" + Environment.NewLine +
                "Bitte das Paket erneut in die Updateablage kopieren. Bleibt es dabei, wenden Sie sich an " +
                "den Support, bevor Sie es ausfuehren.");
        }

        _logger.LogInformation("Pruefsumme des Updatepakets bestaetigt: {Path}", installerPath);

        return new InstallerVerification(true, source, expected, actual,
            source == ChecksumSource.Manifest
                ? "Die Pruefsumme des Pakets stimmt mit der Angabe in latest.json ueberein."
                : $"Die Pruefsumme des Pakets stimmt mit {ChecksumFileName} ueberein.");
    }

    /// <summary>Liest die erwartete Pruefsumme aus checksums.sha256 neben dem Paket.</summary>
    private static async Task<string?> ReadFromChecksumFileAsync(
        string installerPath,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(installerPath);

        if (string.IsNullOrWhiteSpace(directory))
        {
            return null;
        }

        var checksumFile = Path.Combine(directory, ChecksumFileName);

        if (!File.Exists(checksumFile))
        {
            return null;
        }

        var fileName = Path.GetFileName(installerPath);

        string inhalt;

        try
        {
            inhalt = await File.ReadAllTextAsync(checksumFile, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            return null;
        }

        foreach (var zeile in inhalt.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var teile = zeile.Trim().Split([' ', '\t', '*'], StringSplitOptions.RemoveEmptyEntries);

            if (teile.Length == 0)
            {
                continue;
            }

            // Entweder steht nur die Pruefsumme in der Datei oder "<hash>  <dateiname>".
            if (teile.Length == 1 || teile[^1].EndsWith(fileName, StringComparison.OrdinalIgnoreCase))
            {
                return Normalisiere(teile[0]);
            }
        }

        return null;
    }

    private static string? Normalisiere(string? pruefsumme) =>
        string.IsNullOrWhiteSpace(pruefsumme) ? null : pruefsumme.Trim().ToLowerInvariant();

    public async Task<bool> LaunchUpdaterAsync(
        UpdateLaunchRequest request,
        CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.UpdatesManage);

        if (!File.Exists(request.InstallerPath))
        {
            throw new FileNotFoundException(
                $"Das Updatepaket wurde nicht gefunden: {request.InstallerPath}", request.InstallerPath);
        }

        // Das Paket wird anschliessend mit Administratorrechten ausgefuehrt.
        // Deshalb steht die Pruefung hier und nicht beim Aufrufer: so kann sie
        // niemand vergessen.
        var pruefung = await VerifyInstallerAsync(
            request.InstallerPath, request.ExpectedChecksum, cancellationToken).ConfigureAwait(false);

        if (!pruefung.IsValid)
        {
            throw new BusinessRuleException(pruefung.Message);
        }

        var applicationDirectory = request.ApplicationDirectory ?? ApplicationPaths.InstallDirectory;
        var updaterPath = Path.Combine(applicationDirectory, UpdaterFileName);

        if (!File.Exists(updaterPath))
        {
            throw new FileNotFoundException(
                $"Die Updatekomponente wurde nicht gefunden: {updaterPath}", updaterPath);
        }

        var arguments = new[]
        {
            $"--installer \"{request.InstallerPath}\"",
            $"--version \"{request.TargetVersion}\"",
            $"--appdir \"{applicationDirectory}\"",
            $"--pid {Environment.ProcessId}",
            request.CreateBackupBeforeMigration ? "--backup" : "--no-backup",
            string.IsNullOrWhiteSpace(request.UserName) ? string.Empty : $"--user \"{request.UserName}\""
        };

        var startInfo = new ProcessStartInfo
        {
            FileName = updaterPath,
            Arguments = string.Join(' ', arguments.Where(a => !string.IsNullOrWhiteSpace(a))),
            UseShellExecute = true,
            Verb = "runas",
            WorkingDirectory = applicationDirectory
        };

        _logger.LogInformation("Updater wird gestartet: {Updater}", updaterPath);

        var process = Process.Start(startInfo);
        return process is not null;
    }

    /// <summary>Vergleicht zwei Versionsangaben nach Semantic Versioning.</summary>
    public static bool TryCompareVersions(string left, string right, out int comparison)
    {
        comparison = 0;

        if (!Version.TryParse(Normalize(left), out var leftVersion)
            || !Version.TryParse(Normalize(right), out var rightVersion))
        {
            return false;
        }

        comparison = leftVersion.CompareTo(rightVersion);
        return true;
    }

    private static string Normalize(string version)
    {
        var core = version.Split('-', '+')[0].Trim();
        var parts = core.Split('.');

        return parts.Length switch
        {
            1 => $"{core}.0.0",
            2 => $"{core}.0",
            _ => core
        };
    }
}
