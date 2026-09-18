using Microsoft.EntityFrameworkCore;
using Vehistra.Application.Abstractions;
using Vehistra.Application.Services;
using Vehistra.Domain.Enums;
using Vehistra.Infrastructure.Persistence.Seeding;

namespace Vehistra.IntegrationTests;

/// <summary>Anmeldung, Sperre nach Fehlversuchen und Passwortregeln.</summary>
public class AuthenticationTests
{
    private const string UserName = "admin";
    private const string Password = "Sicher-Passwort-1";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<TestDatabase> WithAdministratorAsync()
    {
        var database = await TestDatabase.CreateAsync();

        await database.Service<DatabaseSeeder>()
            .CreateAdministratorAsync(UserName, Password, "Max", "Muster", null, Token);

        return database;
    }

    [Fact]
    public async Task Die_Anmeldung_mit_richtigem_Passwort_gelingt()
    {
        await using var database = await WithAdministratorAsync();

        var result = await database.Service<IAuthenticationService>().LoginAsync(UserName, Password, Token);

        result.IsSuccessful.ShouldBeTrue();
        result.User.ShouldNotBeNull();
        result.User!.UserName.ShouldBe(UserName);
        result.User.Permissions.Count.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Die_Anmeldung_mit_falschem_Passwort_scheitert()
    {
        await using var database = await WithAdministratorAsync();

        var result = await database.Service<IAuthenticationService>()
            .LoginAsync(UserName, "falsch", Token);

        result.IsSuccessful.ShouldBeFalse();
        result.User.ShouldBeNull();
        result.ErrorMessage.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Die_Fehlermeldung_verraet_nicht_ob_der_Benutzer_existiert()
    {
        await using var database = await WithAdministratorAsync();
        var authentication = database.Service<IAuthenticationService>();

        var wrongPassword = await authentication.LoginAsync(UserName, "falsch", Token);
        var unknownUser = await authentication.LoginAsync("gibtesnicht", "falsch", Token);

        unknownUser.ErrorMessage.ShouldBe(wrongPassword.ErrorMessage);
    }

    [Fact]
    public async Task Nach_zu_vielen_Fehlversuchen_wird_das_Konto_gesperrt()
    {
        await using var database = await WithAdministratorAsync();
        var authentication = database.Service<IAuthenticationService>();

        var maxAttempts = await database.Service<ISettingsService>()
            .GetIntAsync(SettingsKeys.LoginMaxFailedAttempts, 5, Token);

        LoginResultKind last = default;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var result = await authentication.LoginAsync(UserName, "falsch", Token);
            last = new LoginResultKind(result.IsLockedOut, result.RemainingAttempts);
        }

        last.IsLockedOut.ShouldBeTrue();
    }

    [Fact]
    public async Task Ein_gesperrtes_Konto_weist_auch_das_richtige_Passwort_ab()
    {
        await using var database = await WithAdministratorAsync();
        var authentication = database.Service<IAuthenticationService>();

        var maxAttempts = await database.Service<ISettingsService>()
            .GetIntAsync(SettingsKeys.LoginMaxFailedAttempts, 5, Token);

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            await authentication.LoginAsync(UserName, "falsch", Token);
        }

        var result = await authentication.LoginAsync(UserName, Password, Token);

        result.IsSuccessful.ShouldBeFalse();
        result.IsLockedOut.ShouldBeTrue();
    }

    [Fact]
    public async Task Nach_Ablauf_der_Sperre_gelingt_die_Anmeldung_wieder()
    {
        await using var database = await WithAdministratorAsync();
        var authentication = database.Service<IAuthenticationService>();
        var settings = database.Service<ISettingsService>();

        var maxAttempts = await settings.GetIntAsync(SettingsKeys.LoginMaxFailedAttempts, 5, Token);
        var lockoutMinutes = await settings.GetIntAsync(SettingsKeys.LoginLockoutMinutes, 15, Token);

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            await authentication.LoginAsync(UserName, "falsch", Token);
        }

        database.Clock.Advance(TimeSpan.FromMinutes(lockoutMinutes + 1));

        (await authentication.LoginAsync(UserName, Password, Token)).IsSuccessful.ShouldBeTrue();
    }

    [Fact]
    public async Task Eine_erfolgreiche_Anmeldung_setzt_den_Fehlversuchszaehler_zurueck()
    {
        await using var database = await WithAdministratorAsync();
        var authentication = database.Service<IAuthenticationService>();

        await authentication.LoginAsync(UserName, "falsch", Token);
        await authentication.LoginAsync(UserName, Password, Token);

        var user = await database.Db.Users.SingleAsync(Token);

        user.FailedLoginAttempts.ShouldBe(0);
        user.LockedUntil.ShouldBeNull();
    }

    [Fact]
    public async Task Die_Anmeldung_wird_im_Pruefprotokoll_festgehalten()
    {
        await using var database = await WithAdministratorAsync();

        await database.Service<IAuthenticationService>().LoginAsync(UserName, Password, Token);

        var entries = await database.Db.AuditLogs
            .Where(a => a.Action == AuditAction.Login)
            .ToListAsync(Token);

        entries.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Auch_der_Fehlversuch_wird_festgehalten()
    {
        await using var database = await WithAdministratorAsync();

        await database.Service<IAuthenticationService>().LoginAsync(UserName, "falsch", Token);

        var entries = await database.Db.AuditLogs
            .Where(a => a.Action == AuditAction.LoginFailed)
            .ToListAsync(Token);

        entries.ShouldNotBeEmpty();
    }

    [Theory]
    [InlineData("kurz")]
    [InlineData("alleskleingeschrieben")]
    [InlineData("OHNEZIFFERNUNDKLEIN")]
    [InlineData("")]
    public async Task Zu_schwache_Passwoerter_werden_abgelehnt(string password)
    {
        await using var database = await TestDatabase.CreateAsync();

        var errors = await database.Service<IAuthenticationService>()
            .ValidatePasswordAsync(password, Token);

        errors.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Ein_sicheres_Passwort_wird_angenommen()
    {
        await using var database = await TestDatabase.CreateAsync();

        var errors = await database.Service<IAuthenticationService>()
            .ValidatePasswordAsync("Sicher-Passwort-1", Token);

        errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task Das_Zuruecksetzen_erzeugt_ein_Einmalpasswort()
    {
        await using var database = await WithAdministratorAsync();
        database.SignInAsAdministrator();

        var user = await database.Db.Users.SingleAsync(Token);
        var temporary = await database.Service<IAuthenticationService>()
            .ResetPasswordAsync(user.Id, cancellationToken: Token);

        temporary.ShouldNotBeNullOrWhiteSpace();

        var result = await database.Service<IAuthenticationService>()
            .LoginAsync(UserName, temporary, Token);

        result.IsSuccessful.ShouldBeTrue();
        result.MustChangePassword.ShouldBeTrue();
    }

    private readonly record struct LoginResultKind(bool IsLockedOut, int? RemainingAttempts);
}
