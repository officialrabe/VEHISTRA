using Vehistra.Application.Abstractions;
using Vehistra.Application.Dtos;
using Vehistra.Domain.Common;
using Vehistra.Domain.Entities;
using Vehistra.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Vehistra.Application.Services;

/// <inheritdoc />
public sealed class NotificationService : INotificationService
{
    private readonly IVehistraDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ISettingsService _settings;
    private readonly IClock _clock;

    public NotificationService(
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

    public async Task<int> CreateAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        notification.CreatedAt = notification.CreatedAt == default ? _clock.Now : notification.CreatedAt;

        if (!string.IsNullOrWhiteSpace(notification.DeduplicationKey))
        {
            var exists = await _db.Notifications
                .AnyAsync(n => n.DeduplicationKey == notification.DeduplicationKey && !n.IsDismissed, cancellationToken)
                .ConfigureAwait(false);

            if (exists)
            {
                return 0;
            }
        }

        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return notification.Id;
    }

    public async Task<IReadOnlyList<NotificationListItem>> GetForCurrentUserAsync(
        bool onlyUnread = false,
        int maxCount = 100,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.User?.Id;

        var query = _db.Notifications
            .AsNoTracking()
            .Where(n => !n.IsDismissed && (n.TargetUserId == null || n.TargetUserId == userId));

        if (onlyUnread)
        {
            query = query.Where(n => !n.IsRead);
        }

        return await query
            .OrderByDescending(n => n.Severity)
            .ThenByDescending(n => n.CreatedAt)
            .Take(maxCount)
            .Select(n => new NotificationListItem(
                n.Id, n.Category, n.Severity, n.Title, n.Message, n.CreatedAt, n.DueDate, n.VehicleId, n.IsRead))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> GetUnreadCountAsync(CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.User?.Id;

        return await _db.Notifications
            .CountAsync(n => !n.IsDismissed && !n.IsRead && (n.TargetUserId == null || n.TargetUserId == userId),
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task MarkAsReadAsync(int notificationId, CancellationToken cancellationToken = default)
    {
        var notification = await _db.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId, cancellationToken)
            .ConfigureAwait(false);

        if (notification is null || notification.IsRead)
        {
            return;
        }

        notification.IsRead = true;
        notification.ReadAt = _clock.Now;
        notification.ReadByUserId = _currentUser.User?.Id;

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task MarkAllAsReadAsync(CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.User?.Id;

        var open = await _db.Notifications
            .Where(n => !n.IsRead && !n.IsDismissed && (n.TargetUserId == null || n.TargetUserId == userId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var notification in open)
        {
            notification.IsRead = true;
            notification.ReadAt = _clock.Now;
            notification.ReadByUserId = userId;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DismissAsync(int notificationId, CancellationToken cancellationToken = default)
    {
        var notification = await _db.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId, cancellationToken)
            .ConfigureAwait(false);

        if (notification is null)
        {
            return;
        }

        notification.IsDismissed = true;
        notification.DismissedAt = _clock.Now;

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> RefreshDueNotificationsAsync(CancellationToken cancellationToken = default)
    {
        var today = _clock.Today;
        var thresholds = await _settings.GetInspectionThresholdsAsync(cancellationToken).ConfigureAwait(false);
        var created = 0;

        // Hauptuntersuchung
        var inspectionLimit = today.AddDays(thresholds.WarningDays);
        var dueInspections = await _db.Vehicles
            .AsNoTracking()
            .Where(v => !v.IsRetired && v.NextInspectionDue != null && v.NextInspectionDue <= inspectionLimit)
            .Select(v => new { v.Id, v.InternalNumber, v.LicensePlate, v.Manufacturer, v.Model, v.NextInspectionDue })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var vehicle in dueInspections)
        {
            var due = vehicle.NextInspectionDue!.Value;
            var level = DueDateCalculator.Evaluate(due, today, thresholds);
            var display = string.IsNullOrWhiteSpace(vehicle.LicensePlate) ? vehicle.InternalNumber : vehicle.LicensePlate;

            created += await CreateAsync(new Notification
            {
                Category = NotificationCategory.Tuev,
                Severity = level == WarningLevel.Kritisch ? NotificationSeverity.Kritisch : NotificationSeverity.Warnung,
                // Der Titel haengt am Datum, nicht an der Warnstufe: kritisch ist
                // eine Frist auch schon kurz vor dem Termin - abgelaufen ist sie
                // dann aber noch nicht.
                Title = due.Date < today.Date ? "TÜV abgelaufen" : "TÜV wird fällig",
                Message = $"TÜV {display} {DueDateCalculator.Describe(due, today)}.",
                VehicleId = vehicle.Id,
                DueDate = due,
                SourceReference = $"Vehicle:{vehicle.Id}",
                DeduplicationKey = $"Inspection:{vehicle.Id}:{due:yyyyMMdd}"
            }, cancellationToken).ConfigureAwait(false) > 0 ? 1 : 0;
        }

        // Kennzeichenreservierungen
        var warnDaysRaw = await _settings
            .GetOrDefaultAsync(SettingsKeys.PlateReservationWarnDays, "30;14;7;3;1", cancellationToken)
            .ConfigureAwait(false);

        var warnDays = warnDaysRaw
            .Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => int.TryParse(s, out var value) ? value : -1)
            .Where(v => v >= 0)
            .DefaultIfEmpty(30)
            .ToArray();

        var maxWarnDays = warnDays.Max();
        var reservationLimit = today.AddDays(maxWarnDays);

        var reservations = await _db.LicensePlateReservations
            .AsNoTracking()
            .Include(r => r.LicensePlate)
            .Where(r => !r.IsReleased && r.ReservedUntil <= reservationLimit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var reservation in reservations)
        {
            var remaining = (reservation.ReservedUntil.Date - today).Days;
            var isExpired = remaining < 0;

            if (!isExpired && !warnDays.Contains(remaining))
            {
                continue;
            }

            created += await CreateAsync(new Notification
            {
                Category = NotificationCategory.Kennzeichenreservierung,
                Severity = isExpired ? NotificationSeverity.Kritisch : NotificationSeverity.Warnung,
                Title = isExpired ? "Kennzeichenreservierung abgelaufen" : "Kennzeichenreservierung laeuft ab",
                Message = isExpired
                    ? $"Die Reservierung fuer {reservation.LicensePlate?.Plate} ist seit {Math.Abs(remaining)} Tag(en) abgelaufen."
                    : $"Kennzeichen {reservation.LicensePlate?.Plate} muss in {remaining} Tag(en) verlängert werden.",
                DueDate = reservation.ReservedUntil,
                SourceReference = $"LicensePlateReservation:{reservation.Id}",
                DeduplicationKey = $"PlateReservation:{reservation.Id}:{remaining}"
            }, cancellationToken).ConfigureAwait(false) > 0 ? 1 : 0;
        }

        // Versicherungen
        var insuranceLimit = today.AddDays(30);
        var insurances = await _db.VehicleInsurances
            .AsNoTracking()
            .Include(i => i.Vehicle)
            .Where(i => i.IsActive && i.ValidTo != null && i.ValidTo <= insuranceLimit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var insurance in insurances)
        {
            created += await CreateAsync(new Notification
            {
                Category = NotificationCategory.Versicherung,
                Severity = insurance.ValidTo < today ? NotificationSeverity.Kritisch : NotificationSeverity.Warnung,
                Title = "Versicherung laeuft ab",
                Message = $"Die Versicherung ({insurance.Company}) fuer {insurance.Vehicle?.DisplayName} " +
                          $"{DueDateCalculator.Describe(insurance.ValidTo, today)}.",
                VehicleId = insurance.VehicleId,
                DueDate = insurance.ValidTo,
                SourceReference = $"VehicleInsurance:{insurance.Id}",
                DeduplicationKey = $"Insurance:{insurance.Id}:{insurance.ValidTo:yyyyMMdd}"
            }, cancellationToken).ConfigureAwait(false) > 0 ? 1 : 0;
        }

        // Werkstatttermine heute
        var appointments = await _db.WorkshopOrders
            .AsNoTracking()
            .Include(w => w.Vehicle)
            .Where(w => w.AppointmentDate != null
                        && w.AppointmentDate >= today
                        && w.AppointmentDate < today.AddDays(1)
                        && w.Status != WorkshopOrderStatus.Storniert
                        && w.Status != WorkshopOrderStatus.Abgeholt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var order in appointments)
        {
            created += await CreateAsync(new Notification
            {
                Category = NotificationCategory.Werkstatt,
                Severity = NotificationSeverity.Information,
                Title = "Werkstatttermin heute",
                Message = $"{order.Vehicle?.DisplayName} hat heute um {order.AppointmentDate:HH:mm} einen Werkstatttermin.",
                VehicleId = order.VehicleId,
                DueDate = order.AppointmentDate,
                SourceReference = $"WorkshopOrder:{order.Id}",
                DeduplicationKey = $"WorkshopAppointment:{order.Id}:{order.AppointmentDate:yyyyMMdd}"
            }, cancellationToken).ConfigureAwait(false) > 0 ? 1 : 0;
        }

        return created;
    }
}
