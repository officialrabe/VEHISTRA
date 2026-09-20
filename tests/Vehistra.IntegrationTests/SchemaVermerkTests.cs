using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Vehistra.Application;
using Vehistra.Application.Abstractions;
using Vehistra.Infrastructure;
using Vehistra.Infrastructure.Persistence;
using Vehistra.Infrastructure.Services;

namespace Vehistra.IntegrationTests;

/// <summary>
/// Auf der Seite "Updates" stand unter DATENBANKSCHEMA dauerhaft "unbekannt",
/// und im Supportpaket ebenso. Grund: der Stand wurde nur nach einer
/// tatsaechlich ausgefuehrten Migration vermerkt - eine frisch eingerichtete
/// Datenbank hat aber keine hinter sich. Gemeldet wurde es als
/// Schoenheitsfehler; im Supportfall fehlt damit die Angabe, auf die es
/// ankommt.
/// </summary>
public class SchemaVermerkTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ServiceProvider BuildServices(ServerConnectionSettings settings)
    {
        var services = new ServiceCollection();

        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        services.AddVehistraApplication();
        services.AddSingleton<IClock>(new TestClock(new DateTime(2026, 3, 14, 9, 0, 0)));
        services.AddSingleton<IDocumentStorage, InMemoryDocumentStorage>();
        services.AddVehistraInfrastructure(_ => settings, "1.4.4-test");

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Eine_frisch_eingerichtete_Datenbank_bekommt_ihren_Stand_vermerkt()
    {
        using var datei = new TemporaryDatabaseFile();
        using var services = BuildServices(datei.Settings);

        services.GetRequiredService<IConnectionSettingsStore>().Save(datei.Settings);

        var db = services.GetRequiredService<VehistraDbContext>();
        await db.Database.MigrateAsync(Token);

        // So steht es nach der Einrichtung da: Schema vorhanden, aber nichts vermerkt.
        (await db.DatabaseVersions.CountAsync(Token)).ShouldBe(0);

        var vermerkt = await services.GetRequiredService<IDatabaseAdministrationService>()
            .EnsureSchemaVersionRecordedAsync(new MigrationOptions
            {
                ApplicationVersion = "1.4.4",
                UserName = "einrichtung"
            }, Token);

        vermerkt.ShouldBe("1.4.4");

        var eintrag = await db.DatabaseVersions.AsNoTracking().SingleAsync(Token);

        eintrag.IsCurrent.ShouldBeTrue();
        eintrag.SchemaVersion.ShouldBe("1.4.4");
        eintrag.LastMigration.ShouldNotBeNullOrWhiteSpace();
        eintrag.AppliedByUserName.ShouldBe("einrichtung");
    }

    [Fact]
    public async Task Ein_vorhandener_Vermerk_wird_nicht_ueberschrieben()
    {
        using var datei = new TemporaryDatabaseFile();
        using var services = BuildServices(datei.Settings);

        services.GetRequiredService<IConnectionSettingsStore>().Save(datei.Settings);

        var db = services.GetRequiredService<VehistraDbContext>();
        await db.Database.MigrateAsync(Token);

        var verwaltung = services.GetRequiredService<IDatabaseAdministrationService>();

        await verwaltung.EnsureSchemaVersionRecordedAsync(
            new MigrationOptions { ApplicationVersion = "1.4.3" }, Token);

        var zweiter = await verwaltung.EnsureSchemaVersionRecordedAsync(
            new MigrationOptions { ApplicationVersion = "9.9.9" }, Token);

        // Der Vermerk sagt, womit die Datenbank eingerichtet wurde. Ihn bei
        // jedem Start zu ueberschreiben wuerde genau diese Aussage zerstoeren.
        zweiter.ShouldBe("1.4.3");
        (await db.DatabaseVersions.CountAsync(Token)).ShouldBe(1);
    }

    [Fact]
    public async Task Ein_Migrationslauf_ohne_offene_Aenderungen_traegt_den_Stand_nach()
    {
        using var datei = new TemporaryDatabaseFile();
        using var services = BuildServices(datei.Settings);

        services.GetRequiredService<IConnectionSettingsStore>().Save(datei.Settings);

        var db = services.GetRequiredService<VehistraDbContext>();
        await db.Database.MigrateAsync(Token);

        // Genau der Weg, den ein Administrator hat: "Datenbank aktualisieren"
        // auf einer Datenbank, an der nichts zu tun ist.
        var ergebnis = await services.GetRequiredService<IDatabaseAdministrationService>()
            .MigrateAsync(new MigrationOptions
            {
                CreateBackup = false,
                AbortWhenBackupFails = false,
                ApplicationVersion = "1.4.4",
                UserName = "verwalter"
            }, null, Token);

        ergebnis.IsSuccessful.ShouldBeTrue(ergebnis.ErrorMessage);
        ergebnis.AppliedMigrations.ShouldBeEmpty();
        ergebnis.SchemaVersion.ShouldBe("1.4.4");
    }
}
