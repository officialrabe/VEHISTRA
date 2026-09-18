using Fuhrpark.Application.Abstractions;
using Fuhrpark.Application.Common;
using Fuhrpark.Domain.Entities;
using Fuhrpark.Domain.Exceptions;
using Fuhrpark.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace Fuhrpark.Application.Services;

/// <inheritdoc />
public sealed class UserService : IUserService
{
    private readonly IFuhrparkDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAuthenticationService _authentication;
    private readonly IClock _clock;

    public UserService(
        IFuhrparkDbContext db,
        ICurrentUserService currentUser,
        IPasswordHasher passwordHasher,
        IAuthenticationService authentication,
        IClock clock)
    {
        _db = db;
        _currentUser = currentUser;
        _passwordHasher = passwordHasher;
        _authentication = authentication;
        _clock = clock;
    }

    public async Task<IReadOnlyList<User>> GetUsersAsync(
        bool includeInactive = true,
        CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.UsersManage);

        var query = _db.Users
            .AsNoTracking()
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .Where(u => !u.IsDeleted);

        if (!includeInactive)
        {
            query = query.Where(u => u.IsActive);
        }

        return await query
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.FirstName)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<User?> GetUserAsync(int id, CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.UsersManage);

        return await _db.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> CreateUserAsync(
        User user,
        string password,
        IEnumerable<int> roleIds,
        CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.UsersManage);

        user.UserName = Guard.NotEmpty(user.UserName, "Benutzername");
        user.LastName = Guard.NotEmpty(user.LastName, "Nachname");

        var exists = await _db.Users
            .AnyAsync(u => u.UserName == user.UserName && !u.IsDeleted, cancellationToken)
            .ConfigureAwait(false);
        Guard.That(!exists, $"Der Benutzername '{user.UserName}' ist bereits vergeben.");

        var errors = await _authentication.ValidatePasswordAsync(password, cancellationToken).ConfigureAwait(false);
        if (errors.Count > 0)
        {
            throw new BusinessRuleException(string.Join(Environment.NewLine, errors));
        }

        user.PasswordHash = _passwordHasher.Hash(password);
        user.PasswordChangedAt = _clock.Now;

        _db.Users.Add(user);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await ReplaceRolesAsync(user.Id, roleIds, cancellationToken).ConfigureAwait(false);
        return user.Id;
    }

    public async Task UpdateUserAsync(User user, IEnumerable<int> roleIds, CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.UsersManage);

        var existing = await _db.Users
            .Include(u => u.UserRoles)
            .FirstOrDefaultAsync(u => u.Id == user.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EntityNotFoundException(nameof(User), user.Id);

        var duplicate = await _db.Users
            .AnyAsync(u => u.UserName == user.UserName && u.Id != user.Id && !u.IsDeleted, cancellationToken)
            .ConfigureAwait(false);
        Guard.That(!duplicate, $"Der Benutzername '{user.UserName}' ist bereits vergeben.");

        existing.UserName = Guard.NotEmpty(user.UserName, "Benutzername");
        existing.FirstName = user.FirstName;
        existing.LastName = Guard.NotEmpty(user.LastName, "Nachname");
        existing.Email = user.Email;
        existing.PersonnelNumber = user.PersonnelNumber;
        existing.Comment = user.Comment;
        existing.IsActive = user.IsActive;
        existing.MustChangePassword = user.MustChangePassword;
        existing.RowVersion = user.RowVersion;

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await ReplaceRolesAsync(existing.Id, roleIds, cancellationToken).ConfigureAwait(false);
    }

    public async Task SetUserActiveAsync(int userId, bool isActive, CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.UsersManage);

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken).ConfigureAwait(false)
            ?? throw new EntityNotFoundException(nameof(User), userId);

        if (!isActive)
        {
            await EnsureNotLastAdministratorAsync(userId, cancellationToken).ConfigureAwait(false);
        }

        user.IsActive = isActive;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Role>> GetRolesAsync(CancellationToken cancellationToken = default)
    {
        if (!_currentUser.HasPermission(Permissions.RolesManage))
        {
            _currentUser.DemandPermission(Permissions.UsersManage);
        }

        return await _db.Roles
            .AsNoTracking()
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
            .OrderBy(r => r.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Role?> GetRoleAsync(int id, CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.RolesManage);

        return await _db.Roles
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> CreateRoleAsync(
        Role role,
        IEnumerable<string> permissionNames,
        CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.RolesManage);

        role.Name = Guard.NotEmpty(role.Name, "Rollenname").ToUpperInvariant();
        role.DisplayName = string.IsNullOrWhiteSpace(role.DisplayName) ? role.Name : role.DisplayName;

        var exists = await _db.Roles.AnyAsync(r => r.Name == role.Name, cancellationToken).ConfigureAwait(false);
        Guard.That(!exists, $"Eine Rolle mit dem Namen '{role.Name}' existiert bereits.");

        _db.Roles.Add(role);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await ReplacePermissionsAsync(role.Id, permissionNames, cancellationToken).ConfigureAwait(false);
        return role.Id;
    }

    public async Task UpdateRoleAsync(
        Role role,
        IEnumerable<string> permissionNames,
        CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.RolesManage);

        var existing = await _db.Roles.FirstOrDefaultAsync(r => r.Id == role.Id, cancellationToken).ConfigureAwait(false)
            ?? throw new EntityNotFoundException(nameof(Role), role.Id);

        if (existing.IsSystemRole && !string.Equals(existing.Name, role.Name, StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessRuleException("Der Name einer Systemrolle kann nicht geaendert werden.");
        }

        existing.DisplayName = role.DisplayName;
        existing.Description = role.Description;
        existing.RowVersion = role.RowVersion;

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (existing.Name == RoleNames.Administrator)
        {
            // Die Administratorrolle behaelt immer alle Berechtigungen.
            await ReplacePermissionsAsync(existing.Id, Permissions.All.Select(p => p.Name), cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        await ReplacePermissionsAsync(existing.Id, permissionNames, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteRoleAsync(int roleId, CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.RolesManage);

        var role = await _db.Roles
            .Include(r => r.UserRoles)
            .FirstOrDefaultAsync(r => r.Id == roleId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EntityNotFoundException(nameof(Role), roleId);

        Guard.That(!role.IsSystemRole, "Systemrollen koennen nicht geloescht werden.");
        Guard.That(role.UserRoles.Count == 0,
            "Diese Rolle ist noch Benutzern zugeordnet und kann daher nicht geloescht werden.");

        _db.Roles.Remove(role);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Permission>> GetPermissionsAsync(CancellationToken cancellationToken = default)
    {
        return await _db.Permissions
            .AsNoTracking()
            .OrderBy(p => p.Group)
            .ThenBy(p => p.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<string>> GetRolePermissionNamesAsync(
        int roleId,
        CancellationToken cancellationToken = default)
    {
        return await _db.RolePermissions
            .AsNoTracking()
            .Where(rp => rp.RoleId == roleId && rp.Permission != null)
            .Select(rp => rp.Permission!.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task ReplaceRolesAsync(int userId, IEnumerable<int> roleIds, CancellationToken cancellationToken)
    {
        var target = roleIds.Distinct().ToHashSet();

        var existing = await _db.UserRoles
            .Where(ur => ur.UserId == userId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var removed = existing.Where(ur => !target.Contains(ur.RoleId)).ToList();
        if (removed.Count > 0)
        {
            var adminRoleId = await _db.Roles
                .Where(r => r.Name == RoleNames.Administrator)
                .Select(r => r.Id)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            if (adminRoleId != 0 && removed.Any(r => r.RoleId == adminRoleId))
            {
                await EnsureNotLastAdministratorAsync(userId, cancellationToken).ConfigureAwait(false);
            }

            _db.UserRoles.RemoveRange(removed);
        }

        foreach (var roleId in target.Where(id => existing.All(ur => ur.RoleId != id)))
        {
            _db.UserRoles.Add(new UserRole
            {
                UserId = userId,
                RoleId = roleId,
                AssignedAt = _clock.Now,
                AssignedByUserId = _currentUser.User?.Id
            });
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task ReplacePermissionsAsync(
        int roleId,
        IEnumerable<string> permissionNames,
        CancellationToken cancellationToken)
    {
        var names = permissionNames.Distinct(StringComparer.OrdinalIgnoreCase).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var permissions = await _db.Permissions
            .Where(p => names.Contains(p.Name))
            .Select(p => p.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var existing = await _db.RolePermissions
            .Where(rp => rp.RoleId == roleId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        _db.RolePermissions.RemoveRange(existing.Where(rp => !permissions.Contains(rp.PermissionId)));

        foreach (var permissionId in permissions.Where(id => existing.All(rp => rp.PermissionId != id)))
        {
            _db.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionId = permissionId });
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Verhindert, dass das letzte aktive Administratorkonto seine Rechte verliert.</summary>
    private async Task EnsureNotLastAdministratorAsync(int userId, CancellationToken cancellationToken)
    {
        var otherAdmins = await _db.Users
            .CountAsync(
                u => u.Id != userId && u.IsActive && !u.IsDeleted &&
                     u.UserRoles.Any(ur => ur.Role != null && ur.Role.Name == RoleNames.Administrator),
                cancellationToken)
            .ConfigureAwait(false);

        Guard.That(otherAdmins > 0,
            "Das letzte aktive Administratorkonto kann nicht deaktiviert oder seiner Rolle beraubt werden.");
    }
}
