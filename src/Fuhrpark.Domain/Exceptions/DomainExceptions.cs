namespace Fuhrpark.Domain.Exceptions;

/// <summary>Basisklasse fuer fachliche Fehler, die dem Benutzer direkt angezeigt werden duerfen.</summary>
public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message)
    {
    }

    protected DomainException(string message, Exception inner) : base(message, inner)
    {
    }
}

/// <summary>Eine fachliche Regel wurde verletzt.</summary>
public sealed class BusinessRuleException : DomainException
{
    public BusinessRuleException(string message) : base(message)
    {
    }
}

/// <summary>Ein angefragter Datensatz existiert nicht (mehr).</summary>
public sealed class EntityNotFoundException : DomainException
{
    public EntityNotFoundException(string entityName, object id)
        : base($"Der Datensatz '{entityName}' mit der Kennung '{id}' wurde nicht gefunden.")
    {
        EntityName = entityName;
        Id = id;
    }

    public string EntityName { get; }

    public object Id { get; }
}

/// <summary>Dem angemeldeten Benutzer fehlt eine Berechtigung.</summary>
public sealed class PermissionDeniedException : DomainException
{
    public PermissionDeniedException(string permission)
        : base($"Fuer diese Aktion fehlt die Berechtigung '{permission}'.")
    {
        Permission = permission;
    }

    public string Permission { get; }
}

/// <summary>Ein Datensatz wurde zwischenzeitlich von einem anderen Benutzer geaendert.</summary>
public sealed class ConcurrencyConflictException : DomainException
{
    public ConcurrencyConflictException(string entityName, object? id, Exception? inner = null)
        : base("Dieser Datensatz wurde zwischenzeitlich von einem anderen Benutzer geaendert.", inner ?? new Exception())
    {
        EntityName = entityName;
        Id = id;
    }

    public string EntityName { get; }

    public object? Id { get; }
}
