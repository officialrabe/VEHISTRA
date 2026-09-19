namespace Vehistra.Application.Abstractions;

/// <summary>
/// Holt Updates unmittelbar von den Veroeffentlichungen des Projekts, statt
/// aus einer Updateablage im Firmennetz. Gedacht fuer den Solo-Platz und fuer
/// kleine Installationen, in denen niemand Pakete auf eine Freigabe kopiert.
///
/// Das ist die einzige Stelle im Programm, die von sich aus ins Internet
/// greift. Sie ist deshalb abschaltbar und im Auslieferungszustand aus:
/// <see cref="Services.SettingsKeys.UpdateSource"/> steht auf "Ablage".
/// Uebertragen wird nur die Anfrage selbst - keine Fahrzeug-, Fahrer- oder
/// Betriebsdaten, keine Kennung des Arbeitsplatzes.
/// </summary>
public interface IOnlineUpdateSource
{
    /// <summary>Fragt die neueste veroeffentlichte Version ab.</summary>
    Task<OnlineUpdateInfo> CheckAsync(string installedVersion, CancellationToken cancellationToken = default);

    /// <summary>
    /// Laedt Updatepaket und Pruefsummendatei herunter und gibt den Pfad des
    /// Pakets zurueck. Die Pruefsumme wird anschliessend von
    /// <see cref="IUpdateService.VerifyInstallerAsync"/> geprueft - ungeprueft
    /// wird nichts gestartet.
    /// </summary>
    Task<string> DownloadAsync(
        OnlineUpdateInfo info,
        IProgress<OnlineUpdateProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

/// <summary>Fortschritt eines Downloads, fuer die Anzeige.</summary>
public sealed record OnlineUpdateProgress(long ReceivedBytes, long? TotalBytes, string Step)
{
    public int? Percent => TotalBytes is > 0
        ? (int)Math.Clamp(ReceivedBytes * 100 / TotalBytes.Value, 0, 100)
        : null;
}

/// <summary>Was die Veroeffentlichungsseite ueber die neueste Version sagt.</summary>
public sealed record OnlineUpdateInfo(
    bool IsUpdateAvailable,
    string InstalledVersion,
    string? AvailableVersion,
    string? ReleaseNotes,
    string? InstallerUrl,
    string? ChecksumUrl,
    string? PageUrl,
    DateTime? PublishedAt,
    string Message)
{
    /// <summary>Name des Pakets, wie es der Releaseablauf veroeffentlicht.</summary>
    public const string InstallerAssetName = "Vehistra-Update.exe";

    /// <summary>Datei mit den Pruefsummen aller veroeffentlichten Dateien.</summary>
    public const string ChecksumAssetName = "checksums.sha256";

    public static OnlineUpdateInfo NichtVerfuegbar(string installedVersion, string message) =>
        new(false, installedVersion, null, null, null, null, null, null, message);
}
