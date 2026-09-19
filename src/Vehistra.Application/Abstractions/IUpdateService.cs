namespace Vehistra.Application.Abstractions;

/// <summary>Prueft die zentrale Updateablage und startet die Updatekomponente.</summary>
public interface IUpdateService
{
    /// <summary>Liest latest.json aus der Updateablage.</summary>
    Task<UpdateCheckResult> CheckForUpdateAsync(
        string? updatePath = null,
        CancellationToken cancellationToken = default);

    Task<string?> ReadReleaseNotesAsync(UpdateManifest manifest, string updatePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Prueft die SHA-256-Pruefsumme des Updatepakets. Erwartet wird der Wert aus
    /// latest.json; fehlt er, wird die Datei checksums.sha256 neben dem Paket
    /// herangezogen. Ohne beides gilt das Paket als ungeprueft.
    /// </summary>
    Task<InstallerVerification> VerifyInstallerAsync(
        string installerPath,
        string? expectedChecksum,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Startet Vehistra.Updater.exe und beendet die Hauptanwendung. Die
    /// Pruefsumme des Pakets wird vorher geprueft; stimmt sie nicht oder fehlt
    /// sie, wird nichts gestartet.
    /// </summary>
    Task<bool> LaunchUpdaterAsync(UpdateLaunchRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Woher die erwartete Pruefsumme stammt.</summary>
public enum ChecksumSource
{
    /// <summary>Keine erwartete Pruefsumme gefunden.</summary>
    Keine = 0,

    /// <summary>Aus latest.json.</summary>
    Manifest = 1,

    /// <summary>Aus checksums.sha256 neben dem Paket.</summary>
    Pruefsummendatei = 2
}

/// <summary>Ergebnis der Pruefsummenpruefung eines Updatepakets.</summary>
public sealed record InstallerVerification(
    bool IsValid,
    ChecksumSource Source,
    string? Expected,
    string? Actual,
    string Message);

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

    /// <summary>
    /// Erwartete SHA-256-Pruefsumme des Pakets, ueblicherweise aus latest.json.
    /// Fehlt sie, sucht der Dienst die Datei checksums.sha256 neben dem Paket.
    /// </summary>
    public string? ExpectedChecksum { get; set; }
}
