namespace Fuhrpark.Domain.Common;

/// <summary>
/// Basisklasse aller persistierten Entitaeten.
/// </summary>
public abstract class EntityBase
{
    public int Id { get; set; }
}

/// <summary>
/// Entitaet mit Erstell-/Aenderungsnachverfolgung und optimistischer Nebenlaeufigkeitspruefung.
/// Das <see cref="RowVersion"/>-Feld wird von SQL Server als <c>rowversion</c> gefuehrt und
/// erlaubt die Erkennung konkurrierender Aenderungen mehrerer Arbeitsplaetze.
/// </summary>
public abstract class AuditableEntity : EntityBase
{
    public DateTime CreatedAt { get; set; }

    public int? CreatedByUserId { get; set; }

    public string? CreatedByUserName { get; set; }

    public DateTime? ModifiedAt { get; set; }

    public int? ModifiedByUserId { get; set; }

    public string? ModifiedByUserName { get; set; }

    /// <summary>Optimistic-Concurrency-Token.</summary>
    public byte[]? RowVersion { get; set; }
}

/// <summary>
/// Kennzeichnet Entitaeten, die niemals physisch geloescht, sondern nur logisch entfernt werden.
/// </summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }

    DateTime? DeletedAt { get; set; }

    int? DeletedByUserId { get; set; }
}

/// <summary>
/// Kennzeichnet Entitaeten, die archiviert werden koennen (aus der taeglichen Ansicht entfernt,
/// aber vollstaendig erhalten).
/// </summary>
public interface IArchivable
{
    bool IsArchived { get; set; }

    DateTime? ArchivedAt { get; set; }

    int? ArchivedByUserId { get; set; }
}
