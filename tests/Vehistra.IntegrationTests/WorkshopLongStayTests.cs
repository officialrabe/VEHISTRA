using Vehistra.Application.Abstractions;
using Vehistra.Application.Services;
using Vehistra.Domain.Entities;
using Vehistra.Domain.Enums;

namespace Vehistra.IntegrationTests;

/// <summary>
/// Die Langzeitwarnung der Werkstatt. Wie lange ein Fahrzeug dort stehen darf,
/// bevor es auffaellt, entscheidet der Betrieb - vorher waren sieben Tage fest
/// im Code. Wichtig ist, dass der Ueberblick und die Werkstattliste dieselbe
/// Frist verwenden: sonst zeigt ein Klick auf die Zahl eine andere Menge.
/// </summary>
public class WorkshopLongStayTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<WorkshopOrder> LegeVorgangAnAsync(
        TestDatabase database,
        string internalNumber,
        int tageInWerkstatt)
    {
        var fahrzeug = await TestData.AddVehicleAsync(
            database.Db, internalNumber, $"FDS-WS {internalNumber}", cancellationToken: Token);

        var vorgang = new WorkshopOrder
        {
            OrderNumber = $"WS-2026-{internalNumber}",
            VehicleId = fahrzeug.Id,
            CreatedOn = database.Clock.Today.AddDays(-tageInWerkstatt - 1),
            Status = WorkshopOrderStatus.InBearbeitung,
            VehicleHandedOverAt = database.Clock.Today.AddDays(-tageInWerkstatt)
        };

        database.Db.WorkshopOrders.Add(vorgang);
        await database.Db.SaveChangesAsync(Token);

        return vorgang;
    }

    [Fact]
    public async Task Der_Ueberblick_zaehlt_Fahrzeuge_ab_der_eingestellten_Frist()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.SignInAsAdministrator();

        await database.Service<ISettingsService>().SetAsync(SettingsKeys.WorkshopLongStayWarnDays, "5", Token);

        await LegeVorgangAnAsync(database, "L-01", 10);
        await LegeVorgangAnAsync(database, "L-02", 5);
        await LegeVorgangAnAsync(database, "L-03", 4);

        var daten = await database.Service<IDashboardService>().GetAsync(Token);

        daten.Workshop.LongStayWarnDays.ShouldBe(5);
        daten.Workshop.LongStay.ShouldBe(2, "Genau die Vorgaenge ab fuenf Tagen.");

        // Ab der doppelten Frist ist der Hinweis kritisch, davor eine Warnung.
        var hinweise = daten.AttentionItems
            .Where(a => a.Category == NotificationCategory.Werkstatt)
            .ToList();

        hinweise.Count.ShouldBe(2);
        hinweise.Count(a => a.Level == WarningLevel.Kritisch).ShouldBe(1);
        hinweise.Count(a => a.Level == WarningLevel.BaldFaellig).ShouldBe(1);
    }

    [Fact]
    public async Task Die_Werkstattliste_filtert_nach_derselben_Frist()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.SignInAsAdministrator();

        await LegeVorgangAnAsync(database, "L-01", 10);
        await LegeVorgangAnAsync(database, "L-02", 3);

        var workshop = database.Service<IWorkshopService>();

        var lange = await workshop.GetOrdersAsync(new WorkshopFilter { MinDaysInWorkshop = 5 }, Token);
        lange.Select(o => o.OrderNumber).ShouldBe(["WS-2026-L-01"]);

        var sehrLange = await workshop.GetOrdersAsync(new WorkshopFilter { MinDaysInWorkshop = 30 }, Token);
        sehrLange.ShouldBeEmpty();

        var alle = await workshop.GetOrdersAsync(new WorkshopFilter(), Token);
        alle.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Ohne_Einstellung_gilt_eine_Woche()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.SignInAsAdministrator();

        await LegeVorgangAnAsync(database, "L-01", 7);
        await LegeVorgangAnAsync(database, "L-02", 6);

        var daten = await database.Service<IDashboardService>().GetAsync(Token);

        daten.Workshop.LongStayWarnDays.ShouldBe(7);
        daten.Workshop.LongStay.ShouldBe(1);
    }
}
