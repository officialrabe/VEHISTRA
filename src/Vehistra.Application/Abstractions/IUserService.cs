using Vehistra.Domain.Entities;

namespace Vehistra.Application.Abstractions;

/// <summary>Verwaltung von Benutzern, Rollen und Berechtigungen.</summary>
public interface IUserService
{
    Task<IReadOnlyList<User>> GetUsersAsync(bool includeInactive = true, CancellationToken cancellationToken = default);

    Task<User?> GetUserAsync(int id, CancellationToken cancellationToken = default);

    Task<int> CreateUserAsync(User user, string password, IEnumerable<int> roleIds, CancellationToken cancellationToken = default);

    Task UpdateUserAsync(User user, IEnumerable<int> roleIds, CancellationToken cancellationToken = default);

    Task SetUserActiveAsync(int userId, bool isActive, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Role>> GetRolesAsync(CancellationToken cancellationToken = default);

    Task<Role?> GetRoleAsync(int id, CancellationToken cancellationToken = default);

    Task<int> CreateRoleAsync(Role role, IEnumerable<string> permissionNames, CancellationToken cancellationToken = default);

    Task UpdateRoleAsync(Role role, IEnumerable<string> permissionNames, CancellationToken cancellationToken = default);

    Task DeleteRoleAsync(int roleId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Permission>> GetPermissionsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetRolePermissionNamesAsync(int roleId, CancellationToken cancellationToken = default);
}
