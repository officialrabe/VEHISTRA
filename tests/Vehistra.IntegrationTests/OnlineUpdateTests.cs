using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Vehistra.Application.Abstractions;
using Vehistra.Application.Services;
using Vehistra.Domain.Exceptions;
using Vehistra.Infrastructure.Services;

namespace Vehistra.IntegrationTests;

/// <summary>
/// Updates unmittelbar von der Veroeffentlichungsseite. Der einzige Weg des
/// Programms ins Internet - entsprechend steht hier vor allem, was dabei NICHT
/// passieren darf: kein Paket ohne Pruefsumme, keine fremde Adresse, kein
/// Download ohne das Recht, Updates zu verwalten.
/// </summary>
public class OnlineUpdateTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>Beantwortet Anfragen aus einer Tabelle, ohne Netz.</summary>
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, (HttpStatusCode Status, byte[] Body)> _antworten;

        public StubHandler(Dictionary<string, (HttpStatusCode, byte[])> antworten) =>
            _antworten = antworten.ToDictionary(e => e.Key, e => (e.Value.Item1, e.Value.Item2),
                StringComparer.OrdinalIgnoreCase);

        public List<string> Angefragt { get; } = [];

        /// <summary>Der "User-Agent" jeder Anfrage - er muss an der Anfrage haengen, nicht am Client.</summary>
        public List<string> Kennungen { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var adresse = request.RequestUri!.ToString();
            Angefragt.Add(adresse);
            Kennungen.Add(request.Headers.UserAgent.ToString());

            if (!_antworten.TryGetValue(adresse, out var antwort))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }

            return Task.FromResult(new HttpResponseMessage(antwort.Status)
            {
                Content = new ByteArrayContent(antwort.Body)
            });
        }
    }

    private const string ApiAdresse = "https://api.github.com/repos/officialrabe/VEHISTRA/releases/latest";
    private const string PaketAdresse = "https://github.com/officialrabe/VEHISTRA/releases/download/v9.9.9/Vehistra-Update.exe";
    private const string PruefsummenAdresse = "https://github.com/officialrabe/VEHISTRA/releases/download/v9.9.9/checksums.sha256";

    private static string Veroeffentlichung(string version, bool mitPruefsummen = true, string? paketAdresse = null)
    {
        var anhaenge = new List<string>
        {
            $$"""{"name":"Vehistra-Update.exe","browser_download_url":"{{paketAdresse ?? PaketAdresse}}"}"""
        };

        if (mitPruefsummen)
        {
            anhaenge.Add($$"""{"name":"checksums.sha256","browser_download_url":"{{PruefsummenAdresse}}"}""");
        }

        return $$"""
        {
          "tag_name": "v{{version}}",
          "html_url": "https://github.com/officialrabe/VEHISTRA/releases/tag/v{{version}}",
          "body": "Was sich geändert hat.",
          "published_at": "2026-09-19T20:32:03Z",
          "assets": [{{string.Join(",", anhaenge)}}]
        }
        """;
    }

    private static GitHubUpdateSource Quelle(TestDatabase database, StubHandler handler) =>
        Quelle(database, new HttpClient(handler));

    private static GitHubUpdateSource Quelle(TestDatabase database, HttpClient client) =>
        new(client,
            database.Service<ISettingsService>(),
            database.Service<ICurrentUserService>(),
            new ApplicationVersionProvider("1.3.0"),
            NullLogger<GitHubUpdateSource>.Instance);

    [Fact]
    public async Task Eine_neuere_Veroeffentlichung_wird_gefunden()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.SignInAsAdministrator();

        var handler = new StubHandler(new()
        {
            [ApiAdresse] = (HttpStatusCode.OK, Encoding.UTF8.GetBytes(Veroeffentlichung("9.9.9")))
        });

        var ergebnis = await Quelle(database, handler).CheckAsync("1.3.0", Token);

        ergebnis.IsUpdateAvailable.ShouldBeTrue(ergebnis.Message);
        ergebnis.AvailableVersion.ShouldBe("9.9.9");
        ergebnis.InstallerUrl.ShouldBe(PaketAdresse);
        ergebnis.ChecksumUrl.ShouldBe(PruefsummenAdresse);
        ergebnis.ReleaseNotes.ShouldNotBeNull().ShouldContain("geändert");
    }

    [Fact]
    public async Task Die_gleiche_Version_gilt_als_aktuell()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.SignInAsAdministrator();

        var handler = new StubHandler(new()
        {
            [ApiAdresse] = (HttpStatusCode.OK, Encoding.UTF8.GetBytes(Veroeffentlichung("1.3.0")))
        });

        var ergebnis = await Quelle(database, handler).CheckAsync("1.3.0", Token);

        ergebnis.IsUpdateAvailable.ShouldBeFalse();
        ergebnis.Message.ShouldContain("aktuell");
    }

    [Fact]
    public async Task Ohne_Pruefsummendatei_wird_kein_Update_angeboten()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.SignInAsAdministrator();

        var handler = new StubHandler(new()
        {
            [ApiAdresse] = (HttpStatusCode.OK,
                Encoding.UTF8.GetBytes(Veroeffentlichung("9.9.9", mitPruefsummen: false)))
        });

        var ergebnis = await Quelle(database, handler).CheckAsync("1.3.0", Token);

        // Ungeprueft wird nichts ausgefuehrt - dann gar nicht erst anbieten.
        ergebnis.IsUpdateAvailable.ShouldBeFalse();
        ergebnis.Message.ShouldContain("Prüfsumme");
    }

    [Fact]
    public async Task Das_heruntergeladene_Paket_wird_gegen_seine_Pruefsumme_geprueft()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.SignInAsAdministrator();

        var paket = Encoding.UTF8.GetBytes("Das ist nicht wirklich eine EXE, reicht aber zum Rechnen.");
        var pruefsumme = Convert.ToHexString(SHA256.HashData(paket)).ToLowerInvariant();

        var handler = new StubHandler(new()
        {
            [ApiAdresse] = (HttpStatusCode.OK, Encoding.UTF8.GetBytes(Veroeffentlichung("9.9.9"))),
            [PaketAdresse] = (HttpStatusCode.OK, paket),
            [PruefsummenAdresse] = (HttpStatusCode.OK,
                Encoding.UTF8.GetBytes($"{pruefsumme} *Vehistra-Update.exe\n"))
        });

        var quelle = Quelle(database, handler);
        var info = await quelle.CheckAsync("1.3.0", Token);

        var pfad = await quelle.DownloadAsync(info, cancellationToken: Token);

        try
        {
            File.Exists(pfad).ShouldBeTrue();

            var pruefung = await database.Service<IUpdateService>().VerifyInstallerAsync(pfad, null, Token);

            pruefung.IsValid.ShouldBeTrue(pruefung.Message);
            pruefung.Source.ShouldBe(ChecksumSource.Pruefsummendatei);
        }
        finally
        {
            var ordner = Path.GetDirectoryName(pfad);
            if (ordner is not null && Directory.Exists(ordner))
            {
                Directory.Delete(ordner, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Ein_veraendertes_Paket_faellt_auf()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.SignInAsAdministrator();

        var handler = new StubHandler(new()
        {
            [ApiAdresse] = (HttpStatusCode.OK, Encoding.UTF8.GetBytes(Veroeffentlichung("9.9.9"))),
            [PaketAdresse] = (HttpStatusCode.OK, Encoding.UTF8.GetBytes("ausgetauschtes Paket")),
            // Pruefsumme eines anderen Inhalts.
            [PruefsummenAdresse] = (HttpStatusCode.OK, Encoding.UTF8.GetBytes(
                Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("das Original"))).ToLowerInvariant()
                + " *Vehistra-Update.exe\n"))
        });

        var quelle = Quelle(database, handler);
        var info = await quelle.CheckAsync("1.3.0", Token);
        var pfad = await quelle.DownloadAsync(info, cancellationToken: Token);

        try
        {
            var pruefung = await database.Service<IUpdateService>().VerifyInstallerAsync(pfad, null, Token);

            pruefung.IsValid.ShouldBeFalse();
            pruefung.Message.ShouldContain("stimmt nicht");
        }
        finally
        {
            var ordner = Path.GetDirectoryName(pfad);
            if (ordner is not null && Directory.Exists(ordner))
            {
                Directory.Delete(ordner, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Eine_fremde_Adresse_wird_nicht_geladen()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.SignInAsAdministrator();

        const string fremd = "https://beispiel.invalid/Vehistra-Update.exe";

        var handler = new StubHandler(new()
        {
            [ApiAdresse] = (HttpStatusCode.OK,
                Encoding.UTF8.GetBytes(Veroeffentlichung("9.9.9", paketAdresse: fremd))),
            [fremd] = (HttpStatusCode.OK, Encoding.UTF8.GetBytes("boesartig")),
            [PruefsummenAdresse] = (HttpStatusCode.OK, Encoding.UTF8.GetBytes("00 *Vehistra-Update.exe\n"))
        });

        var quelle = Quelle(database, handler);
        var info = await quelle.CheckAsync("1.3.0", Token);

        var fehler = await Should.ThrowAsync<BusinessRuleException>(() =>
            quelle.DownloadAsync(info, cancellationToken: Token));

        fehler.Message.ShouldContain("beispiel.invalid");
        handler.Angefragt.ShouldNotContain(fremd);
    }

    [Fact]
    public async Task Ohne_das_Recht_Updates_zu_verwalten_wird_nichts_geladen()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.SignInAsAdministrator();

        var handler = new StubHandler(new()
        {
            [ApiAdresse] = (HttpStatusCode.OK, Encoding.UTF8.GetBytes(Veroeffentlichung("9.9.9"))),
            [PaketAdresse] = (HttpStatusCode.OK, Encoding.UTF8.GetBytes("Paket")),
            [PruefsummenAdresse] = (HttpStatusCode.OK, Encoding.UTF8.GetBytes("00 *Vehistra-Update.exe\n"))
        });

        var quelle = Quelle(database, handler);
        var info = await quelle.CheckAsync("1.3.0", Token);

        // Suchen darf jeder, herunterladen nur, wer Updates verwalten darf.
        database.SignInWith(Vehistra.Domain.Security.Permissions.VehicleView);

        await Should.ThrowAsync<PermissionDeniedException>(() =>
            quelle.DownloadAsync(info, cancellationToken: Token));
    }

    [Fact]
    public async Task Ein_ungueltiger_Repository_Eintrag_wird_abgewiesen()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.SignInAsAdministrator();

        await database.Service<ISettingsService>()
            .SetAsync(SettingsKeys.UpdateRepository, "https://beispiel.invalid/pfad", Token);

        var quelle = Quelle(database, new StubHandler([]));

        var fehler = await Should.ThrowAsync<BusinessRuleException>(() => quelle.CheckAsync("1.3.0", Token));

        fehler.Message.ShouldContain("nicht gültig");
    }

    /// <summary>
    /// Der Dienst entsteht bei jedem Aufruf der Seite "Updates" neu, die
    /// Verbindung lebt dagegen so lange wie das Programm. Wer in ihr etwas
    /// einstellt, sobald sie gesendet hat, bekommt eine Ausnahme - und die
    /// Seite liess sich danach nicht mehr oeffnen. Deshalb hier zweimal
    /// derselbe Client.
    /// </summary>
    [Fact]
    public async Task Zwei_Aufrufe_teilen_sich_eine_Verbindung()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.SignInAsAdministrator();

        var handler = new StubHandler(new()
        {
            [ApiAdresse] = (HttpStatusCode.OK, Encoding.UTF8.GetBytes(Veroeffentlichung("9.9.9")))
        });

        using var verbindung = new HttpClient(handler);

        var erste = await Quelle(database, verbindung).CheckAsync("1.3.0", Token);
        erste.IsUpdateAvailable.ShouldBeTrue(erste.Message);

        var zweite = await Quelle(database, verbindung).CheckAsync("1.3.0", Token);
        zweite.IsUpdateAvailable.ShouldBeTrue(zweite.Message);

        // Die Kennung haengt an der Anfrage, also traegt sie jede Anfrage.
        handler.Kennungen.Count.ShouldBe(2);
        handler.Kennungen.ShouldAllBe(k => k == "Vehistra/1.3.0");
    }
}
