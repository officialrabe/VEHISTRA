using Microsoft.EntityFrameworkCore;
using Vehistra.Application.Abstractions;
using Vehistra.Domain.Entities;
using Vehistra.Domain.Exceptions;
using Vehistra.Domain.Security;

namespace Vehistra.IntegrationTests;

/// <summary>
/// Einsatzbereiche (Fahrzeugkategorien) muessen sich im Betrieb erweitern lassen.
/// Die mitgelieferten Bereiche passen nicht zu jedem Fuhrpark - wer einen
/// "Winterdienst" fuehrt, soll ihn anlegen koennen, ohne den Quellcode anzufassen.
/// </summary>
public class VehicleCategoryTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Ein_eigener_Einsatzbereich_kann_angelegt_werden()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.SignInAsAdministrator();

        var vehicles = database.Service<IVehicleService>();

        var angelegt = await vehicles.CreateCategoryAsync("Winterdienst", "#0288D1", Token);

        angelegt.Id.ShouldBeGreaterThan(0);
        angelegt.IsActive.ShouldBeTrue();
        angelegt.IsSystemCategory.ShouldBeFalse("Selbst angelegte Bereiche sind keine mitgelieferten.");

        var bereiche = await vehicles.GetCategoriesAsync(cancellationToken: Token);

        bereiche.Select(c => c.Name).ShouldContain("Winterdienst");

        // Hinten einsortiert, damit die mitgelieferte Reihenfolge erhalten bleibt.
        angelegt.SortOrder.ShouldBeGreaterThan(bereiche.Where(c => c.IsSystemCategory).Max(c => c.SortOrder));
    }

    [Fact]
    public async Task Ein_neuer_Einsatzbereich_steht_sofort_am_Fahrzeug_zur_Auswahl()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.SignInAsAdministrator();

        var vehicles = database.Service<IVehicleService>();
        var bereich = await vehicles.CreateCategoryAsync("Winterdienst", cancellationToken: Token);

        var fahrzeug = await TestData.AddVehicleAsync(database.Db, cancellationToken: Token);

        database.Db.VehicleCategoryAssignments.Add(new VehicleCategoryAssignment
        {
            VehicleId = fahrzeug.Id,
            VehicleCategoryId = bereich.Id,
            IsPrimary = true
        });
        await database.Db.SaveChangesAsync(Token);

        var zugeordnet = await vehicles.GetCategoryIdsAsync(fahrzeug.Id, Token);

        zugeordnet.ShouldContain(bereich.Id);
    }

    [Fact]
    public async Task Ein_doppelter_Name_wird_abgelehnt()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.SignInAsAdministrator();

        var vehicles = database.Service<IVehicleService>();
        await vehicles.CreateCategoryAsync("Winterdienst", cancellationToken: Token);

        // Auch in anderer Schreibweise - zwei gleich benannte Bereiche waeren
        // in jeder Auswahlliste ein Ratespiel.
        var fehler = await Should.ThrowAsync<BusinessRuleException>(() =>
            vehicles.CreateCategoryAsync("winterdienst", cancellationToken: Token));

        fehler.Message.ShouldContain("bereits vorhanden");
    }

    [Fact]
    public async Task Ein_leerer_Name_wird_abgelehnt()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.SignInAsAdministrator();

        await Should.ThrowAsync<BusinessRuleException>(() =>
            database.Service<IVehicleService>().CreateCategoryAsync("   ", cancellationToken: Token));
    }

    [Fact]
    public async Task Ein_Einsatzbereich_kann_umbenannt_werden()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.SignInAsAdministrator();

        var vehicles = database.Service<IVehicleService>();
        var bereich = await vehicles.CreateCategoryAsync("Winterdinst", cancellationToken: Token);

        bereich.Name = "Winterdienst";
        await vehicles.UpdateCategoryAsync(bereich, Token);

        var bereiche = await vehicles.GetCategoriesAsync(cancellationToken: Token);

        bereiche.Select(c => c.Name).ShouldContain("Winterdienst");
        bereiche.Select(c => c.Name).ShouldNotContain("Winterdinst");
    }

    [Fact]
    public async Task Ein_stillgelegter_Einsatzbereich_verschwindet_aus_der_Auswahl_bleibt_aber_erreichbar()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.SignInAsAdministrator();

        var vehicles = database.Service<IVehicleService>();
        var bereich = await vehicles.CreateCategoryAsync("Winterdienst", cancellationToken: Token);

        bereich.IsActive = false;
        await vehicles.UpdateCategoryAsync(bereich, Token);

        (await vehicles.GetCategoriesAsync(cancellationToken: Token))
            .Select(c => c.Name).ShouldNotContain("Winterdienst");

        // Sonst liesse sich ein stillgelegter Bereich nie wieder einschalten.
        (await vehicles.GetCategoriesAsync(true, Token))
            .Select(c => c.Name).ShouldContain("Winterdienst");
    }

    [Fact]
    public async Task Ein_unbenutzter_eigener_Einsatzbereich_kann_geloescht_werden()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.SignInAsAdministrator();

        var vehicles = database.Service<IVehicleService>();
        var bereich = await vehicles.CreateCategoryAsync("Versehen", cancellationToken: Token);

        await vehicles.DeleteCategoryAsync(bereich.Id, Token);

        (await database.Db.VehicleCategories.CountAsync(c => c.Id == bereich.Id, Token)).ShouldBe(0);
    }

    [Fact]
    public async Task Ein_mitgelieferter_Einsatzbereich_wird_nicht_geloescht()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.SignInAsAdministrator();

        var vehicles = database.Service<IVehicleService>();
        var mitgeliefert = (await vehicles.GetCategoriesAsync(true, Token)).First(c => c.IsSystemCategory);

        var fehler = await Should.ThrowAsync<BusinessRuleException>(() =>
            vehicles.DeleteCategoryAsync(mitgeliefert.Id, Token));

        fehler.Message.ShouldContain("stilllegen");

        (await database.Db.VehicleCategories.CountAsync(c => c.Id == mitgeliefert.Id, Token)).ShouldBe(1);
    }

    [Fact]
    public async Task Ein_zugeordneter_Einsatzbereich_wird_nicht_geloescht()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.SignInAsAdministrator();

        var vehicles = database.Service<IVehicleService>();
        var bereich = await vehicles.CreateCategoryAsync("Winterdienst", cancellationToken: Token);
        var fahrzeug = await TestData.AddVehicleAsync(database.Db, cancellationToken: Token);

        database.Db.VehicleCategoryAssignments.Add(new VehicleCategoryAssignment
        {
            VehicleId = fahrzeug.Id,
            VehicleCategoryId = bereich.Id
        });
        await database.Db.SaveChangesAsync(Token);

        var fehler = await Should.ThrowAsync<BusinessRuleException>(() =>
            vehicles.DeleteCategoryAsync(bereich.Id, Token));

        fehler.Message.ShouldContain("Fahrzeug");

        (await database.Db.VehicleCategories.CountAsync(c => c.Id == bereich.Id, Token)).ShouldBe(1);
    }

    [Fact]
    public async Task Ohne_Recht_zur_Einstellungsverwaltung_geht_nichts()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.SignInWith(Permissions.VehicleView, Permissions.VehicleEdit);

        var vehicles = database.Service<IVehicleService>();

        await Should.ThrowAsync<PermissionDeniedException>(() =>
            vehicles.CreateCategoryAsync("Winterdienst", cancellationToken: Token));

        var vorhanden = (await vehicles.GetCategoriesAsync(true, Token)).First();

        await Should.ThrowAsync<PermissionDeniedException>(() =>
            vehicles.UpdateCategoryAsync(vorhanden, Token));

        await Should.ThrowAsync<PermissionDeniedException>(() =>
            vehicles.DeleteCategoryAsync(vorhanden.Id, Token));
    }
}
