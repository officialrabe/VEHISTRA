using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Vehistra.Application.Abstractions;
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

    public async Task<bool> VerifyChecksumAsync(
        string installerPath,
        string checksumFilePath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(installerPath) || !File.Exists(checksumFilePath))
        {
            return false;
        }

        await using var stream = File.OpenRead(installerPath);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        var actual = Convert.ToHexString(hash);

        var content = await File.ReadAllTextAsync(checksumFilePath, cancellationToken).ConfigureAwait(false);
        var fileName = Path.GetFileName(installerPath);

        foreach (var line in content.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Trim().Split([' ', '\t', '*'], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                continue;
            }

            var expected = parts[0].Trim();

            // Entweder steht nur die Pruefsumme in der Datei oder das Format "<hash>  <dateiname>".
            if (parts.Length == 1 || parts[^1].EndsWith(fileName, StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        _logger.LogError("Die Pruefsumme des Updatepakets stimmt nicht ueberein: {Path}", installerPath);
        return false;
    }

    public Task<bool> LaunchUpdaterAsync(UpdateLaunchRequest request, CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.UpdatesManage);

        if (!File.Exists(request.InstallerPath))
        {
            throw new FileNotFoundException(
                $"Das Updatepaket wurde nicht gefunden: {request.InstallerPath}", request.InstallerPath);
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
        return Task.FromResult(process is not null);
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
