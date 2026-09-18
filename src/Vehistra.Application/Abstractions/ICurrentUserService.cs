using Vehistra.Application.Dtos;

namespace Vehistra.Application.Abstractions;

/// <summary>Liefert den aktuell angemeldeten Benutzer und prueft dessen Berechtigungen.</summary>
public interface ICurrentUserService
{
    /// <summary>Der angemeldete Benutzer oder <c>null</c>, solange niemand angemeldet ist.</summary>
    CurrentUser? User { get; }

    bool IsAuthenticated { get; }

    string ComputerName { get; }

    /// <summary>Prueft eine Berechtigung, ohne eine Ausnahme auszuloesen.</summary>
    bool HasPermission(string permission);

    /// <summary>
    /// Prueft eine Berechtigung und wirft eine <see cref="Domain.Exceptions.PermissionDeniedException"/>,
    /// falls sie fehlt. Wird von allen Anwendungsdiensten verwendet - nicht nur von der Oberflaeche.
    /// </summary>
    void DemandPermission(string permission);

    void SetUser(CurrentUser? user);

    event EventHandler? UserChanged;
}
