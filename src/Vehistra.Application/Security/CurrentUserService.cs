using Vehistra.Application.Abstractions;
using Vehistra.Application.Dtos;
using Vehistra.Domain.Exceptions;

namespace Vehistra.Application.Security;

/// <summary>
/// Haelt den angemeldeten Benutzer fuer die Dauer der Sitzung und prueft Berechtigungen.
/// Die Pruefung erfolgt in den Anwendungsdiensten - nicht nur durch Ausblenden von Schaltflaechen.
/// </summary>
public sealed class CurrentUserService : ICurrentUserService
{
    private readonly object _sync = new();
    private CurrentUser? _user;

    public CurrentUser? User
    {
        get
        {
            lock (_sync)
            {
                return _user;
            }
        }
    }

    public bool IsAuthenticated => User is not null;

    public string ComputerName { get; } = Environment.MachineName;

    public event EventHandler? UserChanged;

    public bool HasPermission(string permission)
    {
        var user = User;
        return user is not null && user.Has(permission);
    }

    public void DemandPermission(string permission)
    {
        var user = User
            ?? throw new PermissionDeniedException(permission);

        if (!user.Has(permission))
        {
            throw new PermissionDeniedException(permission);
        }
    }

    public void SetUser(CurrentUser? user)
    {
        lock (_sync)
        {
            _user = user;
        }

        UserChanged?.Invoke(this, EventArgs.Empty);
    }
}
