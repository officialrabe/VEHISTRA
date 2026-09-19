using Vehistra.Application.Abstractions;
using Vehistra.Application.Common;
using Vehistra.Application.Dtos;
using Vehistra.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace Vehistra.Application.Services;

/// <summary>
/// Lesender Zugriff auf das Audit-Log. Eintraege koennen ueber die Anwendung
/// weder geaendert noch geloescht werden.
/// </summary>
public sealed class AuditService : IAuditService
{
    private readonly IVehistraDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public AuditService(IVehistraDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<AuditLogListItem>> GetAsync(
        AuditFilter filter,
        CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.AuditView);

        var query = _db.AuditLogs.AsNoTracking().AsQueryable();

        if (filter.From is { } from)
        {
            query = query.Where(a => a.Timestamp >= from);
        }

        if (filter.To is { } to)
        {
            var end = to.Date.AddDays(1);
            query = query.Where(a => a.Timestamp < end);
        }

        if (!string.IsNullOrWhiteSpace(filter.UserName))
        {
            query = query.Where(a => a.UserName == filter.UserName);
        }

        if (!string.IsNullOrWhiteSpace(filter.EntityName))
        {
            query = query.Where(a => a.EntityName == filter.EntityName);
        }

        if (filter.Action is { } action)
        {
            query = query.Where(a => a.Action == action);
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchText))
        {
            var text = filter.SearchText.Trim();
            query = query.Where(a =>
                (a.EntityDisplay != null && a.EntityDisplay.Contains(text)) ||
                (a.EntityId != null && a.EntityId.Contains(text)) ||
                (a.AdditionalInfo != null && a.AdditionalInfo.Contains(text)) ||
                (a.OldValues != null && a.OldValues.Contains(text)) ||
                (a.NewValues != null && a.NewValues.Contains(text)));
        }

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        var pageSize = filter.PageSize <= 0 ? 100 : filter.PageSize;
        var pageNumber = filter.PageNumber <= 0 ? 1 : filter.PageNumber;

        var items = await query
            .OrderByDescending(a => a.Timestamp)
            .ThenByDescending(a => a.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AuditLogListItem(
                a.Id, a.Timestamp, a.UserName, a.ComputerName, a.Action,
                a.EntityName, a.EntityId, a.EntityDisplay, a.OldValues, a.NewValues, a.AdditionalInfo))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PagedResult<AuditLogListItem>(items, total, pageNumber, pageSize);
    }

    public async Task<IReadOnlyList<string>> GetEntityNamesAsync(CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.AuditView);

        return await _db.AuditLogs
            .AsNoTracking()
            .Select(a => a.EntityName)
            .Distinct()
            .OrderBy(name => name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<string>> GetUserNamesAsync(CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.AuditView);

        return await _db.AuditLogs
            .AsNoTracking()
            .Where(a => a.UserName != null)
            .Select(a => a.UserName!)
            .Distinct()
            .OrderBy(name => name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
