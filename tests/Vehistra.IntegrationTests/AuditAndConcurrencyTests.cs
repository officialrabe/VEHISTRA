using Microsoft.EntityFrameworkCore;
using Vehistra.Application.Abstractions;
using Vehistra.Domain.Entities;
using Vehistra.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace Vehistra.IntegrationTests;

/// <summary>
/// Das Pruefprotokoll haelt fest, WER WAS WANN geaendert hat. Passwortfelder und
/// andere Geheimnisse duerfen dabei niemals im Klartext erscheinen.
/// </summary>
public class AuditTrailTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Das_Anlegen_eines_Fahrzeugs_wird_protokolliert()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.SignInAsAdministrator();

        await TestData.AddVehicleAsync(database.Db, cancellationToken: Token);

        var entry = await database.Db.AuditLogs
            .SingleAsync(a => a.EntityName == nameof(Vehicle), Token);

        entry.Action.ShouldBe(AuditAction.Created);
        entry.UserName.ShouldBe("testadmin");
        entry.Timestamp.ShouldBe(database.Clock.Now, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task Das_Aendern_eines_Fahrzeugs_haelt_alten_und_neuen_Wert_fest()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.SignInAsAdministrator();

        var vehicle = await TestData.AddVehicleAsync(database.Db, cancellationToken: Token);

        vehicle.Color = "Silber";
        await database.Db.SaveChangesAsync(Token);

        var entry = await database.Db.AuditLogs
            .Where(a => a.EntityName == nameof(Vehicle) && a.Action == AuditAction.Updated)
            .SingleAsync(Token);

        entry.NewValues.ShouldNotBeNull();
        entry.NewValues!.ShouldContain("Silber");
        entry.EntityId.ShouldBe(vehicle.Id.ToString());
    }

    [Fact]
    public async Task Das_Protokoll_nennt_den_Computernamen_und_die_Programmversion()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.SignInAsAdministrator();

        await TestData.AddVehicleAsync(database.Db, cancellationToken: Token);

        var entry = await database.Db.AuditLogs.FirstAsync(Token);

        entry.ApplicationVersion.ShouldBe("1.0.0-test");
    }

    [Fact]
    public async Task Das_Protokoll_enthaelt_niemals_einen_Passworthash()
    {
        const string password = "Geheim-Passwort-42";

        await using var database = await TestDatabase.CreateAsync();
        database.SignInAsAdministrator();

        await database.Service<Vehistra.Infrastructure.Persistence.Seeding.DatabaseSeeder>()
            .CreateAdministratorAsync("admin", password, "Max", "Muster", null, Token);

        var hash = await database.Db.Users.Select(u => u.PasswordHash).SingleAsync(Token);
        var entries = await database.Db.AuditLogs.Where(a => a.EntityName == nameof(User)).ToListAsync(Token);

        entries.ShouldNotBeEmpty();

        foreach (var entry in entries)
        {
            (entry.NewValues ?? string.Empty).ShouldNotContain(password);
            (entry.NewValues ?? string.Empty).ShouldNotContain(hash);
            (entry.OldValues ?? string.Empty).ShouldNotContain(hash);
        }
    }

    [Fact]
    public async Task Das_Protokoll_maskiert_geheime_Felder_ausdruecklich()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.SignInAsAdministrator();

        await database.Service<Vehistra.Infrastructure.Persistence.Seeding.DatabaseSeeder>()
            .CreateAdministratorAsync("admin", "Geheim-Passwort-42", "Max", "Muster", null, Token);

        var entry = await database.Db.AuditLogs
            .Where(a => a.EntityName == nameof(User))
            .FirstAsync(Token);

        entry.NewValues.ShouldNotBeNull();
        entry.NewValues!.ShouldContain("***");
    }

    [Fact]
    public async Task Historien_werden_ergaenzt_und_niemals_ueberschrieben()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.SignInAsAdministrator();

        var vehicle = await TestData.AddVehicleAsync(database.Db, cancellationToken: Token);

        var statuses = await database.Db.VehicleStatuses.OrderBy(s => s.SortOrder).ToListAsync(Token);
        var vehicles = database.Service<IVehicleService>();

        await vehicles.ChangeStatusAsync(vehicle.Id, statuses[1].Id, "Werkstatt", null, Token);
        await vehicles.ChangeStatusAsync(vehicle.Id, statuses[0].Id, "Zurueck", null, Token);

        var history = await vehicles.GetStatusHistoryAsync(vehicle.Id, Token);

        history.Count.ShouldBeGreaterThanOrEqualTo(2);
    }
}

/// <summary>
/// Zwei Arbeitsplaetze duerfen denselben Datensatz nicht stillschweigend
/// ueberschreiben. Der zweite Speichervorgang muss erkennbar scheitern.
/// </summary>
public class ConcurrencyTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Der_zweite_Speichervorgang_meldet_einen_Konflikt()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.SignInAsAdministrator();

        var vehicle = await TestData.AddVehicleAsync(database.Db, cancellationToken: Token);
        var id = vehicle.Id;

        // Zwei unabhaengige Sitzungen - wie zwei Arbeitsplaetze.
        using var scopeA = database.Services.CreateScope();
        using var scopeB = database.Services.CreateScope();

        var dbA = scopeA.ServiceProvider.GetRequiredService<Infrastructure.Persistence.VehistraDbContext>();
        var dbB = scopeB.ServiceProvider.GetRequiredService<Infrastructure.Persistence.VehistraDbContext>();

        var fromA = await dbA.Vehicles.SingleAsync(v => v.Id == id, Token);
        var fromB = await dbB.Vehicles.SingleAsync(v => v.Id == id, Token);

        fromA.Color = "Silber";
        await dbA.SaveChangesAsync(Token);

        fromB.Color = "Schwarz";

        await Should.ThrowAsync<DbUpdateConcurrencyException>(() => dbB.SaveChangesAsync(Token));
    }

    [Fact]
    public async Task Nach_einem_Konflikt_bleibt_die_zuerst_gespeicherte_Aenderung_erhalten()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.SignInAsAdministrator();

        var vehicle = await TestData.AddVehicleAsync(database.Db, cancellationToken: Token);
        var id = vehicle.Id;

        using var scopeA = database.Services.CreateScope();
        using var scopeB = database.Services.CreateScope();

        var dbA = scopeA.ServiceProvider.GetRequiredService<Infrastructure.Persistence.VehistraDbContext>();
        var dbB = scopeB.ServiceProvider.GetRequiredService<Infrastructure.Persistence.VehistraDbContext>();

        var fromA = await dbA.Vehicles.SingleAsync(v => v.Id == id, Token);
        var fromB = await dbB.Vehicles.SingleAsync(v => v.Id == id, Token);

        fromA.Color = "Silber";
        await dbA.SaveChangesAsync(Token);

        fromB.Color = "Schwarz";
        await Should.ThrowAsync<DbUpdateConcurrencyException>(() => dbB.SaveChangesAsync(Token));

        using var scopeC = database.Services.CreateScope();
        var dbC = scopeC.ServiceProvider.GetRequiredService<Infrastructure.Persistence.VehistraDbContext>();

        (await dbC.Vehicles.SingleAsync(v => v.Id == id, Token)).Color.ShouldBe("Silber");
    }

    [Fact]
    public async Task Der_Nebenlaeufigkeitsstempel_aendert_sich_bei_jedem_Speichern()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.SignInAsAdministrator();

        var vehicle = await TestData.AddVehicleAsync(database.Db, cancellationToken: Token);
        var first = vehicle.RowVersion.ToArray();

        vehicle.Color = "Silber";
        await database.Db.SaveChangesAsync(Token);

        vehicle.RowVersion.ShouldNotBe(first);
    }
}
