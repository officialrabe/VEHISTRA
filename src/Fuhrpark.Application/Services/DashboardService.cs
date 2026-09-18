using Fuhrpark.Application.Abstractions;
using Fuhrpark.Application.Dtos;
using Fuhrpark.Domain.Common;
using Fuhrpark.Domain.Enums;
using Fuhrpark.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace Fuhrpark.Application.Services;

/// <inheritdoc />
public sealed class DashboardService : IDashboardService
{
    private readonly IFuhrparkDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ISettingsService _settings;
    private readonly IMaintenanceService _maintenance;
    private readonly IClock _clock;

    public DashboardService(
        IFuhrparkDbContext db,
        ICurrentUserService currentUser,
        ISettingsService settings,
        IMaintenanceService maintenance,
        IClock clock)
    {
        _db = db;
        _currentUser = currentUser;
        _settings = settings;
        _maintenance = maintenance;
        _clock = clock;
    }

    public async Task<DashboardData> GetAsync(CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.VehicleView);

        var today = _clock.Today;
        var thresholds = await _settings.GetInspectionThresholdsAsync(cancellationToken).ConfigureAwait(false);

        var vehicles = await _db.Vehicles
            .AsNoTracking()
            .Select(v => new
            {
                v.Id,
                v.InternalNumber,
                v.LicensePlate,
                v.Manufacturer,
                v.Model,
                v.IsRetired,
                v.IsRegistered,
                v.CurrentDriverId,
                v.NextInspectionDue,
                StatusKind = v.Status != null ? v.Status.Kind : null,
                OpenDamages = v.Damages.Count(d => d.Status != DamageStatus.Geschlossen),
                CriticalDamages = v.Damages.Count(d =>
                    d.Status != DamageStatus.Geschlossen && d.Priority == DamagePriority.Kritisch),
                NotDriveableDamages = v.Damages.Count(d => d.Status != DamageStatus.Geschlossen && !d.IsDriveable),
                InWorkshop = v.WorkshopOrders.Any(w =>
                    w.Status == WorkshopOrderStatus.FahrzeugAbgegeben ||
                    w.Status == WorkshopOrderStatus.InBearbeitung ||
                    w.Status == WorkshopOrderStatus.WartetAufTeile ||
                    w.Status == WorkshopOrderStatus.Fertig)
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var active = vehicles.Where(v => !v.IsRetired).ToList();

        var vehicleCounters = new VehicleCounters
        {
            Total = vehicles.Count,
            Active = active.Count(v => v.StatusKind is VehicleStatusKind.Aktiv
                or VehicleStatusKind.Verfuegbar or VehicleStatusKind.ImEinsatz),
            Available = active.Count(v => v.StatusKind == VehicleStatusKind.Verfuegbar),
            InUse = active.Count(v => v.StatusKind == VehicleStatusKind.ImEinsatz),
            WithoutDriver = active.Count(v => v.CurrentDriverId is null),
            InWorkshop = active.Count(v => v.InWorkshop || v.StatusKind == VehicleStatusKind.Werkstatt),
            Damaged = active.Count(v => v.OpenDamages > 0),
            NotDriveable = active.Count(v =>
                v.NotDriveableDamages > 0 || v.StatusKind == VehicleStatusKind.NichtFahrbereit),
            Deregistered = vehicles.Count(v => !v.IsRegistered && !v.IsRetired),
            Retired = vehicles.Count(v => v.IsRetired)
        };

        var inspectionSource = active
            .Where(v => v.NextInspectionDue is not null)
            .Select(v => new { v.Id, v.InternalNumber, v.LicensePlate, Due = v.NextInspectionDue!.Value })
            .ToList();

        var inspectionCounters = new InspectionCounters
        {
            Expired = inspectionSource.Count(v => v.Due.Date < today),
            Within7Days = inspectionSource.Count(v => v.Due.Date >= today && (v.Due.Date - today).Days <= 7),
            Within14Days = inspectionSource.Count(v => v.Due.Date >= today && (v.Due.Date - today).Days <= 14),
            Within30Days = inspectionSource.Count(v => v.Due.Date >= today && (v.Due.Date - today).Days <= 30),
            Within60Days = inspectionSource.Count(v => v.Due.Date >= today && (v.Due.Date - today).Days <= 60)
        };

        var damageRows = await _db.DamageReports
            .AsNoTracking()
            .Where(d => d.Status != DamageStatus.Geschlossen)
            .Select(d => new { d.Id, d.Status, d.Priority, d.IsDriveable, d.CreatedAt })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var damageCounters = new DamageCounters
        {
            Open = damageRows.Count,
            New = damageRows.Count(d => d.Status == DamageStatus.Gemeldet),
            Critical = damageRows.Count(d => d.Priority == DamagePriority.Kritisch),
            NotDriveable = damageRows.Count(d => !d.IsDriveable)
        };

        var orderRows = await _db.WorkshopOrders
            .AsNoTracking()
            .Where(w => w.Status != WorkshopOrderStatus.Abgeholt && w.Status != WorkshopOrderStatus.Storniert)
            .Select(w => new
            {
                w.Id,
                w.Status,
                w.AppointmentDate,
                w.PlannedCompletionAt,
                w.VehicleHandedOverAt,
                w.OrderNumber,
                w.VehicleId,
                VehicleDisplay = w.Vehicle != null
                    ? (w.Vehicle.LicensePlate ?? w.Vehicle.InternalNumber)
                    : string.Empty
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var workshopCounters = new WorkshopCounters
        {
            CurrentlyInWorkshop = orderRows.Count(w => w.Status is WorkshopOrderStatus.FahrzeugAbgegeben
                or WorkshopOrderStatus.InBearbeitung or WorkshopOrderStatus.WartetAufTeile or WorkshopOrderStatus.Fertig),
            AppointmentsToday = orderRows.Count(w => w.AppointmentDate?.Date == today),
            UpcomingAppointments = orderRows.Count(w => w.AppointmentDate?.Date > today),
            Overdue = orderRows.Count(w =>
                (w.PlannedCompletionAt is not null && w.PlannedCompletionAt.Value.Date < today
                 && w.Status != WorkshopOrderStatus.Fertig)
                || (w.AppointmentDate is not null && w.AppointmentDate.Value.Date < today
                    && w.Status == WorkshopOrderStatus.TerminVereinbart))
        };

        var plateRows = await _db.LicensePlates
            .AsNoTracking()
            .Select(p => new
            {
                p.Id,
                p.Plate,
                p.Status,
                ReservedUntil = p.Reservations
                    .Where(r => !r.IsReleased)
                    .OrderByDescending(r => r.ReservedUntil)
                    .Select(r => (DateTime?)r.ReservedUntil)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var plateCounters = new LicensePlateCounters
        {
            Available = plateRows.Count(p => p.Status == LicensePlateStatus.Verfuegbar),
            Reserved = plateRows.Count(p => p.Status == LicensePlateStatus.Reserviert),
            ReservationsExpiringSoon = plateRows.Count(p =>
                p.ReservedUntil is not null && p.ReservedUntil.Value.Date >= today
                && (p.ReservedUntil.Value.Date - today).Days <= 14),
            ReservationsExpired = plateRows.Count(p =>
                p.ReservedUntil is not null && p.ReservedUntil.Value.Date < today)
        };

        var maintenanceDue = _currentUser.HasPermission(Permissions.MaintenanceView)
            ? await _maintenance.GetDueItemsAsync(cancellationToken: cancellationToken).ConfigureAwait(false)
            : [];

        var maintenanceCounters = new MaintenanceCounters
        {
            ServiceDue = maintenanceDue.Count(m => m.Level == WarningLevel.Kritisch),
            ServiceDueSoon = maintenanceDue.Count(m => m.Level is WarningLevel.BaldFaellig or WarningLevel.Hinweis),
            MileageIntervalReached = maintenanceDue.Count(m =>
                m.DueMileage is not null && m.CurrentMileage >= m.DueMileage)
        };

        // Bereich "Aufmerksamkeit erforderlich"
        var attention = new List<AttentionItem>();

        foreach (var vehicle in inspectionSource.Where(v => (v.Due.Date - today).Days <= thresholds.WarningDays))
        {
            var display = string.IsNullOrWhiteSpace(vehicle.LicensePlate) ? vehicle.InternalNumber : vehicle.LicensePlate;
            attention.Add(new AttentionItem(
                NotificationCategory.Tuev,
                DueDateCalculator.Evaluate(vehicle.Due, today, thresholds),
                $"TUEV {display} {DueDateCalculator.Describe(vehicle.Due, today)}.",
                vehicle.Id, $"Vehicle:{vehicle.Id}", vehicle.Due));
        }

        foreach (var plate in plateRows.Where(p => p.ReservedUntil is not null
                                                   && (p.ReservedUntil.Value.Date - today).Days <= 30))
        {
            var days = (plate.ReservedUntil!.Value.Date - today).Days;
            attention.Add(new AttentionItem(
                NotificationCategory.Kennzeichenreservierung,
                days < 0 ? WarningLevel.Kritisch : days <= 7 ? WarningLevel.BaldFaellig : WarningLevel.Hinweis,
                days < 0
                    ? $"Reservierung fuer Kennzeichen {plate.Plate} ist seit {Math.Abs(days)} Tag(en) abgelaufen."
                    : $"Kennzeichen {plate.Plate} muss in {days} Tag(en) verlaengert werden.",
                null, $"LicensePlate:{plate.Id}", plate.ReservedUntil));
        }

        var criticalDamages = await _db.DamageReports
            .AsNoTracking()
            .Where(d => d.Status != DamageStatus.Geschlossen
                        && (d.Priority == DamagePriority.Kritisch || !d.IsDriveable))
            .Select(d => new
            {
                d.Id,
                d.VehicleId,
                d.Description,
                d.IsDriveable,
                d.OccurredAt,
                VehicleDisplay = d.Vehicle != null ? (d.Vehicle.LicensePlate ?? d.Vehicle.InternalNumber) : string.Empty
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        attention.AddRange(criticalDamages.Select(d => new AttentionItem(
            NotificationCategory.Schaden,
            WarningLevel.Kritisch,
            d.IsDriveable
                ? $"{d.VehicleDisplay} besitzt einen kritischen Schaden."
                : $"{d.VehicleDisplay} ist nicht fahrbereit: {d.Description}",
            d.VehicleId, $"DamageReport:{d.Id}", d.OccurredAt)));

        foreach (var order in orderRows.Where(w => w.VehicleHandedOverAt is not null))
        {
            var days = (today - order.VehicleHandedOverAt!.Value.Date).Days;
            if (days < 7)
            {
                continue;
            }

            attention.Add(new AttentionItem(
                NotificationCategory.Werkstatt,
                days >= 14 ? WarningLevel.Kritisch : WarningLevel.BaldFaellig,
                $"{order.VehicleDisplay} befindet sich seit {days} Tagen in der Werkstatt.",
                order.VehicleId, $"WorkshopOrder:{order.Id}", order.VehicleHandedOverAt));
        }

        attention.AddRange(maintenanceDue
            .Where(m => m.Level == WarningLevel.Kritisch)
            .Select(m => new AttentionItem(
                NotificationCategory.Wartung, m.Level,
                $"{m.VehicleDisplay}: {m.RuleName} - {m.Description}",
                m.VehicleId, $"Maintenance:{m.VehicleId}", m.DueDate)));

        return new DashboardData
        {
            GeneratedAt = _clock.Now,
            Vehicles = vehicleCounters,
            Inspections = inspectionCounters,
            Damages = damageCounters,
            Workshop = workshopCounters,
            LicensePlates = plateCounters,
            Maintenance = maintenanceCounters,
            AttentionItems = attention
                .OrderByDescending(a => a.Level)
                .ThenBy(a => a.DueDate ?? DateTime.MaxValue)
                .Take(50)
                .ToList()
        };
    }
}
