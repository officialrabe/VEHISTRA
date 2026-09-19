using Vehistra.Application.Abstractions;
using Vehistra.Application.Dtos;
using Vehistra.Domain.Entities;
using Vehistra.Domain.Enums;
using Vehistra.Domain.Exceptions;
using Vehistra.Domain.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Vehistra.Application.Services;

/// <inheritdoc />
public sealed class AuthenticationService : IAuthenticationService
{
    private const int DefaultMaxFailedAttempts = 5;
    private const int DefaultLockoutMinutes = 15;
    private const int DefaultMinimumPasswordLength = 8;

    private readonly IVehistraDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ICurrentUserService _currentUser;
    private readonly ISettingsService _settings;
    private readonly IClock _clock;
    private readonly IAuditWriter _audit;
    private readonly ILogger<AuthenticationService> _logger;

    public AuthenticationService(
        IVehistraDbContext db,
        IPasswordHasher passwordHasher,
        ICurrentUserService currentUser,
        ISettingsService settings,
        IClock clock,
        IAuditWriter audit,
        ILogger<AuthenticationService> logger)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _currentUser = currentUser;
        _settings = settings;
        _clock = clock;
        _audit = audit;
        _logger = logger;
    }

    public async Task<LoginResult> LoginAsync(string userName, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
        {
            return new LoginResult(false, null, "Bitte Benutzername und Passwort eingeben.", false, false, null);
        }

        var maxAttempts = await _settings
            .GetIntAsync(SettingsKeys.LoginMaxFailedAttempts, DefaultMaxFailedAttempts, cancellationToken)
            .ConfigureAwait(false);
        var lockoutMinutes = await _settings
            .GetIntAsync(SettingsKeys.LoginLockoutMinutes, DefaultLockoutMinutes, cancellationToken)
            .ConfigureAwait(false);

        var normalized = userName.Trim();

        var user = await _db.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r!.RolePermissions)
                        .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(u => u.UserName == normalized && !u.IsDeleted, cancellationToken)
            .ConfigureAwait(false);

        if (user is null)
        {
            // Aus Sicherheitsgruenden keine Unterscheidung zwischen unbekanntem Benutzer und falschem Passwort.
            _logger.LogWarning("Anmeldung fehlgeschlagen: unbekannter Benutzer {UserName}", normalized);
            await WriteLoginAuditAsync(null, normalized, false, "Unbekannter Benutzer", cancellationToken).ConfigureAwait(false);
            return new LoginResult(false, null, "Benutzername oder Passwort ist falsch.", false, false, null);
        }

        if (user.LockedUntil is { } lockedUntil && lockedUntil > _clock.Now)
        {
            var remaining = (int)Math.Ceiling((lockedUntil - _clock.Now).TotalMinutes);
            return new LoginResult(
                false, null,
                $"Das Konto ist wegen zu vieler Fehlversuche noch {remaining} Minute(n) gesperrt.",
                true, false, 0);
        }

        if (!user.IsActive)
        {
            await WriteLoginAuditAsync(user.Id, user.UserName, false, "Konto deaktiviert", cancellationToken).ConfigureAwait(false);
            return new LoginResult(false, null, "Dieses Benutzerkonto ist deaktiviert.", false, false, null);
        }

        if (!_passwordHasher.Verify(password, user.PasswordHash))
        {
            user.FailedLoginAttempts++;

            string message;
            var lockedOut = false;

            if (user.FailedLoginAttempts >= maxAttempts)
            {
                user.LockedUntil = _clock.Now.AddMinutes(lockoutMinutes);
                user.FailedLoginAttempts = 0;
                lockedOut = true;
                message = $"Zu viele Fehlversuche. Das Konto ist fuer {lockoutMinutes} Minuten gesperrt.";
            }
            else
            {
                message = "Benutzername oder Passwort ist falsch.";
            }

            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await WriteLoginAuditAsync(user.Id, user.UserName, false, "Falsches Passwort", cancellationToken).ConfigureAwait(false);

            return new LoginResult(false, null, message, lockedOut, false,
                lockedOut ? 0 : Math.Max(0, maxAttempts - user.FailedLoginAttempts));
        }

        // Erfolgreiche Anmeldung
        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;
        user.LastLoginAt = _clock.Now;
        user.LastLoginComputer = _currentUser.ComputerName;

        if (_passwordHasher.NeedsRehash(user.PasswordHash))
        {
            user.PasswordHash = _passwordHasher.Hash(password);
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var roles = user.UserRoles
            .Where(ur => ur.Role is not null)
            .Select(ur => ur.Role!.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var permissions = user.UserRoles
            .Where(ur => ur.Role is not null)
            .SelectMany(ur => ur.Role!.RolePermissions)
            .Where(rp => rp.Permission is not null)
            .Select(rp => rp.Permission!.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var currentUser = new CurrentUser(
            user.Id, user.UserName, user.FirstName, user.LastName,
            roles, permissions, user.MustChangePassword);

        _currentUser.SetUser(currentUser);

        await WriteLoginAuditAsync(user.Id, user.UserName, true, null, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Benutzer {UserName} hat sich angemeldet.", user.UserName);

        return new LoginResult(true, currentUser, null, false, user.MustChangePassword, null);
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        var user = _currentUser.User;
        if (user is not null)
        {
            await _audit.WriteAsync(AuditAction.Logout, nameof(User), user.Id.ToString(), user.UserName,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Benutzer {UserName} hat sich abgemeldet.", user.UserName);
        }

        _currentUser.SetUser(null);
    }

    public async Task<bool> ChangePasswordAsync(
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        var current = _currentUser.User
            ?? throw new BusinessRuleException("Es ist kein Benutzer angemeldet.");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == current.Id, cancellationToken).ConfigureAwait(false)
            ?? throw new EntityNotFoundException(nameof(User), current.Id);

        if (!_passwordHasher.Verify(currentPassword, user.PasswordHash))
        {
            throw new BusinessRuleException("Das aktuelle Passwort ist falsch.");
        }

        var errors = await ValidatePasswordAsync(newPassword, cancellationToken).ConfigureAwait(false);
        if (errors.Count > 0)
        {
            throw new BusinessRuleException(string.Join(Environment.NewLine, errors));
        }

        user.PasswordHash = _passwordHasher.Hash(newPassword);
        user.PasswordChangedAt = _clock.Now;
        user.MustChangePassword = false;

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await _audit.WriteAsync(AuditAction.PasswordChanged, nameof(User), user.Id.ToString(), user.UserName,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        current.MustChangePassword = false;
        return true;
    }

    public async Task<string> ResetPasswordAsync(
        int userId,
        string? newPassword = null,
        CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.UsersManage);

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken).ConfigureAwait(false)
            ?? throw new EntityNotFoundException(nameof(User), userId);

        var password = newPassword;
        if (string.IsNullOrWhiteSpace(password))
        {
            password = GenerateTemporaryPassword();
        }
        else
        {
            var errors = await ValidatePasswordAsync(password, cancellationToken).ConfigureAwait(false);
            if (errors.Count > 0)
            {
                throw new BusinessRuleException(string.Join(Environment.NewLine, errors));
            }
        }

        user.PasswordHash = _passwordHasher.Hash(password);
        user.PasswordChangedAt = _clock.Now;
        user.MustChangePassword = true;
        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await _audit.WriteAsync(AuditAction.PasswordReset, nameof(User), user.Id.ToString(), user.UserName,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return password;
    }

    public Task<bool> HasAnyActiveAdministratorAsync(CancellationToken cancellationToken = default) =>
        _db.Users.AnyAsync(
            u => u.IsActive && !u.IsDeleted &&
                 u.UserRoles.Any(ur => ur.Role != null && ur.Role.Name == RoleNames.Administrator),
            cancellationToken);

    public async Task<IReadOnlyList<string>> ValidatePasswordAsync(
        string password,
        CancellationToken cancellationToken = default)
    {
        var minimumLength = await _settings
            .GetIntAsync(SettingsKeys.PasswordMinimumLength, DefaultMinimumPasswordLength, cancellationToken)
            .ConfigureAwait(false);

        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(password))
        {
            errors.Add("Das Passwort darf nicht leer sein.");
            return errors;
        }

        if (password.Length < minimumLength)
        {
            errors.Add($"Das Passwort muss mindestens {minimumLength} Zeichen lang sein.");
        }

        if (!password.Any(char.IsLetter))
        {
            errors.Add("Das Passwort muss mindestens einen Buchstaben enthalten.");
        }

        if (!password.Any(char.IsDigit))
        {
            errors.Add("Das Passwort muss mindestens eine Ziffer enthalten.");
        }

        return errors;
    }

    private async Task WriteLoginAuditAsync(
        int? userId,
        string userName,
        bool successful,
        string? reason,
        CancellationToken cancellationToken)
    {
        var entry = new AuditLog
        {
            Timestamp = _clock.Now,
            UserId = userId,
            UserName = userName,
            ComputerName = _currentUser.ComputerName,
            Action = successful ? AuditAction.Login : AuditAction.LoginFailed,
            EntityName = nameof(User),
            EntityId = userId?.ToString(),
            EntityDisplay = userName,
            AdditionalInfo = reason
        };

        _db.AuditLogs.Add(entry);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string GenerateTemporaryPassword()
    {
        // Ohne leicht verwechselbare Zeichen (0/O, 1/l/I).
        const string letters = "abcdefghijkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ";
        const string digits = "23456789";
        const string special = "!$%&*+-";

        var random = System.Security.Cryptography.RandomNumberGenerator.Create();
        var buffer = new byte[12];
        random.GetBytes(buffer);

        var chars = new char[12];
        for (var i = 0; i < 12; i++)
        {
            var pool = i switch
            {
                < 8 => letters,
                < 11 => digits,
                _ => special
            };
            chars[i] = pool[buffer[i] % pool.Length];
        }

        return new string(chars);
    }
}
