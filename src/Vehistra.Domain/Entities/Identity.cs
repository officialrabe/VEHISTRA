using Vehistra.Domain.Common;

namespace Vehistra.Domain.Entities;

/// <summary>Benutzerkonto der Anwendung (internes Konto, unabhaengig vom Windows-Konto).</summary>
public class User : AuditableEntity, ISoftDeletable
{
    public string UserName { get; set; } = string.Empty;

    /// <summary>PBKDF2-Hash. Niemals das Klartextpasswort speichern.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? PersonnelNumber { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Erzwingt eine Passwortaenderung beim naechsten Login (z. B. nach Zuruecksetzen).</summary>
    public bool MustChangePassword { get; set; }

    public DateTime? LastLoginAt { get; set; }

    public string? LastLoginComputer { get; set; }

    public int FailedLoginAttempts { get; set; }

    /// <summary>Sperrzeitpunkt nach zu vielen Fehlversuchen.</summary>
    public DateTime? LockedUntil { get; set; }

    public DateTime? PasswordChangedAt { get; set; }

    public string? Comment { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public int? DeletedByUserId { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();

    public string FullName => $"{FirstName} {LastName}".Trim();

    public string DisplayName => string.IsNullOrWhiteSpace(FullName) ? UserName : $"{FullName} ({UserName})";
}

/// <summary>Rolle als Buendel von Berechtigungen.</summary>
public class Role : AuditableEntity
{
    public string Name { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Systemrollen koennen nicht geloescht werden.</summary>
    public bool IsSystemRole { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}

/// <summary>Einzelne Berechtigung.</summary>
public class Permission : EntityBase
{
    public string Name { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Group { get; set; } = string.Empty;

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}

/// <summary>Zuordnung Benutzer zu Rolle.</summary>
public class UserRole : EntityBase
{
    public int UserId { get; set; }

    public User? User { get; set; }

    public int RoleId { get; set; }

    public Role? Role { get; set; }

    public DateTime AssignedAt { get; set; }

    public int? AssignedByUserId { get; set; }
}

/// <summary>Zuordnung Rolle zu Berechtigung.</summary>
public class RolePermission : EntityBase
{
    public int RoleId { get; set; }

    public Role? Role { get; set; }

    public int PermissionId { get; set; }

    public Permission? Permission { get; set; }
}
