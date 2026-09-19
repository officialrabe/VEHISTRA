using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Vehistra.Application.Abstractions;
using Vehistra.Domain.Exceptions;
using Vehistra.Domain.Security;

namespace Vehistra.IntegrationTests;

/// <summary>
/// Erkennung neuer Programmversionen ueber die zentrale Updateablage.
/// Geprueft werden Versionsvergleich, Pflichtupdates, Pruefsummen und die
/// verstaendlichen Meldungen bei fehlender oder unvollstaendiger Ablage.
/// </summary>
public class UpdateDetectionTests : IDisposable
{
    private readonly string _updateDirectory =
        Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "vehistra-updates-" + Guid.NewGuid().ToString("N"))).FullName;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        if (Directory.Exists(_updateDirectory))
        {
            Directory.Delete(_updateDirectory, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>Legt eine Updateablage mit Manifest und Paket an.</summary>
    private string PublishRelease(string version, bool mandatory = false, string? minimumVersion = null)
    {
        var releaseDirectory = Path.Combine(_updateDirectory, version);
        Directory.CreateDirectory(releaseDirectory);

        var installer = Path.Combine(releaseDirectory, "Vehistra-Update.exe");
        File.WriteAllText(installer, $"Testpaket {version}");

        File.WriteAllText(Path.Combine(releaseDirectory, "release-notes.txt"), $"Version {version}\n- Testeintrag");

        var manifest = new
        {
            version,
            minimumVersion = minimumVersion ?? "1.0.0",
            installer = $"{version}/Vehistra-Update.exe",
            releaseNotes = $"{version}/release-notes.txt",
            checksum = Checksum(installer),
            mandatory,
            releasedAt = DateTime.Now
        };

        File.WriteAllText(
            Path.Combine(_updateDirectory, "latest.json"),
            JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));

        return installer;
    }

    private static string Checksum(string path) =>
        System.Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    private static Task<TestDatabase> CreateAsync(string installedVersion) =>
        TestDatabase.CreateAsync(applicationVersion: installedVersion);

    [Fact]
    public async Task Eine_neuere_Version_wird_gemeldet()
    {
        await using var database = await CreateAsync("1.0.0");
        PublishRelease("1.1.0");

        var result = await database.Service<IUpdateService>().CheckForUpdateAsync(_updateDirectory, Token);

        result.IsUpdateAvailable.ShouldBeTrue();
        result.AvailableVersion.ShouldBe("1.1.0");
        result.InstalledVersion.ShouldBe("1.0.0");
        result.InstallerFullPath.ShouldNotBeNull();
    }

    [Fact]
    public async Task Die_gleiche_Version_gilt_als_aktuell()
    {
        await using var database = await CreateAsync("1.1.0");
        PublishRelease("1.1.0");

        var result = await database.Service<IUpdateService>().CheckForUpdateAsync(_updateDirectory, Token);

        result.IsUpdateAvailable.ShouldBeFalse();
        result.Message!.ShouldContain("aktuell");
    }

    [Fact]
    public async Task Eine_aeltere_Version_in_der_Ablage_loest_kein_Update_aus()
    {
        await using var database = await CreateAsync("2.0.0");
        PublishRelease("1.1.0");

        var result = await database.Service<IUpdateService>().CheckForUpdateAsync(_updateDirectory, Token);

        result.IsUpdateAvailable.ShouldBeFalse();
    }

    [Fact]
    public async Task Ein_Pflichtupdate_wird_als_solches_gekennzeichnet()
    {
        await using var database = await CreateAsync("1.0.0");
        PublishRelease("2.0.0", mandatory: true);

        var result = await database.Service<IUpdateService>().CheckForUpdateAsync(_updateDirectory, Token);

        result.IsUpdateAvailable.ShouldBeTrue();
        result.IsMandatory.ShouldBeTrue();
    }

    [Fact]
    public async Task Eine_nicht_erreichbare_Ablage_wird_verstaendlich_gemeldet()
    {
        await using var database = await CreateAsync("1.0.0");

        var result = await database.Service<IUpdateService>()
            .CheckForUpdateAsync(Path.Combine(_updateDirectory, "gibtesnicht"), Token);

        result.IsUpdateAvailable.ShouldBeFalse();
        result.UpdatePathReachable.ShouldBeFalse();
        result.Message!.ShouldContain("nicht erreichbar");
    }

    [Fact]
    public async Task Eine_fehlende_latest_json_wird_verstaendlich_gemeldet()
    {
        await using var database = await CreateAsync("1.0.0");

        var result = await database.Service<IUpdateService>().CheckForUpdateAsync(_updateDirectory, Token);

        result.IsUpdateAvailable.ShouldBeFalse();
        result.Message!.ShouldContain("latest.json");
    }

    [Fact]
    public async Task Eine_kaputte_latest_json_stuerzt_nicht_ab()
    {
        await using var database = await CreateAsync("1.0.0");
        File.WriteAllText(Path.Combine(_updateDirectory, "latest.json"), "{ das ist kein JSON");

        var result = await database.Service<IUpdateService>().CheckForUpdateAsync(_updateDirectory, Token);

        result.IsUpdateAvailable.ShouldBeFalse();
        result.Message.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Ein_fehlendes_Updatepaket_wird_gemeldet()
    {
        await using var database = await CreateAsync("1.0.0");
        var installer = PublishRelease("1.1.0");
        File.Delete(installer);

        var result = await database.Service<IUpdateService>().CheckForUpdateAsync(_updateDirectory, Token);

        result.IsUpdateAvailable.ShouldBeFalse();
        result.Message!.ShouldContain("Vehistra-Update.exe");
    }

    [Fact]
    public async Task Die_Versionshinweise_werden_gelesen()
    {
        await using var database = await CreateAsync("1.0.0");
        PublishRelease("1.1.0");

        var service = database.Service<IUpdateService>();
        var result = await service.CheckForUpdateAsync(_updateDirectory, Token);
        var notes = await service.ReadReleaseNotesAsync(result.Manifest!, _updateDirectory, Token);

        notes.ShouldNotBeNull();
        notes!.ShouldContain("1.1.0");
    }

    [Fact]
    public async Task Eine_richtige_Pruefsumme_aus_dem_Manifest_wird_bestaetigt()
    {
        await using var database = await CreateAsync("1.0.0");
        var installer = PublishRelease("1.1.0");

        var pruefung = await database.Service<IUpdateService>()
            .VerifyInstallerAsync(installer, Checksum(installer), Token);

        pruefung.IsValid.ShouldBeTrue(pruefung.Message);
        pruefung.Source.ShouldBe(ChecksumSource.Manifest);
        pruefung.Actual.ShouldBe(Checksum(installer));
    }

    [Fact]
    public async Task Ohne_Angabe_im_Manifest_wird_die_Pruefsummendatei_verwendet()
    {
        await using var database = await CreateAsync("1.0.0");
        var installer = PublishRelease("1.1.0");

        // So legt CreateRelease.ps1 die Datei ab: neben dem Paket.
        File.WriteAllText(
            Path.Combine(Path.GetDirectoryName(installer)!, "checksums.sha256"),
            $"{Checksum(installer)}  Vehistra-Update.exe\n");

        var pruefung = await database.Service<IUpdateService>()
            .VerifyInstallerAsync(installer, null, Token);

        pruefung.IsValid.ShouldBeTrue(pruefung.Message);
        pruefung.Source.ShouldBe(ChecksumSource.Pruefsummendatei);
    }

    [Fact]
    public async Task Eine_falsche_Pruefsumme_wird_abgelehnt()
    {
        await using var database = await CreateAsync("1.0.0");
        var installer = PublishRelease("1.1.0");

        var pruefung = await database.Service<IUpdateService>()
            .VerifyInstallerAsync(installer, new string('a', 64), Token);

        pruefung.IsValid.ShouldBeFalse();
        pruefung.Message.ShouldContain("stimmt nicht");

        // Die Meldung muss beide Werte nennen, sonst kann niemand nachsehen.
        pruefung.Message.ShouldContain(Checksum(installer));
    }

    [Fact]
    public async Task Ein_nachtraeglich_veraendertes_Paket_wird_erkannt()
    {
        await using var database = await CreateAsync("1.0.0");
        var installer = PublishRelease("1.1.0");
        var service = database.Service<IUpdateService>();

        var ergebnis = await service.CheckForUpdateAsync(_updateDirectory, Token);
        ergebnis.IsUpdateAvailable.ShouldBeTrue();

        // Jemand tauscht das Paket aus, nachdem das Manifest geschrieben wurde.
        await File.WriteAllTextAsync(installer, "etwas ganz anderes", Token);

        var pruefung = await service
            .VerifyInstallerAsync(installer, ergebnis.Manifest!.Checksum, Token);

        pruefung.IsValid.ShouldBeFalse("Ein veraendertes Paket darf nicht als in Ordnung gelten.");
    }

    [Fact]
    public async Task Ohne_jede_Pruefsumme_gilt_das_Paket_als_ungeprueft()
    {
        await using var database = await CreateAsync("1.0.0");
        var installer = PublishRelease("1.1.0");

        var pruefung = await database.Service<IUpdateService>()
            .VerifyInstallerAsync(installer, null, Token);

        pruefung.IsValid.ShouldBeFalse("Ungeprueft heisst abgelehnt.");
        pruefung.Source.ShouldBe(ChecksumSource.Keine);
        pruefung.Message.ShouldContain("keine Pruefsumme");

        // Damit der Verwalter die Angabe nachtragen kann, nennt die Meldung sie.
        pruefung.Message.ShouldContain(Checksum(installer));
    }

    [Fact]
    public async Task Ein_fehlendes_Paket_wird_gemeldet()
    {
        await using var database = await CreateAsync("1.0.0");

        var pruefung = await database.Service<IUpdateService>()
            .VerifyInstallerAsync(Path.Combine(_updateDirectory, "gibtsnicht.exe"), null, Token);

        pruefung.IsValid.ShouldBeFalse();
        pruefung.Message.ShouldContain("nicht gefunden");
    }

    [Fact]
    public async Task Der_Updater_wird_bei_falscher_Pruefsumme_nicht_gestartet()
    {
        await using var database = await CreateAsync("1.0.0");
        database.SignInAsAdministrator();

        var installer = PublishRelease("1.1.0");

        // Der Aufrufer koennte die Pruefung vergessen - deshalb sitzt sie im
        // Dienst. Gestartet wird hier nichts, die Ausnahme kommt vor dem Start.
        var fehler = await Should.ThrowAsync<BusinessRuleException>(() =>
            database.Service<IUpdateService>().LaunchUpdaterAsync(new UpdateLaunchRequest
            {
                InstallerPath = installer,
                TargetVersion = "1.1.0",
                ExpectedChecksum = new string('b', 64)
            }, Token));

        fehler.Message.ShouldContain("stimmt nicht");
    }

    [Fact]
    public async Task Ohne_Recht_zur_Updateverwaltung_startet_der_Updater_nicht()
    {
        await using var database = await CreateAsync("1.0.0");
        database.SignInWith(Permissions.VehicleView);

        var installer = PublishRelease("1.1.0");

        await Should.ThrowAsync<PermissionDeniedException>(() =>
            database.Service<IUpdateService>().LaunchUpdaterAsync(new UpdateLaunchRequest
            {
                InstallerPath = installer,
                TargetVersion = "1.1.0",
                ExpectedChecksum = Checksum(installer)
            }, Token));
    }

    [Fact]
    public async Task Ein_zu_grosser_Versionssprung_wird_erkannt()
    {
        await using var database = await CreateAsync("1.0.0");
        PublishRelease("3.0.0", minimumVersion: "2.0.0");

        var result = await database.Service<IUpdateService>().CheckForUpdateAsync(_updateDirectory, Token);

        result.Message.ShouldNotBeNullOrWhiteSpace();
        result.AvailableVersion.ShouldBe("3.0.0");
    }
}
