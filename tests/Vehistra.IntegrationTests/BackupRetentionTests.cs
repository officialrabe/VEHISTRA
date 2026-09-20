using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Vehistra.Application;
using Vehistra.Application.Abstractions;
using Vehistra.Application.Services;
using Vehistra.Infrastructure;
using Vehistra.Infrastructure.Persistence;
using Vehistra.Infrastructure.Persistence.Seeding;
using Vehistra.Infrastructure.Services;

namespace Vehistra.IntegrationTests;

/// <summary>
/// Die Aufbewahrungsdauer der Sicherungen. Loeschen ist endgueltig, deshalb
/// steht hier vor allem, was NICHT geloescht werden darf: fremde Dateien, die
/// neuesten Sicherungen und alles, was vor einer Migration entstanden ist.
/// </summary>
[Collection(VerbindungsdateiCollection.Name)]
public class BackupRetentionTests
{
    private static readonly DateTime Heute = new(2026, 3, 14, 9, 0, 0);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ServiceProvider BuildServices(ServerConnectionSettings settings)
    {
        var services = new ServiceCollection();

        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        services.AddVehistraApplication();
        services.AddSingleton<IClock>(new TestClock(Heute));
        services.AddSingleton<IDocumentStorage, InMemoryDocumentStorage>();
        services.AddVehistraInfrastructure(_ => settings, "1.3.0-test");

        return services.BuildServiceProvider();
    }

    private static async Task<ServiceProvider> BuildSoloAsync(TemporaryDatabaseFile datei, int? aufbewahrungstage)
    {
        var services = BuildServices(datei.Settings);
        services.GetRequiredService<IConnectionSettingsStore>().Save(datei.Settings);

        var db = services.GetRequiredService<VehistraDbContext>();
        await db.Database.MigrateAsync(Token);
        await services.GetRequiredService<DatabaseSeeder>().SeedSystemDataAsync(Token);

        if (aufbewahrungstage is { } tage)
        {
            // Direkt in die Tabelle: der Dienst verlangt eine Anmeldung, und
            // hier geht es nicht um die Berechtigungen.
            var eintrag = await db.SystemSettings
                .FirstOrDefaultAsync(e => e.Key == SettingsKeys.BackupRetentionDays, Token);

            if (eintrag is null)
            {
                db.SystemSettings.Add(new Domain.Entities.SystemSetting
                {
                    Key = SettingsKeys.BackupRetentionDays,
                    Value = tage.ToString(),
                    Category = "Sicherung",
                    DataType = "int"
                });
            }
            else
            {
                eintrag.Value = tage.ToString();
            }

            await db.SaveChangesAsync(Token);
            services.GetRequiredService<ISettingsService>().InvalidateCache();
        }

        return services;
    }

    /// <summary>Legt eine Datei mit dem Namensschema des Programms an.</summary>
    private static string LegeSicherungAn(string verzeichnis, int tageAlt, string art = "Manuell")
    {
        var pfad = Path.Combine(verzeichnis, $"Vehistra_{Heute.AddDays(-tageAlt):yyyyMMdd_HHmmss}_{art}.db");
        File.WriteAllText(pfad, new string('x', 64));
        return pfad;
    }

    [Fact]
    public async Task Ohne_eingestellte_Dauer_wird_nichts_geloescht()
    {
        using var datei = new TemporaryDatabaseFile();
        using var services = await BuildSoloAsync(datei, aufbewahrungstage: null);

        var alte = Enumerable.Range(1, 6)
            .Select(i => LegeSicherungAn(datei.BackupDirectory, 100 + i))
            .ToList();

        var ergebnis = await services.GetRequiredService<IBackupService>()
            .CleanUpAsync(new BackupCleanupRequest { PreviewOnly = false }, Token);

        ergebnis.RetentionDays.ShouldBe(0);
        ergebnis.Candidates.ShouldBeEmpty();
        alte.ShouldAllBe(p => File.Exists(p));
    }

    [Fact]
    public async Task Die_Vorschau_loescht_noch_nichts()
    {
        using var datei = new TemporaryDatabaseFile();
        using var services = await BuildSoloAsync(datei, aufbewahrungstage: 30);

        for (var i = 1; i <= 5; i++)
        {
            LegeSicherungAn(datei.BackupDirectory, 40 + i);
        }

        var vorschau = await services.GetRequiredService<IBackupService>()
            .CleanUpAsync(new BackupCleanupRequest { PreviewOnly = true }, Token);

        vorschau.WasExecuted.ShouldBeFalse();
        vorschau.Candidates.Count.ShouldBe(2, "Fuenf Dateien, drei bleiben immer liegen.");
        vorschau.Candidates.ShouldAllBe(c => !c.IsDeleted);
        Directory.GetFiles(datei.BackupDirectory).Length.ShouldBe(5);
    }

    [Fact]
    public async Task Die_neuesten_Sicherungen_bleiben_auch_wenn_sie_zu_alt_sind()
    {
        using var datei = new TemporaryDatabaseFile();
        using var services = await BuildSoloAsync(datei, aufbewahrungstage: 10);

        // Alle sind aelter als die Aufbewahrungsdauer.
        var pfade = new[] { 20, 30, 40, 50, 60 }
            .Select(tage => LegeSicherungAn(datei.BackupDirectory, tage))
            .ToList();

        var ergebnis = await services.GetRequiredService<IBackupService>()
            .CleanUpAsync(new BackupCleanupRequest { PreviewOnly = false }, Token);

        ergebnis.Candidates.Count(c => c.IsDeleted).ShouldBe(2);
        ergebnis.KeptCount.ShouldBe(BackupRetention.MinimumKept);

        // Die drei neuesten (20, 30, 40 Tage) liegen noch da.
        File.Exists(pfade[0]).ShouldBeTrue();
        File.Exists(pfade[1]).ShouldBeTrue();
        File.Exists(pfade[2]).ShouldBeTrue();
        File.Exists(pfade[3]).ShouldBeFalse();
        File.Exists(pfade[4]).ShouldBeFalse();
    }

    [Fact]
    public async Task Junge_Sicherungen_bleiben_unberuehrt()
    {
        using var datei = new TemporaryDatabaseFile();
        using var services = await BuildSoloAsync(datei, aufbewahrungstage: 30);

        var jung = new[] { 1, 5, 10, 29 }.Select(t => LegeSicherungAn(datei.BackupDirectory, t)).ToList();
        var alt = new[] { 31, 45 }.Select(t => LegeSicherungAn(datei.BackupDirectory, t)).ToList();

        await services.GetRequiredService<IBackupService>()
            .CleanUpAsync(new BackupCleanupRequest { PreviewOnly = false }, Token);

        jung.ShouldAllBe(p => File.Exists(p));
        alt.ShouldAllBe(p => !File.Exists(p));
    }

    [Fact]
    public async Task Sicherungen_vor_einer_Migration_werden_nie_geloescht()
    {
        using var datei = new TemporaryDatabaseFile();
        using var services = await BuildSoloAsync(datei, aufbewahrungstage: 5);

        var vorMigration = LegeSicherungAn(datei.BackupDirectory, 400, "VorMigration");
        var manuell = new[] { 100, 200, 300, 350 }
            .Select(t => LegeSicherungAn(datei.BackupDirectory, t))
            .ToList();

        await services.GetRequiredService<IBackupService>()
            .CleanUpAsync(new BackupCleanupRequest { PreviewOnly = false }, Token);

        File.Exists(vorMigration).ShouldBeTrue("Die Kopie vor einer Migration ist der Rettungsanker.");

        // Von den manuellen bleiben die drei neuesten.
        manuell.Count(File.Exists).ShouldBe(3);
    }

    [Fact]
    public async Task Fremde_Dateien_im_Verzeichnis_bleiben_unberuehrt()
    {
        using var datei = new TemporaryDatabaseFile();
        using var services = await BuildSoloAsync(datei, aufbewahrungstage: 1);

        var fremd = new List<string>();
        foreach (var name in new[]
                 {
                     "wichtig.db", "Vehistra.db", "Vehistra_ohne_Zeitstempel_Manuell.db",
                     "Steuerberater_2019.zip", "Vehistra_20250101_010101_Manuell.txt"
                 })
        {
            var pfad = Path.Combine(datei.BackupDirectory, name);
            File.WriteAllText(pfad, "fremd");
            fremd.Add(pfad);
        }

        // Dazu genug eigene Sicherungen, damit ueberhaupt geloescht wird.
        var eigene = new[] { 10, 20, 30, 40 }
            .Select(t => LegeSicherungAn(datei.BackupDirectory, t))
            .ToList();

        var ergebnis = await services.GetRequiredService<IBackupService>()
            .CleanUpAsync(new BackupCleanupRequest { PreviewOnly = false }, Token);

        fremd.ShouldAllBe(p => File.Exists(p));
        ergebnis.Candidates.Count(c => c.IsDeleted).ShouldBe(1);
        File.Exists(eigene[3]).ShouldBeFalse();
    }

    [Fact]
    public async Task Eine_Sicherung_raeumt_hinterher_selbst_auf()
    {
        using var datei = new TemporaryDatabaseFile();
        using var services = await BuildSoloAsync(datei, aufbewahrungstage: 7);

        var alt = new[] { 30, 60, 90, 120 }
            .Select(t => LegeSicherungAn(datei.BackupDirectory, t))
            .ToList();

        var ergebnis = await services.GetRequiredService<IBackupService>()
            .CreateBackupAsync(new BackupRequest { Kind = "Manuell", VerifyAfterBackup = false },
                cancellationToken: Token);

        ergebnis.IsSuccessful.ShouldBeTrue(ergebnis.Message);
        File.Exists(ergebnis.FilePath!).ShouldBeTrue("Die neue Sicherung darf nie dem Aufraeumen zum Opfer fallen.");

        // Die neue Sicherung zaehlt mit: von den vier alten bleiben zwei.
        alt.Count(File.Exists).ShouldBe(2);
        ergebnis.Message.ShouldContain("entfernt");
    }
}
