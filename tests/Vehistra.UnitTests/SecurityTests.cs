using Vehistra.Application.Abstractions;
using Vehistra.Domain.Security;
using Vehistra.Infrastructure.Security;

namespace Vehistra.UnitTests;

/// <summary>Tests des Passwort-Hashings. Passwoerter duerfen niemals im Klartext liegen.</summary>
public class PasswordHasherTests
{
    private readonly IPasswordHasher _hasher = new Pbkdf2PasswordHasher();

    [Fact]
    public void Hash_enthaelt_niemals_das_Passwort_im_Klartext()
    {
        const string password = "MeinGeheimesPasswort123";

        var hash = _hasher.Hash(password);

        hash.ShouldNotContain(password);
        hash.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Hash_nennt_Verfahren_und_Durchlaeufe_im_Format()
    {
        var hash = _hasher.Hash("Testpasswort1");
        var parts = hash.Split('$');

        parts[0].ShouldBe("PBKDF2");
        parts[1].ShouldBe("SHA256");
        int.Parse(parts[2]).ShouldBeGreaterThanOrEqualTo(210_000);
        parts.Length.ShouldBe(5);
    }

    [Fact]
    public void Zwei_Hashes_desselben_Passworts_unterscheiden_sich_durch_das_Salz()
    {
        const string password = "GleichesPasswort1";

        _hasher.Hash(password).ShouldNotBe(_hasher.Hash(password));
    }

    [Fact]
    public void Verify_erkennt_das_richtige_Passwort()
    {
        const string password = "Richtig-Passwort-1";

        _hasher.Verify(password, _hasher.Hash(password)).ShouldBeTrue();
    }

    [Theory]
    [InlineData("falsch")]
    [InlineData("Richtig-Passwort-2")]
    [InlineData("richtig-passwort-1")]
    [InlineData("")]
    public void Verify_weist_falsche_Passwoerter_ab(string attempt) =>
        _hasher.Verify(attempt, _hasher.Hash("Richtig-Passwort-1")).ShouldBeFalse();

    [Theory]
    [InlineData("")]
    [InlineData("kein-gueltiges-format")]
    [InlineData("PBKDF2$SHA256$1000")]
    [InlineData("PBKDF2$SHA512$210000$abc$def")]
    public void Verify_stuerzt_bei_kaputten_Hashwerten_nicht_ab(string hash) =>
        _hasher.Verify("beliebig", hash).ShouldBeFalse();

    [Fact]
    public void NeedsRehash_verneint_bei_aktuellen_Parametern() =>
        _hasher.NeedsRehash(_hasher.Hash("Aktuell1")).ShouldBeFalse();

    [Fact]
    public void NeedsRehash_bejaht_bei_zu_wenigen_Durchlaeufen() =>
        _hasher.NeedsRehash("PBKDF2$SHA256$1000$c2FsemVk$aGFzaHdlcnQ=").ShouldBeTrue();

    [Fact]
    public void NeedsRehash_bejaht_bei_unbekanntem_Format() =>
        _hasher.NeedsRehash("MD5$irgendwas").ShouldBeTrue();
}

/// <summary>Tests des Rechte- und Rollenmodells.</summary>
public class PermissionModelTests
{
    [Fact]
    public void Jede_Berechtigung_besitzt_einen_eindeutigen_Schluessel()
    {
        var keys = Permissions.All.Select(p => p.Name).ToList();

        keys.Distinct(StringComparer.Ordinal).Count().ShouldBe(keys.Count);
    }

    [Fact]
    public void Jede_Berechtigung_ist_beschrieben()
    {
        foreach (var permission in Permissions.All)
        {
            permission.Name.ShouldNotBeNullOrWhiteSpace();
            permission.DisplayName.ShouldNotBeNullOrWhiteSpace();
            permission.Group.ShouldNotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void Alle_Rollennamen_sind_eindeutig()
    {
        var names = RoleNames.Defaults.Select(r => r.Name).ToList();

        names.Distinct(StringComparer.Ordinal).Count().ShouldBe(names.Count);
    }

    [Fact]
    public void Die_Administratorrolle_besitzt_alle_Berechtigungen()
    {
        var administrator = RoleNames.Defaults.Single(r => r.Name == RoleNames.Administrator);

        foreach (var permission in Permissions.All)
        {
            administrator.Permissions.ShouldContain(permission.Name);
        }
    }

    [Fact]
    public void Die_Mitarbeiterrolle_darf_keine_Benutzer_verwalten()
    {
        var employee = RoleNames.Defaults.Single(r => r.Name == RoleNames.Mitarbeiter);

        employee.Permissions.ShouldNotContain(Permissions.UsersManage);
    }

    [Fact]
    public void Keine_Rolle_verweist_auf_eine_unbekannte_Berechtigung()
    {
        var known = Permissions.All.Select(p => p.Name).ToHashSet(StringComparer.Ordinal);

        foreach (var role in RoleNames.Defaults)
        {
            foreach (var permission in role.Permissions)
            {
                known.ShouldContain(permission, $"Die Rolle {role.Name} verweist auf {permission}.");
            }
        }
    }

    [Fact]
    public void Das_Pruefprotokoll_ist_nur_fuer_die_Verwaltung_einsehbar()
    {
        var employee = RoleNames.Defaults.Single(r => r.Name == RoleNames.Mitarbeiter);

        employee.Permissions.ShouldNotContain(Permissions.AuditView);
    }
}
