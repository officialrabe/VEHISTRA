using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Vehistra.Application;
using Vehistra.Application.Abstractions;
using Vehistra.Domain.Enums;
using Vehistra.Infrastructure;
using Vehistra.Infrastructure.Persistence;
using Vehistra.Infrastructure.Persistence.Seeding;
using Vehistra.Infrastructure.Services;

namespace Vehistra.IntegrationTests;

/// <summary>
/// Der Solo-Platz arbeitet mit einer Datenbankdatei statt mit einem Server.
/// Geprueft wird, dass die Datei wirklich angelegt, migriert, gesichert und
/// zurueckgelesen werden kann - nicht nur, dass der Code uebersetzt.
/// </summary>
[Collection(VerbindungsdateiCollection.Name)]
public class SoloPlatzTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>Baut die Dienste so auf, wie es die Anwendungen zur Laufzeit tun.</summary>
    private static ServiceProvider BuildServices(ServerConnectionSettings settings)
    {
        var services = new ServiceCollection();

        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        services.AddVehistraApplication();
        services.AddSingleton<IClock>(new TestClock(new DateTime(2026, 3, 14, 9, 0, 0)));
        services.AddSingleton<IDocumentStorage, InMemoryDocumentStorage>();
        services.AddVehistraInfrastructure(_ => settings, "0.7.0-test");

        return services.BuildServiceProvider();
    }

    [Fact]
    public void Die_Verbindungszeichenfolge_zeigt_auf_die_Datei()
    {
        using var datei = new TemporaryDatabaseFile();
        using var services = BuildServices(datei.Settings);

        var text = services.GetRequiredService<IConnectionSettingsStore>()
            .BuildConnectionString(datei.Settings);

        text.ShouldContain("Vehistra.db");
        new SqliteConnectionStringBuilder(text).DataSource.ShouldBe(Path.GetFullPath(datei.Path_));
    }

    [Fact]
    public async Task Die_Datenbankdatei_wird_angelegt_und_migriert()
    {
        using var datei = new TemporaryDatabaseFile();
        using var services = BuildServices(datei.Settings);

        var db = services.GetRequiredService<VehistraDbContext>();
        await db.Database.MigrateAsync(Token);

        File.Exists(datei.Path_).ShouldBeTrue();
        (await db.Database.GetPendingMigrationsAsync(Token)).ShouldBeEmpty();

        var tabellen = await DatabaseFacts.GetTableCountAsync(db, DatabaseProvider.Sqlite, Token);
        tabellen.ShouldBeGreaterThan(30);
    }

    [Fact]
    public async Task Stammdaten_und_Fahrzeuge_lassen_sich_in_der_Datei_speichern()
    {
        using var datei = new TemporaryDatabaseFile();
        using var services = BuildServices(datei.Settings);

        var db = services.GetRequiredService<VehistraDbContext>();
        await db.Database.MigrateAsync(Token);
        await services.GetRequiredService<DatabaseSeeder>().SeedSystemDataAsync(Token);

        var statusId = await db.VehicleStatuses.Select(s => s.Id).FirstAsync(Token);

        db.Vehicles.Add(new Domain.Entities.Vehicle
        {
            InternalNumber = "T-01",
            LicensePlate = "FDS-AB 123",
            Manufacturer = "Mercedes-Benz",
            Model = "Vito",
            FuelType = FuelType.Diesel,
            VehicleStatusId = statusId
        });

        await db.SaveChangesAsync(Token);

        (await db.Vehicles.CountAsync(Token)).ShouldBe(1);
    }

    [Fact]
    public async Task Der_Nebenlaeufigkeitsstempel_wird_auch_ohne_rowversion_gesetzt()
    {
        using var datei = new TemporaryDatabaseFile();
        using var services = BuildServices(datei.Settings);

        var db = services.GetRequiredService<VehistraDbContext>();
        await db.Database.MigrateAsync(Token);
        await services.GetRequiredService<DatabaseSeeder>().SeedSystemDataAsync(Token);

        var statusId = await db.VehicleStatuses.Select(s => s.Id).FirstAsync(Token);
        var fahrzeug = new Domain.Entities.Vehicle
        {
            InternalNumber = "T-02",
            Manufacturer = "VW",
            Model = "Caddy",
            VehicleStatusId = statusId
        };

        db.Vehicles.Add(fahrzeug);
        await db.SaveChangesAsync(Token);

        // SQLite kennt kein rowversion; der Stempel kommt vom Interceptor.
        fahrzeug.RowVersion.ShouldNotBeNull();
        fahrzeug.RowVersion!.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Die_Sicherung_erzeugt_eine_lesbare_Kopie_mit_allen_Daten()
    {
        using var datei = new TemporaryDatabaseFile();
        using var services = BuildServices(datei.Settings);

        var store = services.GetRequiredService<IConnectionSettingsStore>();
        store.Save(datei.Settings);

        var db = services.GetRequiredService<VehistraDbContext>();
        await db.Database.MigrateAsync(Token);
        await services.GetRequiredService<DatabaseSeeder>().SeedSystemDataAsync(Token);

        var statusId = await db.VehicleStatuses.Select(s => s.Id).FirstAsync(Token);
        db.Vehicles.Add(new Domain.Entities.Vehicle
        {
            InternalNumber = "T-07",
            LicensePlate = "FDS-XY 789",
            Manufacturer = "Skoda",
            Model = "Octavia",
            VehicleStatusId = statusId
        });
        await db.SaveChangesAsync(Token);

        var ergebnis = await services.GetRequiredService<IBackupService>()
            .CreateBackupAsync(new BackupRequest { Kind = "Manuell", VerifyAfterBackup = true },
                cancellationToken: Token);

        ergebnis.IsSuccessful.ShouldBeTrue(ergebnis.Message);
        ergebnis.IsVerified.ShouldBeTrue(ergebnis.Message);
        ergebnis.FilePath.ShouldNotBeNull();
        File.Exists(ergebnis.FilePath!).ShouldBeTrue();

        // Die Kopie muss den Datensatz tatsächlich enthalten.
        var kopie = new SqliteConnectionStringBuilder
        {
            DataSource = ergebnis.FilePath!,
            Mode = SqliteOpenMode.ReadOnly
        }.ConnectionString;

        await using var verbindung = new SqliteConnection(kopie);
        await verbindung.OpenAsync(Token);

        await using var befehl = verbindung.CreateCommand();
        befehl.CommandText = "SELECT LicensePlate FROM Vehicles WHERE InternalNumber = 'T-07'";

        (await befehl.ExecuteScalarAsync(Token) as string).ShouldBe("FDS-XY 789");
    }

    [Fact]
    public async Task Eine_beschaedigte_Sicherung_wird_erkannt()
    {
        using var datei = new TemporaryDatabaseFile();
        using var services = BuildServices(datei.Settings);

        services.GetRequiredService<IConnectionSettingsStore>().Save(datei.Settings);

        var db = services.GetRequiredService<VehistraDbContext>();
        await db.Database.MigrateAsync(Token);

        var kaputt = Path.Combine(datei.BackupDirectory, "kaputt.db");
        await File.WriteAllTextAsync(kaputt, "das ist keine Datenbank", Token);

        var ergebnis = await services.GetRequiredService<IBackupService>()
            .VerifyBackupAsync(kaputt, Token);

        ergebnis.IsSuccessful.ShouldBeFalse();
    }

    [Fact]
    public async Task Eine_fehlende_Sicherung_wird_erkannt()
    {
        using var datei = new TemporaryDatabaseFile();
        using var services = BuildServices(datei.Settings);

        services.GetRequiredService<IConnectionSettingsStore>().Save(datei.Settings);
        await services.GetRequiredService<VehistraDbContext>().Database.MigrateAsync(Token);

        var ergebnis = await services.GetRequiredService<IBackupService>()
            .VerifyBackupAsync(Path.Combine(datei.BackupDirectory, "gibtesnicht.db"), Token);

        ergebnis.IsSuccessful.ShouldBeFalse();
    }

    [Fact]
    public async Task Die_Verwaltung_erkennt_die_Datei_als_vorhanden_oder_fehlend()
    {
        using var datei = new TemporaryDatabaseFile();
        using var services = BuildServices(datei.Settings);

        var verwaltung = services.GetRequiredService<IDatabaseAdministrationService>();

        (await verwaltung.DatabaseExistsAsync(datei.Settings, Token)).ShouldBeFalse();

        await verwaltung.CreateDatabaseAsync(datei.Settings, Token);
        await services.GetRequiredService<VehistraDbContext>().Database.MigrateAsync(Token);

        (await verwaltung.DatabaseExistsAsync(datei.Settings, Token)).ShouldBeTrue();
    }

    [Fact]
    public async Task Der_Verbindungstest_meldet_die_geoeffnete_Datei()
    {
        using var datei = new TemporaryDatabaseFile();
        using var services = BuildServices(datei.Settings);

        var ergebnis = await services.GetRequiredService<IDatabaseAdministrationService>()
            .TestConnectionAsync(datei.Settings, Token);

        ergebnis.IsSuccessful.ShouldBeTrue(ergebnis.Message);
        ergebnis.TechnicalDetails.ShouldNotBeNull().ShouldContain("SQLite");
    }

    [Fact]
    public async Task Lese_und_Schreibrechte_werden_erkannt()
    {
        using var datei = new TemporaryDatabaseFile();
        using var services = BuildServices(datei.Settings);

        var db = services.GetRequiredService<VehistraDbContext>();
        await db.Database.MigrateAsync(Token);

        var (lesen, schreiben) = await DatabaseFacts
            .GetPermissionsAsync(db, DatabaseProvider.Sqlite, Token);

        lesen.ShouldBeTrue();
        schreiben.ShouldBeTrue();
    }

    [Fact]
    public async Task Die_Groesse_wird_aus_der_Datei_ermittelt()
    {
        using var datei = new TemporaryDatabaseFile();
        using var services = BuildServices(datei.Settings);

        var db = services.GetRequiredService<VehistraDbContext>();
        await db.Database.MigrateAsync(Token);

        var groesse = await DatabaseFacts.GetSizeMegabytesAsync(db, datei.Settings, Token);

        // Waehrend die Verbindung offen ist, stehen die Daten im
        // Write-Ahead-Log neben der .db-Datei. Beides muss mitgezaehlt werden,
        // sonst meldet eine gefuellte Datenbank "0,00 MB".
        groesse.ShouldNotBeNull();
        groesse!.Value.ShouldBeGreaterThan(0.1m);
    }

    [Fact]
    public async Task Die_Systemdiagnose_nennt_Betriebsart_und_Datei_statt_eines_Servers()
    {
        using var datei = new TemporaryDatabaseFile();
        using var services = BuildServices(datei.Settings);

        // Die Diagnose liest die hinterlegte Konfiguration - also erst schreiben.
        services.GetRequiredService<IConnectionSettingsStore>().Save(datei.Settings);

        await services.GetRequiredService<VehistraDbContext>().Database.MigrateAsync(Token);

        var diagnose = services.GetRequiredService<IDiagnosticsService>();
        var bericht = await diagnose.RunAsync(Token);

        bericht.OperatingMode.ShouldNotBeNull().ShouldContain("Solo-Platz");
        bericht.DatabaseFile.ShouldBe(datei.Path_);

        // Ein Solo-Platz hat keinen Server - dann darf auch keiner dastehen.
        bericht.Server.ShouldBeNull();
        bericht.SqlInstance.ShouldBeNull();

        var text = diagnose.FormatForClipboard(bericht);

        text.ShouldContain("Betriebsart");
        text.ShouldContain(datei.Path_);
        text.ShouldNotContain("SQL-Instanz");

        // Die Groesse muss aus Datei und Begleitprotokoll stammen, nicht 0 sein.
        var dateizeile = text.Split('\n').First(z => z.Contains("] Datenbankdatei:"));
        dateizeile.ShouldNotContain("0,00 MB");
        dateizeile.ShouldNotContain("Groesse unbekannt");
    }

    [Fact]
    public void Die_Beschreibung_nennt_beim_Solo_Platz_die_Datei_statt_eines_Servers()
    {
        var solo = new ServerConnectionSettings
        {
            Provider = DatabaseProvider.Sqlite,
            DatabaseFile = @"C:\Vehistra\Vehistra.db"
        };

        solo.IsSingleWorkstation.ShouldBeTrue();
        solo.Describe().ShouldContain("Solo-Platz");

        var netz = new ServerConnectionSettings { Server = "FUHRPARK-SRV01\\SQLEXPRESS" };

        netz.IsSingleWorkstation.ShouldBeFalse();
        netz.Describe().ShouldContain("FUHRPARK-SRV01");
    }
}
