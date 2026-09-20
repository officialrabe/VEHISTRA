using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Vehistra.Application.Abstractions;
using Vehistra.Application.Services;
using Vehistra.Domain.Exceptions;
using Vehistra.Domain.Security;

namespace Vehistra.Infrastructure.Services;

/// <summary>
/// Holt Updates von den Veroeffentlichungen des Projekts auf GitHub. Das
/// Repository ist oeffentlich, deshalb braucht es keine Anmeldung und keinen
/// Zugangsschluessel.
///
/// Was hier hinausgeht, ist genau eine HTTPS-Anfrage nach der neuesten Version
/// und - auf ausdruecklichen Klick - der Download zweier Dateien. Es wird
/// nichts mitgesendet: keine Fahrzeug- oder Fahrerdaten, keine Kennung des
/// Arbeitsplatzes, kein Name des Betriebs. Der Kopfzeile "User-Agent" ist nur
/// zu entnehmen, dass Vehistra fragt, und in welcher Version - das verlangt
/// die Schnittstelle.
/// </summary>
public sealed class GitHubUpdateSource : IOnlineUpdateSource
{
    /// <summary>Voreingestelltes Repository der Veroeffentlichungen.</summary>
    public const string DefaultRepository = "officialrabe/VEHISTRA";

    /// <summary>Groesse, ab der ein Download abgebrochen wird - 250 MB.</summary>
    public const long MaxDownloadBytes = 250L * 1024 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>Nur diese Gegenstellen werden angesprochen.</summary>
    private static readonly string[] ErlaubteHosts =
    [
        "api.github.com",
        "github.com",
        "objects.githubusercontent.com",
        "release-assets.githubusercontent.com"
    ];

    private readonly HttpClient _http;
    private readonly ISettingsService _settings;
    private readonly ICurrentUserService _currentUser;
    private readonly ApplicationVersionProvider _versionProvider;
    private readonly ILogger<GitHubUpdateSource> _logger;

    public GitHubUpdateSource(
        HttpClient http,
        ISettingsService settings,
        ICurrentUserService currentUser,
        ApplicationVersionProvider versionProvider,
        ILogger<GitHubUpdateSource> logger)
    {
        _http = http;
        _settings = settings;
        _currentUser = currentUser;
        _versionProvider = versionProvider;
        _logger = logger;

        // Hier wird am HttpClient nichts eingestellt. Er ist gemeinsam und
        // langlebig; sobald er die erste Anfrage gesendet hat, wirft jede
        // Aenderung an Timeout oder Kopfzeilen eine Ausnahme. Genau das ist
        // passiert: nach der ersten Updateabfrage liess sich die Seite
        // "Updates" nicht mehr oeffnen, weil dieser Dienst je Aufruf neu
        // entsteht. Kopfzeilen gehen deshalb an die einzelne Anfrage, die
        // Wartezeit steht bei der Registrierung.
    }

    /// <summary>
    /// Baut eine Anfrage mit den Kopfzeilen, die die Schnittstelle verlangt.
    /// Der "User-Agent" nennt nur Programm und Version - sonst nichts.
    /// </summary>
    private HttpRequestMessage Anfrage(string adresse)
    {
        var anfrage = new HttpRequestMessage(HttpMethod.Get, adresse);

        anfrage.Headers.UserAgent.ParseAdd($"Vehistra/{_versionProvider.Version}");
        anfrage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        return anfrage;
    }

    public async Task<OnlineUpdateInfo> CheckAsync(
        string installedVersion,
        CancellationToken cancellationToken = default)
    {
        var repository = await LiesRepositoryAsync(cancellationToken).ConfigureAwait(false);
        var adresse = $"https://api.github.com/repos/{repository}/releases/latest";

        JsonDocument dokument;

        try
        {
            using var anfrage = Anfrage(adresse);
            using var antwort = await _http.SendAsync(anfrage, cancellationToken).ConfigureAwait(false);

            if (!antwort.IsSuccessStatusCode)
            {
                _logger.LogWarning("Die Veroeffentlichungsseite antwortete mit {Status}.", (int)antwort.StatusCode);

                return OnlineUpdateInfo.NichtVerfuegbar(installedVersion,
                    $"Die Veröffentlichungsseite antwortete mit {(int)antwort.StatusCode}. " +
                    "Besteht eine Internetverbindung, und ist das Repository richtig eingetragen?");
            }

            var inhalt = await antwort.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            dokument = JsonDocument.Parse(inhalt);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogWarning(exception, "Die Veroeffentlichungen konnten nicht abgefragt werden.");

            return OnlineUpdateInfo.NichtVerfuegbar(installedVersion,
                "Die Veröffentlichungen konnten nicht abgefragt werden. " +
                "Ohne Internetverbindung bleibt die Updateablage im Firmennetz der Weg.");
        }

        using (dokument)
        {
            var wurzel = dokument.RootElement;

            var schild = Text(wurzel, "tag_name");
            var version = schild?.TrimStart('v', 'V');

            if (string.IsNullOrWhiteSpace(version))
            {
                return OnlineUpdateInfo.NichtVerfuegbar(installedVersion,
                    "Die neueste Veröffentlichung trägt keine lesbare Versionsnummer.");
            }

            var seite = Text(wurzel, "html_url");
            var hinweise = Text(wurzel, "body");
            DateTime? veroeffentlicht = wurzel.TryGetProperty("published_at", out var datum)
                && datum.TryGetDateTime(out var wert) ? wert : null;

            string? paket = null;
            string? pruefsummen = null;

            if (wurzel.TryGetProperty("assets", out var anhaenge) && anhaenge.ValueKind == JsonValueKind.Array)
            {
                foreach (var anhang in anhaenge.EnumerateArray())
                {
                    var name = Text(anhang, "name");
                    var url = Text(anhang, "browser_download_url");

                    if (string.Equals(name, OnlineUpdateInfo.InstallerAssetName, StringComparison.OrdinalIgnoreCase))
                    {
                        paket = url;
                    }
                    else if (string.Equals(name, OnlineUpdateInfo.ChecksumAssetName, StringComparison.OrdinalIgnoreCase))
                    {
                        pruefsummen = url;
                    }
                }
            }

            if (!UpdateVersions.TryCompare(installedVersion, version, out var vergleich))
            {
                return new OnlineUpdateInfo(false, installedVersion, version, hinweise, paket, pruefsummen, seite,
                    veroeffentlicht, "Die Versionsangaben konnten nicht verglichen werden.");
            }

            if (vergleich >= 0)
            {
                return new OnlineUpdateInfo(false, installedVersion, version, hinweise, paket, pruefsummen, seite,
                    veroeffentlicht, $"Die installierte Version {installedVersion} ist aktuell.");
            }

            if (paket is null)
            {
                return new OnlineUpdateInfo(false, installedVersion, version, hinweise, null, pruefsummen, seite,
                    veroeffentlicht,
                    $"Version {version} ist veröffentlicht, enthält aber kein " +
                    $"{OnlineUpdateInfo.InstallerAssetName}. Bitte das Paket von der Veröffentlichungsseite laden.");
            }

            if (pruefsummen is null)
            {
                // Ohne Pruefsummendatei liesse sich der Download nicht pruefen -
                // und ungeprueft wird nichts ausgefuehrt.
                return new OnlineUpdateInfo(false, installedVersion, version, hinweise, paket, null, seite,
                    veroeffentlicht,
                    $"Zur Version {version} fehlt die Datei {OnlineUpdateInfo.ChecksumAssetName}. " +
                    "Ohne Prüfsumme wird kein Paket ausgeführt.");
            }

            return new OnlineUpdateInfo(true, installedVersion, version, hinweise, paket, pruefsummen, seite,
                veroeffentlicht, $"Version {version} steht bereit.");
        }
    }

    public async Task<string> DownloadAsync(
        OnlineUpdateInfo info,
        IProgress<OnlineUpdateProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(info);

        // Der Download endet als Programm mit Administratorrechten. Wer es
        // anstossen darf, entscheidet dasselbe Recht wie beim Start.
        _currentUser.DemandPermission(Permissions.UpdatesManage);

        if (!info.IsUpdateAvailable || info.InstallerUrl is null || info.ChecksumUrl is null)
        {
            throw new BusinessRuleException("Es steht kein vollständiges Updatepaket zum Herunterladen bereit.");
        }

        var ziel = Path.Combine(ApplicationPaths.UserData, "Updates", info.AvailableVersion ?? "neu");
        Directory.CreateDirectory(ziel);

        var paketPfad = Path.Combine(ziel, OnlineUpdateInfo.InstallerAssetName);
        var pruefsummenPfad = Path.Combine(ziel, OnlineUpdateInfo.ChecksumAssetName);

        progress?.Report(new OnlineUpdateProgress(0, null, "Prüfsummen werden geladen"));
        await LadeAsync(info.ChecksumUrl!, pruefsummenPfad, null, cancellationToken).ConfigureAwait(false);

        progress?.Report(new OnlineUpdateProgress(0, null, "Updatepaket wird geladen"));
        await LadeAsync(info.InstallerUrl!, paketPfad, progress, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Updatepaket {Version} heruntergeladen nach {Pfad}.", info.AvailableVersion, paketPfad);

        return paketPfad;
    }

    private async Task LadeAsync(
        string adresse,
        string zielPfad,
        IProgress<OnlineUpdateProgress>? progress,
        CancellationToken cancellationToken)
    {
        PruefeAdresse(adresse);

        using var anfrage = Anfrage(adresse);
        using var antwort = await _http
            .SendAsync(anfrage, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        if (!antwort.IsSuccessStatusCode)
        {
            throw new BusinessRuleException(
                $"Der Download ist fehlgeschlagen (HTTP {(int)antwort.StatusCode}): {Path.GetFileName(zielPfad)}");
        }

        var gesamt = antwort.Content.Headers.ContentLength;

        if (gesamt is > MaxDownloadBytes)
        {
            throw new BusinessRuleException(
                $"Die Datei ist größer als {MaxDownloadBytes / (1024 * 1024)} MB und wird nicht geladen.");
        }

        // Erst neben das Ziel, dann umbenennen: ein abgebrochener Download
        // hinterlaesst so kein halbes Paket, das jemand auszufuehren versucht.
        var vorlaeufig = zielPfad + ".teil";

        await using (var quelle = await antwort.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
        await using (var datei = File.Create(vorlaeufig))
        {
            var puffer = new byte[81920];
            long gelesen = 0;
            int anzahl;

            while ((anzahl = await quelle.ReadAsync(puffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                gelesen += anzahl;

                if (gelesen > MaxDownloadBytes)
                {
                    throw new BusinessRuleException(
                        $"Der Download wurde bei {MaxDownloadBytes / (1024 * 1024)} MB abgebrochen.");
                }

                await datei.WriteAsync(puffer.AsMemory(0, anzahl), cancellationToken).ConfigureAwait(false);
                progress?.Report(new OnlineUpdateProgress(gelesen, gesamt, "Updatepaket wird geladen"));
            }
        }

        File.Move(vorlaeufig, zielPfad, overwrite: true);
    }

    /// <summary>Nur HTTPS und nur die bekannten Gegenstellen.</summary>
    private static void PruefeAdresse(string adresse)
    {
        if (!Uri.TryCreate(adresse, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || !ErlaubteHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase))
        {
            throw new BusinessRuleException($"Diese Adresse wird nicht geladen: {adresse}");
        }
    }

    private async Task<string> LiesRepositoryAsync(CancellationToken cancellationToken)
    {
        var eingetragen = await _settings
            .GetOrDefaultAsync(SettingsKeys.UpdateRepository, DefaultRepository, cancellationToken)
            .ConfigureAwait(false);

        var wert = string.IsNullOrWhiteSpace(eingetragen) ? DefaultRepository : eingetragen.Trim();

        // "besitzer/name" - alles andere koennte eine fremde Adresse sein.
        if (!System.Text.RegularExpressions.Regex.IsMatch(wert, @"^[A-Za-z0-9._-]+/[A-Za-z0-9._-]+$"))
        {
            throw new BusinessRuleException($"Der Eintrag für das Repository ist nicht gültig: {wert}");
        }

        return wert;
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var wert) && wert.ValueKind == JsonValueKind.String
            ? wert.GetString()
            : null;
}
