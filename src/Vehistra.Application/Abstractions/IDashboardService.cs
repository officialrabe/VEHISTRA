using Vehistra.Application.Dtos;

namespace Vehistra.Application.Abstractions;

/// <summary>Kennzahlen fuer das Dashboard.</summary>
public interface IDashboardService
{
    Task<DashboardData> GetAsync(CancellationToken cancellationToken = default);
}

/// <summary>Globale Suche ueber alle wichtigen Datenbereiche.</summary>
public interface ISearchService
{
    Task<IReadOnlyList<SearchResultItem>> SearchAsync(
        string searchText,
        int maxResultsPerArea = 10,
        CancellationToken cancellationToken = default);
}

/// <summary>Lesender Zugriff auf das Audit-Log.</summary>
public interface IAuditService
{
    Task<Common.PagedResult<AuditLogListItem>> GetAsync(AuditFilter filter, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetEntityNamesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetUserNamesAsync(CancellationToken cancellationToken = default);
}
