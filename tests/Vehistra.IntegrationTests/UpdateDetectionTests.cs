using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Vehistra.Application.Abstractions;

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
    public async Task Eine_richtige_Pruefsumme_wird_bestaetigt()
    {
        await using var database = await CreateAsync("1.0.0");
        var installer = PublishRelease("1.1.0");

        var checksumFile = Path.Combine(_updateDirectory, "checksums.sha256");
        File.WriteAllText(checksumFile, $"{Checksum(installer)}  1.1.0/Vehistra-Update.exe\n");

        var ok = await database.Service<IUpdateService>()
            .VerifyChecksumAsync(installer, checksumFile, Token);

        ok.ShouldBeTrue();
    }

    [Fact]
    public async Task Eine_falsche_Pruefsumme_wird_abgelehnt()
    {
        await using var database = await CreateAsync("1.0.0");
        var installer = PublishRelease("1.1.0");

        var checksumFile = Path.Combine(_updateDirectory, "checksums.sha256");
        File.WriteAllText(checksumFile,
            $"{new string('a', 64)}  1.1.0/Vehistra-Update.exe\n");

        var ok = await database.Service<IUpdateService>()
            .VerifyChecksumAsync(installer, checksumFile, Token);

        ok.ShouldBeFalse();
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
