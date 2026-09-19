namespace Vehistra.Application.Abstractions;

/// <summary>Prueft die zentrale Updateablage und startet die Updatekomponente.</summary>
public interface IUpdateService
{
    /// <summary>Liest latest.json aus der Updateablage.</summary>
    Task<UpdateCheckResult> CheckForUpdateAsync(
        string? updatePath = null,
        CancellationToken cancellationToken = default);

    Task<string?> ReadReleaseNotesAsync(UpdateManifest manifest, string updatePath, CancellationToken cancellationToken = default);

    /// <summary>Prueft die SHA-256-Pruefsumme des Installationspakets.</summary>
    Task<bool> VerifyChecksumAsync(string installerPath, string checksumFilePath, CancellationToken cancellationToken = default);

    /// <summary>Startet Vehistra.Updater.exe und beendet die Hauptanwendung.</summary>
    Task<bool> LaunchUpdaterAsync(UpdateLaunchRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Inhalt der Datei latest.json.</summary>
public sealed class UpdateManifest
{
    public string Version { get; set; } = string.Empty;

    /// <summary>Aelteste Version, die direkt auf diese Version aktualisieren darf.</summary>
    public string? MinimumVersion { get; set; }

    /// <summary>Relativer Pfad zum Update-Installer innerhalb der Updateablage.</summary>
    public string Installer { get; set; } = string.Empty;

    public string? ReleaseNotes { get; set; }

    public string? Checksum { get; set; }

    public bool Mandatory { get; set; }

    public DateTime? ReleasedAt { get; set; }

    /// <summary>Mindestens erforderliche Datenbankschemaversion.</summary>
    public string? MinimumDatabaseVersion { get; set; }
}

public sealed record UpdateCheckResult(
    bool IsUpdateAvailable,
    string InstalledVersion,
    string? AvailableVersion,
    bool IsMandatory,
    UpdateManifest? Manifest,
    string? InstallerFullPath,
    string? Message,
    bool UpdatePathReachable);

public sealed class UpdateLaunchRequest
{
    public string InstallerPath { get; set; } = string.Empty;

    public string TargetVersion { get; set; } = string.Empty;

    public string? ApplicationDirectory { get; set; }

    public bool CreateBackupBeforeMigration { get; set; } = true;

    public string? UserName { get; set; }
}
