using Vehistra.Application.Abstractions;
using Vehistra.Application.Dtos;

namespace Vehistra.IntegrationTests;

/// <summary>
/// Die Filter der Fahrzeugliste. Sie tragen ab 1.2.0 doppelt: in der Liste
/// selbst und hinter den Kennzahlen des Dashboards, die mit genau diesen
/// Filtern öffnen.
/// </summary>
public class VehicleFilterTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>Drei Fahrzeuge, die sich in den geprueften Merkmalen unterscheiden.</summary>
    private static async Task<TestDatabase> BestandAsync()
    {
        var database = await TestDatabase.CreateAsync();
        database.SignInAsAdministrator();

        var mercedes = await TestData.AddVehicleAsync(database.Db, "T-01", "FDS-AA 111",
            cancellationToken: Token);
        var skoda = await TestData.AddVehicleAsync(database.Db, "T-02", "FDS-BB 222",
            cancellationToken: Token);
        var vw = await TestData.AddVehicleAsync(database.Db, "T-03", "FDS-CC 333",
            cancellationToken: Token);

        var fahrer = await TestData.AddDriverAsync(database.Db, cancellationToken: Token);

        skoda.Manufacturer = "Skoda";
        vw.Manufacturer = "Volkswagen";

        // T-01: fester Fahrer, angemeldet, HU vor zehn Tagen abgelaufen.
        mercedes.CurrentDriverId = fahrer.Id;
        mercedes.NextInspectionDue = new DateTime(2026, 3, 4);

        // T-02: kein Fahrer, abgemeldet, HU in 20 Tagen.
        skoda.CurrentDriverId = null;
        skoda.IsRegistered = false;
        skoda.NextInspectionDue = new DateTime(2026, 4, 3);

        // T-03: kein Fahrer, angemeldet, HU in 100 Tagen.
        vw.CurrentDriverId = null;
        vw.NextInspectionDue = new DateTime(2026, 6, 22);

        await database.Db.SaveChangesAsync(Token);

        return database;
    }

    private static async Task<IReadOnlyList<string>> NummernAsync(TestDatabase database, VehicleFilter filter)
    {
        var ergebnis = await database.Service<IVehicleService>().GetListAsync(filter, Token);
        return ergebnis.Items.Select(v => v.InternalNumber).OrderBy(n => n).ToList();
    }

    [Fact]
    public async Task Nach_Hersteller_wird_gefiltert()
    {
        await using var database = await BestandAsync();

        (await NummernAsync(database, new VehicleFilter { Manufacturer = "Skoda" }))
            .ShouldBe(["T-02"]);
    }

    [Fact]
    public async Task Die_Herstellerliste_kommt_aus_dem_Bestand()
    {
        await using var database = await BestandAsync();

        var hersteller = await database.Service<IVehicleService>().GetManufacturersAsync(Token);

        hersteller.ShouldContain("Skoda");
        hersteller.ShouldContain("Volkswagen");
        hersteller.ShouldBe(hersteller.OrderBy(h => h).ToList(), "Die Liste muss sortiert sein.");
        hersteller.Distinct().Count().ShouldBe(hersteller.Count, "Keine Doppelten.");
    }

    [Fact]
    public async Task Fahrzeuge_mit_und_ohne_festen_Fahrer_lassen_sich_trennen()
    {
        await using var database = await BestandAsync();

        (await NummernAsync(database, new VehicleFilter { HasDriver = true })).ShouldBe(["T-01"]);
        (await NummernAsync(database, new VehicleFilter { HasDriver = false })).ShouldBe(["T-02", "T-03"]);

        // Ohne Angabe bleiben alle sichtbar.
        (await NummernAsync(database, new VehicleFilter())).Count.ShouldBe(3);
    }

    [Fact]
    public async Task Angemeldete_und_abgemeldete_lassen_sich_trennen()
    {
        await using var database = await BestandAsync();

        (await NummernAsync(database, new VehicleFilter { IsRegistered = true })).ShouldBe(["T-01", "T-03"]);
        (await NummernAsync(database, new VehicleFilter { IsRegistered = false })).ShouldBe(["T-02"]);
    }

    [Fact]
    public async Task Abgelaufene_Hauptuntersuchung_wird_gefunden()
    {
        await using var database = await BestandAsync();

        // Testzeitpunkt ist der 14.03.2026; T-01 war am 04.03. faellig.
        (await NummernAsync(database, new VehicleFilter { OnlyInspectionExpired = true }))
            .ShouldBe(["T-01"]);
    }

    [Fact]
    public async Task Die_Fristenstufen_greifen_gestaffelt()
    {
        await using var database = await BestandAsync();

        // In 30 Tagen: das abgelaufene und das in 20 Tagen faellige.
        (await NummernAsync(database, new VehicleFilter { InspectionDueWithinDays = 30 }))
            .ShouldBe(["T-01", "T-02"]);

        // In 120 Tagen: alle drei.
        (await NummernAsync(database, new VehicleFilter { InspectionDueWithinDays = 120 }))
            .Count.ShouldBe(3);
    }

    [Fact]
    public async Task Mehrere_Filter_wirken_zusammen()
    {
        await using var database = await BestandAsync();

        // Ohne festen Fahrer UND angemeldet: nur T-03.
        (await NummernAsync(database, new VehicleFilter { HasDriver = false, IsRegistered = true }))
            .ShouldBe(["T-03"]);

        // Ein Filter, der nichts trifft, liefert nichts - nicht alles.
        (await NummernAsync(database, new VehicleFilter { Manufacturer = "Skoda", HasDriver = true }))
            .ShouldBeEmpty();
    }
}
