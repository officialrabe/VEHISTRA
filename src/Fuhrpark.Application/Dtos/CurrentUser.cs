namespace Fuhrpark.Application.Dtos;

/// <summary>Angemeldeter Benutzer inklusive aufgeloester Berechtigungen.</summary>
public sealed class CurrentUser
{
    public CurrentUser(
        int id,
        string userName,
        string firstName,
        string lastName,
        IReadOnlyCollection<string> roles,
        IReadOnlyCollection<string> permissions,
        bool mustChangePassword)
    {
        Id = id;
        UserName = userName;
        FirstName = firstName;
        LastName = lastName;
        Roles = roles;
        Permissions = new HashSet<string>(permissions, StringComparer.OrdinalIgnoreCase);
        MustChangePassword = mustChangePassword;
        LoginAt = DateTime.Now;
    }

    public int Id { get; }

    public string UserName { get; }

    public string FirstName { get; }

    public string LastName { get; }

    public IReadOnlyCollection<string> Roles { get; }

    public HashSet<string> Permissions { get; }

    public bool MustChangePassword { get; internal set; }

    public DateTime LoginAt { get; }

    public string FullName => $"{FirstName} {LastName}".Trim();

    public string DisplayName => string.IsNullOrWhiteSpace(FullName) ? UserName : FullName;

    public bool IsAdministrator => Roles.Contains(Domain.Security.RoleNames.Administrator, StringComparer.OrdinalIgnoreCase);

    public bool Has(string permission) => Permissions.Contains(permission);
}
