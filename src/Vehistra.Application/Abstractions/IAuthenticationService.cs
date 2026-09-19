using Vehistra.Application.Dtos;

namespace Vehistra.Application.Abstractions;

/// <summary>Anmeldung, Abmeldung und Passwortverwaltung.</summary>
public interface IAuthenticationService
{
    Task<LoginResult> LoginAsync(string userName, string password, CancellationToken cancellationToken = default);

    Task LogoutAsync(CancellationToken cancellationToken = default);

    Task<bool> ChangePasswordAsync(
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default);

    /// <summary>Setzt das Passwort eines Benutzers zurueck (Administratorfunktion).</summary>
    Task<string> ResetPasswordAsync(int userId, string? newPassword = null, CancellationToken cancellationToken = default);

    /// <summary>Prueft, ob ueberhaupt ein aktives Administratorkonto existiert.</summary>
    Task<bool> HasAnyActiveAdministratorAsync(CancellationToken cancellationToken = default);

    /// <summary>Validiert ein Passwort gegen die konfigurierten Mindestanforderungen.</summary>
    Task<IReadOnlyList<string>> ValidatePasswordAsync(string password, CancellationToken cancellationToken = default);
}
