using Vehistra.Application.Abstractions;
using Vehistra.Application.Dtos;
using Vehistra.Domain.Entities;
using Vehistra.Domain.Enums;

namespace Vehistra.IntegrationTests;

/// <summary>
/// Die Daten hinter der Fahrerakte: alles zu einem Fahrer an einer Stelle.
/// Geprueft wird, dass jede Abfrage wirklich nach dem Fahrer filtert - eine
/// Akte, die fremde Vorgaenge zeigt, waere schlimmer als keine.
/// </summary>
public class DriverFileTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Schaeden_Unfaelle_und_Werkstatt_lassen_sich_je_Fahrer_abfragen()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.SignInAsAdministrator();

        var fahrerA = await TestData.AddDriverAsync(database.Db, "Anna", "Admin", Token);
        var fahrerB = await TestData.AddDriverAsync(database.Db, "Bodo", "Beifahrer", Token);
        var fahrzeug = await TestData.AddVehicleAsync(database.Db, cancellationToken: Token);
        var zweites = await TestData.AddVehicleAsync(database.Db, "T-02", "FDS-CD 456", cancellationToken: Token);

        database.Db.DamageReports.AddRange(
            new DamageReport
            {
                DamageNumber = "SCH-2026-00001", VehicleId = fahrzeug.Id, DriverId = fahrerA.Id,
                OccurredAt = new DateTime(2026, 2, 1), Description = "Kratzer links",
                Status = DamageStatus.Gemeldet
            },
            new DamageReport
            {
                DamageNumber = "SCH-2026-00002", VehicleId = zweites.Id, DriverId = fahrerB.Id,
                OccurredAt = new DateTime(2026, 2, 2), Description = "Delle hinten",
                Status = DamageStatus.Gemeldet
            });

        database.Db.AccidentReports.Add(new AccidentReport
        {
            AccidentNumber = "UNF-2026-00001", VehicleId = fahrzeug.Id, DriverId = fahrerA.Id,
            OccurredAt = new DateTime(2026, 2, 5), Location = "Kreuzung Bahnhofstraße"
        });

        database.Db.WorkshopOrders.AddRange(
            new WorkshopOrder
            {
                OrderNumber = "WS-2026-00001", VehicleId = fahrzeug.Id, DriverId = fahrerA.Id,
                CreatedOn = new DateTime(2026, 2, 6), Status = WorkshopOrderStatus.InBearbeitung
            },
            new WorkshopOrder
            {
                OrderNumber = "WS-2026-00002", VehicleId = zweites.Id, DriverId = fahrerB.Id,
                CreatedOn = new DateTime(2026, 2, 7), Status = WorkshopOrderStatus.Geplant
            });

        await database.Db.SaveChangesAsync(Token);

        var schaeden = await database.Service<IDamageService>()
            .GetListAsync(new DamageFilter { DriverId = fahrerA.Id, OnlyOpen = false }, Token);
        schaeden.Select(d => d.DamageNumber).ShouldBe(["SCH-2026-00001"]);

        var unfaelle = await database.Service<IAccidentService>().GetForDriverAsync(fahrerA.Id, Token);
        unfaelle.Select(u => u.AccidentNumber).ShouldBe(["UNF-2026-00001"]);
        (await database.Service<IAccidentService>().GetForDriverAsync(fahrerB.Id, Token)).ShouldBeEmpty();

        var vorgaenge = await database.Service<IWorkshopService>()
            .GetOrdersAsync(new WorkshopFilter { DriverId = fahrerA.Id, OnlyOpen = false }, Token);
        vorgaenge.Select(v => v.OrderNumber).ShouldBe(["WS-2026-00001"]);
    }

    [Fact]
    public async Task Die_Zuordnungen_eines_Fahrers_zeigen_das_laufende_Fahrzeug()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.SignInAsAdministrator();

        var fahrer = await TestData.AddDriverAsync(database.Db, cancellationToken: Token);
        var erstes = await TestData.AddVehicleAsync(database.Db, cancellationToken: Token);
        var zweites = await TestData.AddVehicleAsync(database.Db, "T-02", "FDS-CD 456", cancellationToken: Token);

        var drivers = database.Service<IDriverService>();

        await drivers.AssignDriverAsync(erstes.Id, fahrer.Id, new DateTime(2026, 1, 1), "erstes Fahrzeug", Token);
        await drivers.AssignDriverAsync(zweites.Id, fahrer.Id, new DateTime(2026, 2, 1), "Wechsel", Token);

        var zuordnungen = await drivers.GetAssignmentsForDriverAsync(fahrer.Id, Token);

        zuordnungen.Count.ShouldBe(2, "Die frühere Zuordnung bleibt als Historie erhalten.");

        // Beide Zuordnungen sind offen, weil es zwei verschiedene Fahrzeuge
        // sind - beendet wird nur die Zuordnung am selben Fahrzeug.
        zuordnungen.ShouldAllBe(z => z.Vehicle != null);
        zuordnungen.First().ValidFrom.ShouldBe(new DateTime(2026, 2, 1));
    }

    [Fact]
    public async Task Ohne_Schadensrecht_bleibt_der_Bereich_in_der_Akte_leer()
    {
        await using var database = await TestDatabase.CreateAsync();

        var fahrer = await TestData.AddDriverAsync(database.Db, cancellationToken: Token);
        var fahrzeug = await TestData.AddVehicleAsync(database.Db, cancellationToken: Token);

        database.Db.DamageReports.Add(new DamageReport
        {
            DamageNumber = "SCH-2026-00003", VehicleId = fahrzeug.Id, DriverId = fahrer.Id,
            OccurredAt = new DateTime(2026, 2, 1), Description = "Steinschlag", Status = DamageStatus.Gemeldet
        });
        await database.Db.SaveChangesAsync(Token);

        // Ein Benutzer ohne Schadensrecht: die Abfrage muss abgewiesen werden,
        // nicht stillschweigend Daten liefern.
        database.SignInWith(Vehistra.Domain.Security.Permissions.DriverView);

        await Should.ThrowAsync<Vehistra.Domain.Exceptions.PermissionDeniedException>(() =>
            database.Service<IDamageService>()
                .GetListAsync(new DamageFilter { DriverId = fahrer.Id, OnlyOpen = false }, Token));

        // Die Zuordnungen selbst darf er sehen.
        await database.Service<IDriverService>().GetAssignmentsForDriverAsync(fahrer.Id, Token);
    }
}
