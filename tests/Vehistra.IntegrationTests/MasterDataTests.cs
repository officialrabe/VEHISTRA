using Microsoft.EntityFrameworkCore;
using Vehistra.Application.Abstractions;
using Vehistra.Domain.Entities;
using Vehistra.Domain.Enums;
using Vehistra.Domain.Exceptions;
using Vehistra.Domain.Security;

namespace Vehistra.IntegrationTests;

/// <summary>
/// Stammdaten, die jeder Fuhrpark anders braucht: Schadenskategorien und
/// Fahrzeugstatus. Wichtig ist dabei, was NICHT gehen darf - an einigen
/// mitgelieferten Eintraegen haengen Ablaeufe im Programm.
/// </summary>
public class MasterDataTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // ----- Schadenskategorien ------------------------------------------------

    [Fact]
    public async Task Eine_eigene_Schadenskategorie_kann_angelegt_und_umbenannt_werden()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.SignInAsAdministrator();

        var damages = database.Service<IDamageService>();

        var kategorie = await damages.CreateCategoryAsync("Hagelschaden", Token);

        kategorie.IsSystemCategory.ShouldBeFalse();
        (await damages.GetCategoriesAsync(cancellationToken: Token))
            .Select(c => c.Name).ShouldContain("Hagelschaden");

        kategorie.Name = "Hagel";
        await damages.UpdateCategoryAsync(kategorie, Token);

        (await damages.GetCategoriesAsync(cancellationToken: Token))
            .Select(c => c.Name).ShouldContain("Hagel");
    }

    [Fact]
    public async Task Eine_doppelte_Schadenskategorie_wird_abgelehnt()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.SignInAsAdministrator();

        var damages = database.Service<IDamageService>();
        var vorhanden = (await damages.GetCategoriesAsync(cancellationToken: Token)).First();

        await Should.ThrowAsync<BusinessRuleException>(() =>
            damages.CreateCategoryAsync(vorhanden.Name.ToUpperInvariant(), Token));
    }

    [Fact]
    public async Task Die_mitgelieferte_Kategorie_Unfall_kann_nicht_umbenannt_werden()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.SignInAsAdministrator();

        var damages = database.Service<IDamageService>();
        var unfall = (await damages.GetCategoriesAsync(true, Token)).First(c => c.Name == "Unfall");

        unfall.IsSystemCategory.ShouldBeTrue();

        unfall.Name = "Karambolage";

        // Das Programm sucht die Kategorie fuer Schaeden aus Unfaellen ueber
        // ihren Namen. Umbenennen wuerde solche Schaeden stillschweigend ohne
        // Kategorie entstehen lassen.
        var fehler = await Should.ThrowAsync<BusinessRuleException>(() =>
            damages.UpdateCategoryAsync(unfall, Token));

        fehler.Message.ShouldContain("Namen");

        (await database.Db.DamageCategories.CountAsync(c => c.Name == "Unfall", Token)).ShouldBe(1);
    }

    [Fact]
    public async Task Die_Schadensmeldung_aus_einem_Unfall_findet_ihre_Kategorie_weiterhin()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.SignInAsAdministrator();

        var damages = database.Service<IDamageService>();
        var unfallKategorie = (await damages.GetCategoriesAsync(true, Token)).First(c => c.Name == "Unfall");

        // Stillgelegt darf sie sein - der Ablauf muss sie trotzdem finden.
        unfallKategorie.IsActive = false;
        await damages.UpdateCategoryAsync(unfallKategorie, Token);

        var fahrzeug = await TestData.AddVehicleAsync(database.Db, cancellationToken: Token);

        var unfall = new AccidentReport
        {
            AccidentNumber = "UNF-2026-00001",
            VehicleId = fahrzeug.Id,
            OccurredAt = new DateTime(2026, 3, 1),
            Location = "Kreuzung Hauptstraße"
        };

        database.Db.AccidentReports.Add(unfall);
        await database.Db.SaveChangesAsync(Token);

        var schadenId = await database.Service<IAccidentService>()
            .CreateDamageFromAccidentAsync(unfall.Id, "Heckschaden", Token);

        var schaden = await database.Db.DamageReports
            .AsNoTracking()
            .FirstAsync(d => d.Id == schadenId, Token);

        schaden.DamageCategoryId.ShouldBe(unfallKategorie.Id);
    }

    [Fact]
    public async Task Eine_zugeordnete_Schadenskategorie_wird_nicht_geloescht()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.SignInAsAdministrator();

        var damages = database.Service<IDamageService>();
        var kategorie = await damages.CreateCategoryAsync("Hagelschaden", Token);
        var fahrzeug = await TestData.AddVehicleAsync(database.Db, cancellationToken: Token);

        database.Db.DamageReports.Add(new DamageReport
        {
            DamageNumber = "SCH-2026-00001",
            VehicleId = fahrzeug.Id,
            DamageCategoryId = kategorie.Id,
            OccurredAt = new DateTime(2026, 3, 1),
            Description = "Dellen im Dach",
            Status = DamageStatus.Gemeldet
        });
        await database.Db.SaveChangesAsync(Token);

        var fehler = await Should.ThrowAsync<BusinessRuleException>(() =>
            damages.DeleteCategoryAsync(kategorie.Id, Token));

        fehler.Message.ShouldContain("Schadensmeldung");
    }

    // ----- Fahrzeugstatus ----------------------------------------------------

    [Fact]
    public async Task Ein_eigener_Status_kann_angelegt_werden_und_hat_keine_Systemzuordnung()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.SignInAsAdministrator();

        var vehicles = database.Service<IVehicleService>();

        var status = await vehicles.CreateStatusAsync("Verleih", Token);

        status.IsSystemStatus.ShouldBeFalse();
        status.Kind.ShouldBeNull("Nur mitgelieferte Status tragen eine Systemzuordnung.");
        status.IsActive.ShouldBeTrue();

        (await vehicles.GetStatusesAsync(cancellationToken: Token))
            .Select(s => s.Name).ShouldContain("Verleih");
    }

    [Fact]
    public async Task Die_Kennzeichen_einsatzbereit_und_verfuegbar_lassen_sich_setzen()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.SignInAsAdministrator();

        var vehicles = database.Service<IVehicleService>();
        var status = await vehicles.CreateStatusAsync("Verleih", Token);

        status.CountsAsOperational = true;
        status.CountsAsAvailable = false;
        await vehicles.UpdateStatusAsync(status, Token);

        var gespeichert = (await vehicles.GetStatusesAsync(true, Token)).First(s => s.Id == status.Id);

        gespeichert.CountsAsOperational.ShouldBeTrue();
        gespeichert.CountsAsAvailable.ShouldBeFalse();
    }

    [Fact]
    public async Task Ein_mitgelieferter_Status_darf_umbenannt_werden_ohne_dass_Ablaeufe_brechen()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.SignInAsAdministrator();

        var vehicles = database.Service<IVehicleService>();
        var ausgemustert = (await vehicles.GetStatusesAsync(true, Token))
            .First(s => s.Kind == VehicleStatusKind.Ausgemustert);

        ausgemustert.Name = "Aus dem Bestand";
        await vehicles.UpdateStatusAsync(ausgemustert, Token);

        var fahrzeug = await TestData.AddVehicleAsync(database.Db, cancellationToken: Token);

        await database.Service<IVehicleLifecycleService>().RetireAsync(new VehicleRetirement
        {
            VehicleId = fahrzeug.Id,
            RetiredAt = new DateTime(2026, 3, 10),
            Reason = RetirementReason.Verkauft
        }, Token);

        var gespeichert = await database.Db.Vehicles.AsNoTracking()
            .FirstAsync(v => v.Id == fahrzeug.Id, Token);

        // Der Ablauf findet den Status ueber seine Systemzuordnung, nicht ueber
        // den Namen - deshalb darf umbenannt werden.
        gespeichert.VehicleStatusId.ShouldBe(ausgemustert.Id);
        gespeichert.IsRetired.ShouldBeTrue();
    }

    [Fact]
    public async Task Eine_Bedeutung_kann_an_einen_eigenen_Status_uebergehen()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.SignInAsAdministrator();

        var vehicles = database.Service<IVehicleService>();
        var bisher = (await vehicles.GetStatusesAsync(true, Token))
            .First(s => s.Kind == VehicleStatusKind.Ausgemustert);
        var eigener = await vehicles.CreateStatusAsync("Aus dem Bestand genommen", Token);

        eigener.Kind = VehicleStatusKind.Ausgemustert;
        await vehicles.UpdateStatusAsync(eigener, Token);

        var alle = await vehicles.GetStatusesAsync(true, Token);

        // Genau ein Status traegt die Bedeutung - der bisherige gibt sie ab.
        alle.Count(s => s.Kind == VehicleStatusKind.Ausgemustert).ShouldBe(1);
        alle.First(s => s.Id == eigener.Id).Kind.ShouldBe(VehicleStatusKind.Ausgemustert);
        alle.First(s => s.Id == bisher.Id).Kind.ShouldBeNull();

        // Und der Ablauf setzt ab jetzt den eigenen Status.
        var fahrzeug = await TestData.AddVehicleAsync(database.Db, cancellationToken: Token);

        await database.Service<IVehicleLifecycleService>().RetireAsync(new VehicleRetirement
        {
            VehicleId = fahrzeug.Id,
            RetiredAt = new DateTime(2026, 4, 2),
            Reason = RetirementReason.Verkauft
        }, Token);

        var gespeichert = await database.Db.Vehicles.AsNoTracking()
            .FirstAsync(v => v.Id == fahrzeug.Id, Token);

        gespeichert.VehicleStatusId.ShouldBe(eigener.Id);
    }

    [Fact]
    public async Task Eine_vergebene_Bedeutung_wird_nicht_entfernt()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.SignInAsAdministrator();

        var vehicles = database.Service<IVehicleService>();
        var werkstatt = (await vehicles.GetStatusesAsync(true, Token))
            .First(s => s.Kind == VehicleStatusKind.Werkstatt);

        var fehler = await Should.ThrowAsync<BusinessRuleException>(() =>
        {
            werkstatt.Kind = null;
            return vehicles.UpdateStatusAsync(werkstatt, Token);
        });

        // Ohne diese Sperre wuerde eine Werkstattbuchung stillschweigend
        // keinen Status mehr setzen.
        fehler.Message.ShouldContain("kann nicht entfernt werden");
    }

    [Fact]
    public async Task Farbe_Beschreibung_und_Reihenfolge_eines_Status_lassen_sich_aendern()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.SignInAsAdministrator();

        var vehicles = database.Service<IVehicleService>();
        var status = await vehicles.CreateStatusAsync("Verleih", Token);

        status.ColorHex = "  #6A1B9A  ";
        status.Description = "Fahrzeug ist an einen Dritten verliehen.";
        status.SortOrder = 5;
        await vehicles.UpdateStatusAsync(status, Token);

        var gespeichert = (await vehicles.GetStatusesAsync(true, Token)).First(s => s.Id == status.Id);

        gespeichert.ColorHex.ShouldBe("#6A1B9A");
        gespeichert.Description.ShouldBe("Fahrzeug ist an einen Dritten verliehen.");
        gespeichert.SortOrder.ShouldBe(5);
    }

    [Fact]
    public async Task Eine_leere_Farbe_wird_als_keine_Farbe_gespeichert()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.SignInAsAdministrator();

        var vehicles = database.Service<IVehicleService>();
        var status = await vehicles.CreateStatusAsync("Verleih", Token);

        status.ColorHex = "   ";
        await vehicles.UpdateStatusAsync(status, Token);

        (await vehicles.GetStatusesAsync(true, Token)).First(s => s.Id == status.Id)
            .ColorHex.ShouldBeNull();
    }

    [Fact]
    public async Task Ein_mitgelieferter_Status_wird_nicht_geloescht()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.SignInAsAdministrator();

        var vehicles = database.Service<IVehicleService>();
        var mitgeliefert = (await vehicles.GetStatusesAsync(true, Token)).First(s => s.Kind is not null);

        var fehler = await Should.ThrowAsync<BusinessRuleException>(() =>
            vehicles.DeleteStatusAsync(mitgeliefert.Id, Token));

        fehler.Message.ShouldContain("stilllegen");
    }

    [Fact]
    public async Task Ein_benutzter_Status_wird_nicht_geloescht()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.SignInAsAdministrator();

        var vehicles = database.Service<IVehicleService>();
        var status = await vehicles.CreateStatusAsync("Verleih", Token);

        var fahrzeug = await TestData.AddVehicleAsync(database.Db, cancellationToken: Token);
        fahrzeug.VehicleStatusId = status.Id;
        await database.Db.SaveChangesAsync(Token);

        var fehler = await Should.ThrowAsync<BusinessRuleException>(() =>
            vehicles.DeleteStatusAsync(status.Id, Token));

        fehler.Message.ShouldContain("Fahrzeug");
    }

    [Fact]
    public async Task Ein_unbenutzter_eigener_Status_kann_geloescht_werden()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.SignInAsAdministrator();

        var vehicles = database.Service<IVehicleService>();
        var status = await vehicles.CreateStatusAsync("Versehen", Token);

        await vehicles.DeleteStatusAsync(status.Id, Token);

        (await database.Db.VehicleStatuses.CountAsync(s => s.Id == status.Id, Token)).ShouldBe(0);
    }

    [Fact]
    public async Task Der_letzte_aktive_Status_kann_nicht_stillgelegt_werden()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.SignInAsAdministrator();

        var vehicles = database.Service<IVehicleService>();
        var alle = await vehicles.GetStatusesAsync(true, Token);

        // Alle bis auf einen stilllegen.
        foreach (var status in alle.Skip(1))
        {
            status.IsActive = false;
            await vehicles.UpdateStatusAsync(status, Token);
        }

        var letzter = alle.First();
        letzter.IsActive = false;

        // Ohne aktiven Status liesse sich kein Fahrzeug mehr anlegen.
        var fehler = await Should.ThrowAsync<BusinessRuleException>(() =>
            vehicles.UpdateStatusAsync(letzter, Token));

        fehler.Message.ShouldContain("letzte aktive Status");

        (await vehicles.GetStatusesAsync(cancellationToken: Token)).Count.ShouldBe(1);
    }

    [Fact]
    public async Task Ohne_Recht_zur_Einstellungsverwaltung_geht_bei_beiden_nichts()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.SignInWith(Permissions.VehicleView, Permissions.DamageView);

        await Should.ThrowAsync<PermissionDeniedException>(() =>
            database.Service<IDamageService>().CreateCategoryAsync("Hagelschaden", Token));

        await Should.ThrowAsync<PermissionDeniedException>(() =>
            database.Service<IVehicleService>().CreateStatusAsync("Verleih", Token));
    }
}
