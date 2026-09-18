using Microsoft.EntityFrameworkCore;
using Vehistra.Domain.Security;

namespace Vehistra.IntegrationTests;

/// <summary>Tests des Datenbankaufbaus und der Stammdaten.</summary>
public class DatabaseSetupTests
{
    [Fact]
    public async Task Die_Datenbank_laesst_sich_aus_dem_Modell_erzeugen()
    {
        await using var database = await TestDatabase.CreateAsync(seedSystemData: false);

        (await database.Db.Database.CanConnectAsync(Token)).ShouldBeTrue();
    }

    [Fact]
    public async Task Die_Stammdaten_enthalten_alle_Standardrollen()
    {
        await using var database = await TestDatabase.CreateAsync();

        var roles = await database.Db.Roles.Select(r => r.Name).ToListAsync(Token);

        foreach (var expected in RoleNames.Defaults)
        {
            roles.ShouldContain(expected.Name);
        }
    }

    [Fact]
    public async Task Die_Stammdaten_enthalten_alle_Berechtigungen()
    {
        await using var database = await TestDatabase.CreateAsync();

        var permissions = await database.Db.Permissions.CountAsync(Token);

        permissions.ShouldBe(Permissions.All.Count);
    }

    [Fact]
    public async Task Die_Administratorrolle_erhaelt_alle_Berechtigungen()
    {
        await using var database = await TestDatabase.CreateAsync();

        var count = await database.Db.RolePermissions
            .CountAsync(rp => rp.Role!.Name == RoleNames.Administrator, Token);

        count.ShouldBe(Permissions.All.Count);
    }

    [Fact]
    public async Task Die_Stammdaten_enthalten_Fahrzeugstatus()
    {
        await using var database = await TestDatabase.CreateAsync();

        (await database.Db.VehicleStatuses.CountAsync(Token)).ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Das_wiederholte_Befuellen_erzeugt_keine_Doppelungen()
    {
        await using var database = await TestDatabase.CreateAsync();

        var seeder = database.Service<Vehistra.Infrastructure.Persistence.Seeding.DatabaseSeeder>();
        await seeder.SeedSystemDataAsync(Token);
        await seeder.SeedSystemDataAsync(Token);

        (await database.Db.Roles.CountAsync(Token)).ShouldBe(RoleNames.Defaults.Count);
        (await database.Db.Permissions.CountAsync(Token)).ShouldBe(Permissions.All.Count);
    }

    [Fact]
    public async Task Ohne_Administratorkonto_meldet_die_Anmeldung_das_ausdruecklich()
    {
        await using var database = await TestDatabase.CreateAsync();

        var authentication = database.Service<Vehistra.Application.Abstractions.IAuthenticationService>();

        (await authentication.HasAnyActiveAdministratorAsync(Token)).ShouldBeFalse();
    }

    [Fact]
    public async Task Das_Anlegen_des_ersten_Administrators_gelingt_genau_einmal()
    {
        await using var database = await TestDatabase.CreateAsync();

        var seeder = database.Service<Vehistra.Infrastructure.Persistence.Seeding.DatabaseSeeder>();
        var authentication = database.Service<Vehistra.Application.Abstractions.IAuthenticationService>();

        await seeder.CreateAdministratorAsync("admin", "Sicher-Passwort-1", "Max", "Muster", null, Token);

        (await authentication.HasAnyActiveAdministratorAsync(Token)).ShouldBeTrue();

        await Should.ThrowAsync<InvalidOperationException>(() =>
            seeder.CreateAdministratorAsync("admin", "Sicher-Passwort-2", "Max", "Muster", null, Token));
    }

    [Fact]
    public async Task Das_Administratorpasswort_wird_niemals_im_Klartext_gespeichert()
    {
        const string password = "Geheim-Passwort-42";

        await using var database = await TestDatabase.CreateAsync();

        await database.Service<Vehistra.Infrastructure.Persistence.Seeding.DatabaseSeeder>()
            .CreateAdministratorAsync("admin", password, "Max", "Muster", null, Token);

        var stored = await database.Db.Users.Select(u => u.PasswordHash).SingleAsync(Token);

        stored.ShouldNotContain(password);
        stored.ShouldStartWith("PBKDF2$");
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;
}
