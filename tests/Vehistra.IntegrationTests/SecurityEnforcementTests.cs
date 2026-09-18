using Microsoft.EntityFrameworkCore;
using Vehistra.Application.Abstractions;
using Vehistra.Application.Dtos;
using Vehistra.Domain.Enums;
using Vehistra.Domain.Exceptions;
using Vehistra.Domain.Security;

namespace Vehistra.IntegrationTests;

/// <summary>
/// Die Rechtepruefung muss in den Diensten stattfinden - nicht nur durch das
/// Ausblenden von Schaltflaechen in der Oberflaeche.
/// </summary>
public class PermissionEnforcementTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Ohne_Anmeldung_verweigert_der_Fahrzeugdienst_das_Anlegen()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.SignOut();

        var vehicles = database.Service<IVehicleService>();

        await Should.ThrowAsync<PermissionDeniedException>(() =>
            vehicles.CreateAsync(new Domain.Entities.Vehicle
            {
                InternalNumber = "T-99",
                Manufacturer = "Test",
                Model = "Test",
                VehicleStatusId = 1
            }, [], Token));
    }

    [Fact]
    public async Task Ohne_passende_Berechtigung_verweigert_der_Fahrzeugdienst_das_Anlegen()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.SignInWith(Permissions.VehicleView);

        var vehicles = database.Service<IVehicleService>();

        await Should.ThrowAsync<PermissionDeniedException>(() =>
            vehicles.CreateAsync(new Domain.Entities.Vehicle
            {
                InternalNumber = "T-99",
                Manufacturer = "Test",
                Model = "Test",
                VehicleStatusId = 1
            }, [], Token));
    }

    [Fact]
    public async Task Mit_Berechtigung_gelingt_das_Anlegen()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.SignInAsAdministrator();

        var statusId = await database.Db.VehicleStatuses.Select(s => s.Id).FirstAsync(Token);
        var vehicles = database.Service<IVehicleService>();

        var id = await vehicles.CreateAsync(new Domain.Entities.Vehicle
        {
            InternalNumber = "T-99",
            LicensePlate = "FDS-ZZ 999",
            Manufacturer = "Mercedes-Benz",
            Model = "Vito",
            VehicleStatusId = statusId
        }, [], Token);

        id.ShouldBeGreaterThan(0);
        (await database.Db.Vehicles.CountAsync(Token)).ShouldBe(1);
    }

    [Fact]
    public async Task Ohne_Leseberechtigung_verweigert_der_Fahrzeugdienst_die_Liste()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.SignInWith(Permissions.DriverView);

        var vehicles = database.Service<IVehicleService>();

        await Should.ThrowAsync<PermissionDeniedException>(() =>
            vehicles.GetListAsync(new VehicleFilter(), Token));
    }

    [Fact]
    public async Task Ohne_Berechtigung_darf_kein_Kilometerstand_erfasst_werden()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.SignInAsAdministrator();
        var vehicle = await TestData.AddVehicleAsync(database.Db, cancellationToken: Token);

        database.SignInWith(Permissions.VehicleView);
        var mileage = database.Service<IMileageService>();

        await Should.ThrowAsync<PermissionDeniedException>(() =>
            mileage.AddAsync(vehicle.Id, 110_000, database.Clock.Now, MileageSource.ManuelleEingabe,
                cancellationToken: Token));
    }

    [Fact]
    public async Task Ein_normaler_Mitarbeiter_darf_das_Pruefprotokoll_nicht_lesen()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.SignInWith(Permissions.VehicleView);

        var audit = database.Service<IAuditService>();

        await Should.ThrowAsync<PermissionDeniedException>(() =>
            audit.GetAsync(new AuditFilter(), Token));
    }

    [Fact]
    public void DemandPermission_bleibt_still_wenn_das_Recht_vorhanden_ist()
    {
        var currentUser = new Application.Security.CurrentUserService();

        currentUser.SetUser(new CurrentUser(
            1, "test", "Test", "Nutzer",
            [RoleNames.Mitarbeiter],
            [Permissions.VehicleView],
            false));

        Should.NotThrow(() => currentUser.DemandPermission(Permissions.VehicleView));
        currentUser.HasPermission(Permissions.VehicleView).ShouldBeTrue();
        currentUser.HasPermission(Permissions.VehicleEdit).ShouldBeFalse();
    }

    [Fact]
    public void Die_Rechtepruefung_ignoriert_Gross_und_Kleinschreibung()
    {
        var currentUser = new Application.Security.CurrentUserService();

        currentUser.SetUser(new CurrentUser(
            1, "test", "Test", "Nutzer", [], [Permissions.VehicleView], false));

        currentUser.HasPermission(Permissions.VehicleView.ToUpperInvariant()).ShouldBeTrue();
    }
}
