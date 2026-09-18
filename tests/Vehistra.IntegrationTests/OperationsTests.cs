using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vehistra.Application.Abstractions;
using Vehistra.Application.Dtos;
using Vehistra.Domain.Enums;
using Vehistra.Infrastructure.Persistence;
using Vehistra.Infrastructure.Services;

namespace Vehistra.IntegrationTests;

/// <summary>Kilometerstaende laufen niemals rueckwaerts - ausser als gekennzeichnete Korrektur.</summary>
public class MileageTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Ein_hoeherer_Kilometerstand_wird_uebernommen()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.SignInAsAdministrator();

        var vehicle = await TestData.AddVehicleAsync(database.Db, mileage: 100_000, cancellationToken: Token);
        var mileage = database.Service<IMileageService>();

        await mileage.AddAsync(vehicle.Id, 110_000, database.Clock.Now, MileageSource.ManuelleEingabe,
            cancellationToken: Token);

        (await mileage.GetCurrentMileageAsync(vehicle.Id, Token)).ShouldBe(110_000);
    }

    [Fact]
    public async Task Ein_niedrigerer_Kilometerstand_wird_abgelehnt()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.SignInAsAdministrator();

        var vehicle = await TestData.AddVehicleAsync(database.Db, mileage: 100_000, cancellationToken: Token);
        var mileage = database.Service<IMileageService>();

        await Should.ThrowAsync<Exception>(() =>
            mileage.AddAsync(vehicle.Id, 90_000, database.Clock.Now, MileageSource.ManuelleEingabe,
                cancellationToken: Token));
    }

    [Fact]
    public async Task Eine_gekennzeichnete_Korrektur_darf_den_Wert_senken()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.SignInAsAdministrator();

        var vehicle = await TestData.AddVehicleAsync(database.Db, mileage: 100_000, cancellationToken: Token);
        var mileage = database.Service<IMileageService>();

        await mileage.AddAsync(vehicle.Id, 90_000, database.Clock.Now, MileageSource.ManuelleEingabe,
            "Tippfehler korrigiert", isCorrection: true, cancellationToken: Token);

        (await mileage.GetCurrentMileageAsync(vehicle.Id, Token)).ShouldBe(90_000);
    }

    [Fact]
    public async Task Jeder_Eintrag_bleibt_in_der_Historie_erhalten()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.SignInAsAdministrator();

        var vehicle = await TestData.AddVehicleAsync(database.Db, mileage: 100_000, cancellationToken: Token);
        var mileage = database.Service<IMileageService>();

        await mileage.AddAsync(vehicle.Id, 110_000, database.Clock.Now, MileageSource.ManuelleEingabe,
            cancellationToken: Token);

        database.Clock.Advance(TimeSpan.FromDays(30));

        await mileage.AddAsync(vehicle.Id, 118_000, database.Clock.Now, MileageSource.Werkstatt,
            cancellationToken: Token);

        var history = await mileage.GetHistoryAsync(vehicle.Id, cancellationToken: Token);

        history.Count.ShouldBe(2);
        history.Select(h => h.Mileage).ShouldContain(110_000);
        history.Select(h => h.Mileage).ShouldContain(118_000);
    }

    [Fact]
    public async Task Der_Kilometerstand_wird_am_Fahrzeug_mitgefuehrt()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.SignInAsAdministrator();

        var vehicle = await TestData.AddVehicleAsync(database.Db, mileage: 100_000, cancellationToken: Token);

        await database.Service<IMileageService>().AddAsync(
            vehicle.Id, 123_456, database.Clock.Now, MileageSource.Tanken, cancellationToken: Token);

        var reloaded = await database.Db.Vehicles.AsNoTracking().SingleAsync(v => v.Id == vehicle.Id, Token);

        reloaded.CurrentMileage.ShouldBe(123_456);
    }
}

/// <summary>Zwei Arbeitsplaetze duerfen die Datenbankstruktur nicht gleichzeitig aendern.</summary>
public class MigrationLockTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Ohne_laufende_Migration_ist_die_Sperre_offen()
    {
        await using var database = await TestDatabase.CreateAsync();

        var state = await database.Service<MigrationLockManager>().GetStateAsync(Token);

        state.IsLocked.ShouldBeFalse();
    }

    [Fact]
    public async Task Die_erste_Sperre_wird_erteilt()
    {
        await using var database = await TestDatabase.CreateAsync();

        var manager = database.Service<MigrationLockManager>();
        await using var handle = await manager.TryAcquireAsync("testadmin", cancellationToken: Token);

        handle.ShouldNotBeNull();
        (await manager.GetStateAsync(Token)).IsLocked.ShouldBeTrue();
    }

    [Fact]
    public async Task Eine_zweite_Sperre_wird_verweigert()
    {
        await using var database = await TestDatabase.CreateAsync();

        using var scopeA = database.Services.CreateScope();
        using var scopeB = database.Services.CreateScope();

        var managerA = scopeA.ServiceProvider.GetRequiredService<MigrationLockManager>();
        var managerB = scopeB.ServiceProvider.GetRequiredService<MigrationLockManager>();

        await using var first = await managerA.TryAcquireAsync("testadmin", cancellationToken: Token);
        first.ShouldNotBeNull();

        var second = await managerB.TryAcquireAsync("testadmin", cancellationToken: Token);
        second.ShouldBeNull();
    }

    [Fact]
    public async Task Nach_dem_Freigeben_ist_die_Sperre_wieder_verfuegbar()
    {
        await using var database = await TestDatabase.CreateAsync();

        var manager = database.Service<MigrationLockManager>();

        var handle = await manager.TryAcquireAsync("testadmin", cancellationToken: Token);
        handle.ShouldNotBeNull();
        await handle!.DisposeAsync();

        (await manager.GetStateAsync(Token)).IsLocked.ShouldBeFalse();

        await using var again = await manager.TryAcquireAsync("testadmin", cancellationToken: Token);
        again.ShouldNotBeNull();
    }

    [Fact]
    public async Task Eine_abgelaufene_Sperre_blockiert_nicht_dauerhaft()
    {
        await using var database = await TestDatabase.CreateAsync();

        var manager = database.Service<MigrationLockManager>();
        await manager.TryAcquireAsync("testadmin", TimeSpan.FromMinutes(5), Token);

        // Der Arbeitsplatz ist abgestuerzt; die Sperre laeuft ab.
        database.Clock.Advance(TimeSpan.FromMinutes(10));

        (await manager.GetStateAsync(Token)).IsLocked.ShouldBeFalse();

        await using var handle = await manager.TryAcquireAsync("testadmin", cancellationToken: Token);
        handle.ShouldNotBeNull();
    }
}
