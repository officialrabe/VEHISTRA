using Fuhrpark.Domain.Enums;

namespace Fuhrpark.Application.Abstractions;

/// <summary>Schreibt Audit-Eintraege ausserhalb des automatischen SaveChanges-Interceptors.</summary>
public interface IAuditWriter
{
    Task WriteAsync(
        AuditAction action,
        string entityName,
        string? entityId,
        string? entityDisplay,
        string? additionalInfo = null,
        CancellationToken cancellationToken = default);
}
