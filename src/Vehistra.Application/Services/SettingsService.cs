using System.Collections.Concurrent;
using System.Globalization;
using Vehistra.Application.Abstractions;
using Vehistra.Domain.Common;
using Vehistra.Domain.Entities;
using Vehistra.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace Vehistra.Application.Services;

/// <inheritdoc />
public sealed class SettingsService : ISettingsService
{
    private readonly IVehistraDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ConcurrentDictionary<string, string?> _cache = new(StringComparer.OrdinalIgnoreCase);
    private volatile bool _cacheLoaded;

    public SettingsService(IVehistraDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        await EnsureCacheAsync(cancellationToken).ConfigureAwait(false);
        return _cache.TryGetValue(key, out var value) ? value : null;
    }

    public async Task<string> GetOrDefaultAsync(string key, string defaultValue, CancellationToken cancellationToken = default)
    {
        var value = await GetAsync(key, cancellationToken).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
    }

    public async Task<int> GetIntAsync(string key, int defaultValue, CancellationToken cancellationToken = default)
    {
        var value = await GetAsync(key, cancellationToken).ConfigureAwait(false);
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : defaultValue;
    }

    public async Task<bool> GetBoolAsync(string key, bool defaultValue, CancellationToken cancellationToken = default)
    {
        var value = await GetAsync(key, cancellationToken).ConfigureAwait(false);
        return bool.TryParse(value, out var parsed) ? parsed : defaultValue;
    }

    public async Task SetAsync(string key, string? value, CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.SettingsManage);

        var setting = await _db.SystemSettings
            .FirstOrDefaultAsync(s => s.Key == key, cancellationToken)
            .ConfigureAwait(false);

        if (setting is null)
        {
            setting = new SystemSetting { Key = key, Value = value };
            _db.SystemSettings.Add(setting);
        }
        else
        {
            setting.Value = value;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _cache[key] = value;
    }

    public async Task<IReadOnlyList<SystemSetting>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _db.SystemSettings
            .AsNoTracking()
            .OrderBy(s => s.Category)
            .ThenBy(s => s.Key)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<CompanyProfile> GetCompanyProfileAsync(CancellationToken cancellationToken = default)
    {
        await EnsureCacheAsync(cancellationToken).ConfigureAwait(false);

        return new CompanyProfile
        {
            Name = await GetOrDefaultAsync(SettingsKeys.CompanyName, "Unternehmen", cancellationToken).ConfigureAwait(false),
            Street = await GetAsync(SettingsKeys.CompanyStreet, cancellationToken).ConfigureAwait(false),
            PostalCode = await GetAsync(SettingsKeys.CompanyPostalCode, cancellationToken).ConfigureAwait(false),
            City = await GetAsync(SettingsKeys.CompanyCity, cancellationToken).ConfigureAwait(false),
            Phone = await GetAsync(SettingsKeys.CompanyPhone, cancellationToken).ConfigureAwait(false),
            Email = await GetAsync(SettingsKeys.CompanyEmail, cancellationToken).ConfigureAwait(false),
            LogoPath = await GetAsync(SettingsKeys.CompanyLogoPath, cancellationToken).ConfigureAwait(false)
        };
    }

    public async Task<DueDateThresholds> GetInspectionThresholdsAsync(CancellationToken cancellationToken = default)
    {
        var critical = await GetIntAsync(SettingsKeys.InspectionWarnCriticalDays, 14, cancellationToken).ConfigureAwait(false);
        var warning = await GetIntAsync(SettingsKeys.InspectionWarnWarningDays, 30, cancellationToken).ConfigureAwait(false);
        var urgent = await GetIntAsync(SettingsKeys.InspectionWarnUrgentDays, 7, cancellationToken).ConfigureAwait(false);
        return new DueDateThresholds(critical, warning, urgent);
    }

    public void InvalidateCache()
    {
        _cacheLoaded = false;
        _cache.Clear();
    }

    private async Task EnsureCacheAsync(CancellationToken cancellationToken)
    {
        if (_cacheLoaded)
        {
            return;
        }

        var settings = await _db.SystemSettings
            .AsNoTracking()
            .Select(s => new { s.Key, s.Value })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var setting in settings)
        {
            _cache[setting.Key] = setting.Value;
        }

        _cacheLoaded = true;
    }
}
