using Vehistra.Application.Abstractions;
using Vehistra.Application.Common;
using Vehistra.Application.Dtos;
using Vehistra.Domain.Common;
using Vehistra.Domain.Entities;
using Vehistra.Domain.Enums;
using Vehistra.Domain.Exceptions;
using Vehistra.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace Vehistra.Application.Services;

/// <inheritdoc />
public sealed class MaintenanceService : IMaintenanceService
{
    private readonly IVehistraDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ISettingsService _settings;
    private readonly IClock _clock;

    public MaintenanceService(
        IVehistraDbContext db,
        ICurrentUserService currentUser,
        ISettingsService settings,
        IClock clock)
    {
        _db = db;
        _currentUser = currentUser;
        _settings = settings;
        _clock = clock;
    }

    public async Task<IReadOnlyList<MaintenanceRule>> GetRulesAsync(
        int? vehicleId = null,
        CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.MaintenanceView);

        var query = _db.MaintenanceRules.AsNoTracking().Where(r => r.IsActive);

        if (vehicleId is { } id)
        {
            query = query.Where(r => r.VehicleId == null || r.VehicleId == id);
        }

        return await query
            .OrderBy(r => r.SortOrder)
            .ThenBy(r => r.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> CreateRuleAsync(MaintenanceRule rule, CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.MaintenanceManage);

        rule.Name = Guard.NotEmpty(rule.Name, "Bezeichnung");
        ValidateRule(rule);

        _db.MaintenanceRules.Add(rule);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return rule.Id;
    }

    public async Task UpdateRuleAsync(MaintenanceRule rule, CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.MaintenanceManage);

        var existing = await _db.MaintenanceRules
            .FirstOrDefaultAsync(r => r.Id == rule.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EntityNotFoundException(nameof(MaintenanceRule), rule.Id);

        ValidateRule(rule);

        existing.Name = Guard.NotEmpty(rule.Name, "Bezeichnung");
        existing.Description = rule.Description;
        existing.IntervalType = rule.IntervalType;
        existing.IntervalMonths = rule.IntervalMonths;
        existing.IntervalKilometers = rule.IntervalKilometers;
        existing.WarnDaysBefore = rule.WarnDaysBefore;
        existing.WarnKilometersBefore = rule.WarnKilometersBefore;
        existing.VehicleId = rule.VehicleId;
        existing.VehicleCategoryId = rule.VehicleCategoryId;
        existing.IsActive = rule.IsActive;
        existing.SortOrder = rule.SortOrder;
        existing.RowVersion = rule.RowVersion;

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteRuleAsync(int ruleId, CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.MaintenanceManage);

        var rule = await _db.MaintenanceRules
            .FirstOrDefaultAsync(r => r.Id == ruleId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EntityNotFoundException(nameof(MaintenanceRule), ruleId);

        Guard.That(!rule.IsSystemRule, "Vordefinierte Wartungsregeln koennen deaktiviert, aber nicht geloescht werden.");

        var used = await _db.MaintenanceEntries
            .AnyAsync(e => e.MaintenanceRuleId == ruleId, cancellationToken)
            .ConfigureAwait(false);

        if (used)
        {
            // Historie bleibt erhalten - die Regel wird nur deaktiviert.
            rule.IsActive = false;
        }
        else
        {
            _db.MaintenanceRules.Remove(rule);
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<MaintenanceEntry>> GetEntriesAsync(
        int vehicleId,
        CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.MaintenanceView);

        return await _db.MaintenanceEntries
            .AsNoTracking()
            .Include(e => e.Rule)
            .Include(e => e.Workshop)
            .Where(e => e.VehicleId == vehicleId)
            .OrderByDescending(e => e.PerformedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> AddEntryAsync(MaintenanceEntry entry, CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.MaintenanceManage);

        var vehicleExists = await _db.Vehicles
            .AnyAsync(v => v.Id == entry.VehicleId, cancellationToken)
            .ConfigureAwait(false);
        Guard.That(vehicleExists, "Das Fahrzeug existiert nicht.");

        MaintenanceRule? rule = null;
        if (entry.MaintenanceRuleId is { } ruleId)
        {
            rule = await _db.MaintenanceRules
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == ruleId, cancellationToken)
                .ConfigureAwait(false);
        }

        Guard.That(rule is not null || !string.IsNullOrWhiteSpace(entry.Title),
            "Bitte eine Wartungsregel auswaehlen oder eine Bezeichnung angeben.");

        // Naechste Faelligkeit aus der Regel ableiten, sofern nicht manuell gesetzt.
        if (rule is not null)
        {
            if (entry.NextDueDate is null
                && rule.IntervalMonths is { } months
                && rule.IntervalType is MaintenanceIntervalType.Datum or MaintenanceIntervalType.DatumOderKilometer)
            {
                entry.NextDueDate = entry.PerformedAt.AddMonths(months);
            }

            if (entry.NextDueMileage is null
                && rule.IntervalKilometers is { } kilometers
                && entry.Mileage is { } mileage
                && rule.IntervalType is MaintenanceIntervalType.Kilometer or MaintenanceIntervalType.DatumOderKilometer)
            {
                entry.NextDueMileage = mileage + kilometers;
            }
        }

        _db.MaintenanceEntries.Add(entry);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return entry.Id;
    }

    public async Task<IReadOnlyList<MaintenanceDueItem>> GetDueItemsAsync(
        int? vehicleId = null,
        CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.MaintenanceView);

        var today = _clock.Today;
        var warnDays = await _settings.GetIntAsync(SettingsKeys.MaintenanceWarnDays, 30, cancellationToken)
            .ConfigureAwait(false);
        var warnKilometers = await _settings.GetIntAsync(SettingsKeys.MaintenanceWarnKilometers, 1000, cancellationToken)
            .ConfigureAwait(false);

        var vehicles = await _db.Vehicles
            .AsNoTracking()
            .Where(v => !v.IsRetired && (vehicleId == null || v.Id == vehicleId))
            .Select(v => new
            {
                v.Id,
                v.InternalNumber,
                v.LicensePlate,
                v.Manufacturer,
                v.Model,
                v.CurrentMileage,
                CategoryIds = v.CategoryAssignments.Select(a => a.VehicleCategoryId).ToList()
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var rules = await _db.MaintenanceRules
            .AsNoTracking()
            .Where(r => r.IsActive)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var vehicleIds = vehicles.Select(v => v.Id).ToList();

        var entries = await _db.MaintenanceEntries
            .AsNoTracking()
            .Where(e => vehicleIds.Contains(e.VehicleId))
            .Select(e => new
            {
                e.VehicleId,
                e.MaintenanceRuleId,
                e.PerformedAt,
                e.NextDueDate,
                e.NextDueMileage
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var result = new List<MaintenanceDueItem>();

        foreach (var vehicle in vehicles)
        {
            var display = $"{(string.IsNullOrWhiteSpace(vehicle.LicensePlate) ? vehicle.InternalNumber : vehicle.LicensePlate)} - {vehicle.Manufacturer} {vehicle.Model}".Trim();

            var applicableRules = rules.Where(r =>
                (r.VehicleId is null || r.VehicleId == vehicle.Id) &&
                (r.VehicleCategoryId is null || vehicle.CategoryIds.Contains(r.VehicleCategoryId.Value)));

            foreach (var rule in applicableRules)
            {
                var last = entries
                    .Where(e => e.VehicleId == vehicle.Id && e.MaintenanceRuleId == rule.Id)
                    .OrderByDescending(e => e.PerformedAt)
                    .FirstOrDefault();

                if (last is null)
                {
                    continue;
                }

                var level = WarningLevel.Ok;
                var descriptions = new List<string>();

                if (last.NextDueDate is { } dueDate)
                {
                    var days = (dueDate.Date - today).Days;
                    var dateLevel = days switch
                    {
                        < 0 => WarningLevel.Kritisch,
                        _ when days <= warnDays / 2 => WarningLevel.BaldFaellig,
                        _ when days <= warnDays => WarningLevel.Hinweis,
                        _ => WarningLevel.Ok
                    };

                    if (dateLevel > level)
                    {
                        level = dateLevel;
                    }

                    descriptions.Add(DueDateCalculator.Describe(dueDate, today));
                }

                if (last.NextDueMileage is { } dueMileage)
                {
                    var remaining = dueMileage - vehicle.CurrentMileage;
                    var mileageLevel = remaining switch
                    {
                        <= 0 => WarningLevel.Kritisch,
                        _ when remaining <= warnKilometers / 2 => WarningLevel.BaldFaellig,
                        _ when remaining <= warnKilometers => WarningLevel.Hinweis,
                        _ => WarningLevel.Ok
                    };

                    if (mileageLevel > level)
                    {
                        level = mileageLevel;
                    }

                    descriptions.Add(remaining <= 0
                        ? $"Kilometerintervall um {Math.Abs(remaining):N0} km ueberschritten"
                        : $"noch {remaining:N0} km bis {dueMileage:N0} km");
                }

                if (level == WarningLevel.Ok || descriptions.Count == 0)
                {
                    continue;
                }

                result.Add(new MaintenanceDueItem(
                    vehicle.Id, display, rule.Id, rule.Name,
                    last.NextDueDate, last.NextDueMileage, vehicle.CurrentMileage,
                    level, string.Join(" · ", descriptions)));
            }
        }

        return result
            .OrderByDescending(r => r.Level)
            .ThenBy(r => r.DueDate ?? DateTime.MaxValue)
            .ToList();
    }

    private static void ValidateRule(MaintenanceRule rule)
    {
        switch (rule.IntervalType)
        {
            case MaintenanceIntervalType.Datum:
                Guard.That(rule.IntervalMonths is > 0,
                    "Bei einer datumsbasierten Regel muss ein Intervall in Monaten angegeben werden.");
                break;

            case MaintenanceIntervalType.Kilometer:
                Guard.That(rule.IntervalKilometers is > 0,
                    "Bei einer kilometerbasierten Regel muss ein Intervall in Kilometern angegeben werden.");
                break;

            case MaintenanceIntervalType.DatumOderKilometer:
                Guard.That(rule.IntervalMonths is > 0 || rule.IntervalKilometers is > 0,
                    "Es muss mindestens ein Intervall (Monate oder Kilometer) angegeben werden.");
                break;
        }
    }
}
