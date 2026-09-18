namespace Vehistra.Application.Common;

/// <summary>Seitenweise Ergebnisliste fuer grosse Datenmengen.</summary>
public sealed class PagedResult<T>
{
    public PagedResult(IReadOnlyList<T> items, int totalCount, int pageNumber, int pageSize)
    {
        Items = items;
        TotalCount = totalCount;
        PageNumber = pageNumber;
        PageSize = pageSize;
    }

    public IReadOnlyList<T> Items { get; }

    public int TotalCount { get; }

    public int PageNumber { get; }

    public int PageSize { get; }

    public int PageCount => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasPreviousPage => PageNumber > 1;

    public bool HasNextPage => PageNumber < PageCount;

    public static PagedResult<T> Empty(int pageSize = 50) => new([], 0, 1, pageSize);
}
