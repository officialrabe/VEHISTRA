using Vehistra.Application.Abstractions;
using Vehistra.Application.Common;
using Vehistra.Application.Dtos;
using Vehistra.Domain.Entities;
using Vehistra.Domain.Enums;
using Vehistra.Domain.Exceptions;
using Vehistra.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace Vehistra.Application.Services;

/// <inheritdoc />
public sealed class WorkshopService : IWorkshopService
{
    private readonly IVehistraDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly NumberGenerator _numbers;
    private readonly IMileageService _mileage;
    private readonly IClock _clock;

    public WorkshopService(
        IVehistraDbContext db,
        ICurrentUserService currentUser,
        NumberGenerator numbers,
        IMileageService mileage,
        IClock clock)
    {
        _db = db;
        _currentUser = currentUser;
        _numbers = numbers;
        _mileage = mileage;
        _clock = clock;
    }

    public async Task<IReadOnlyList<WorkshopOrderListItem>> GetOrdersAsync(
        WorkshopFilter filter,
        CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.WorkshopView);

        var query = _db.WorkshopOrders.AsNoTracking().AsQueryable();

        if (filter.VehicleId is { } vehicleId)
        {
            query = query.Where(w => w.VehicleId == vehicleId);
        }

        if (filter.WorkshopId is { } workshopId)
        {
            query = query.Where(w => w.WorkshopId == workshopId);
        }

        if (filter.DriverId is { } fahrerId)
        {
            query = query.Where(w => w.DriverId == fahrerId);
        }

        if (filter.Status is { } status)
        {
            query = query.Where(w => w.Status == status);
        }
        else if (filter.OnlyInWorkshop)
        {
            query = query.Where(w =>
                w.Status == WorkshopOrderStatus.FahrzeugAbgegeben ||
                w.Status == WorkshopOrderStatus.InBearbeitung ||
                w.Status == WorkshopOrderStatus.WartetAufTeile ||
                w.Status == WorkshopOrderStatus.Fertig);
        }
        else if (filter.OnlyOpen)
        {
            query = query.Where(w => w.Status != WorkshopOrderStatus.Abgeholt && w.Status != WorkshopOrderStatus.Storniert);
        }

        if (filter.MinDaysInWorkshop is { } mindestTage)
        {
            // Der Stichtag steht fest, damit die Datenbank vergleichen kann.
            var stichtag = _clock.Today.AddDays(-mindestTage);
            query = query.Where(w => w.VehicleHandedOverAt != null && w.VehicleHandedOverAt <= stichtag);
        }

        if (filter.From is { } from)
        {
            query = query.Where(w => w.CreatedOn >= from);
        }

        if (filter.To is { } to)
        {
            query = query.Where(w => w.CreatedOn <= to);
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchText))
        {
            var text = filter.SearchText.Trim();
            query = query.Where(w =>
                w.OrderNumber.Contains(text) ||
                (w.InvoiceNumber != null && w.InvoiceNumber.Contains(text)) ||
                (w.Reason != null && w.Reason.Contains(text)) ||
                (w.Vehicle != null && w.Vehicle.InternalNumber.Contains(text)) ||
                (w.Vehicle != null && w.Vehicle.LicensePlate != null && w.Vehicle.LicensePlate.Contains(text)));
        }

        var now = _clock.Now;

        var rows = await query
            .OrderByDescending(w => w.CreatedOn)
            .Select(w => new
            {
                w.Id,
                w.OrderNumber,
                w.VehicleId,
                VehicleDisplay = w.Vehicle != null
                    ? (w.Vehicle.LicensePlate ?? w.Vehicle.InternalNumber) + " - " + w.Vehicle.Manufacturer + " " + w.Vehicle.Model
                    : string.Empty,
                WorkshopName = w.Workshop != null ? w.Workshop.Name : null,
                DriverName = w.Driver != null ? w.Driver.FirstName + " " + w.Driver.LastName : null,
                w.CreatedOn,
                w.AppointmentDate,
                w.Status,
                w.CompletedAt,
                w.CostNet,
                w.InvoiceNumber,
                w.VehicleHandedOverAt,
                w.PickedUpAt
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.Select(r => new WorkshopOrderListItem(
            r.Id, r.OrderNumber, r.VehicleId, r.VehicleDisplay, r.WorkshopName, r.DriverName,
            r.CreatedOn, r.AppointmentDate, r.Status, r.CompletedAt, r.CostNet, r.InvoiceNumber,
            r.VehicleHandedOverAt is null
                ? null
                : (int)((r.PickedUpAt ?? now) - r.VehicleHandedOverAt.Value).TotalDays)).ToList();
    }

    public async Task<WorkshopOrder?> GetOrderAsync(int id, CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.WorkshopView);

        return await _db.WorkshopOrders
            .Include(w => w.Vehicle)
            .Include(w => w.Driver)
            .Include(w => w.Workshop)
            .Include(w => w.Tasks.OrderBy(t => t.Position))
            .Include(w => w.Damages)
            .Include(w => w.Documents)
                .ThenInclude(d => d.Document)
            .FirstOrDefaultAsync(w => w.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> CreateOrderAsync(
        WorkshopOrder order,
        IEnumerable<int>? damageIds = null,
        CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.WorkshopManage);

        var vehicle = await _db.Vehicles
            .FirstOrDefaultAsync(v => v.Id == order.VehicleId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EntityNotFoundException(nameof(Vehicle), order.VehicleId);

        if (string.IsNullOrWhiteSpace(order.OrderNumber))
        {
            order.OrderNumber = await _numbers.NextWorkshopOrderNumberAsync(cancellationToken).ConfigureAwait(false);
        }

        if (order.CreatedOn == default)
        {
            order.CreatedOn = _clock.Now;
        }

        order.DriverId ??= vehicle.CurrentDriverId;

        _db.WorkshopOrders.Add(order);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (damageIds is not null)
        {
            var ids = damageIds.Distinct().ToList();
            var damages = await _db.DamageReports
                .Where(d => ids.Contains(d.Id))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var position = await _db.WorkshopTasks
                .Where(t => t.WorkshopOrderId == order.Id)
                .CountAsync(cancellationToken)
                .ConfigureAwait(false);

            foreach (var damage in damages)
            {
                damage.WorkshopOrderId = order.Id;
                damage.WorkshopId = order.WorkshopId;

                if (damage.Status is DamageStatus.Gemeldet or DamageStatus.Geprueft)
                {
                    damage.Status = DamageStatus.ReparaturGeplant;
                }

                _db.WorkshopTasks.Add(new WorkshopTask
                {
                    WorkshopOrderId = order.Id,
                    Position = ++position,
                    Description = $"{damage.DamageNumber}: {damage.Description}",
                    DamageReportId = damage.Id
                });
            }

            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return order.Id;
    }

    public async Task UpdateOrderAsync(WorkshopOrder order, CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.WorkshopManage);

        var existing = await _db.WorkshopOrders
            .FirstOrDefaultAsync(w => w.Id == order.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EntityNotFoundException(nameof(WorkshopOrder), order.Id);

        existing.DriverId = order.DriverId;
        existing.WorkshopId = order.WorkshopId;
        existing.ProposedDate = order.ProposedDate;
        existing.AppointmentDate = order.AppointmentDate;
        existing.Reason = order.Reason;
        existing.WorkToPerform = order.WorkToPerform;
        existing.Status = order.Status;
        existing.VehicleHandedOverAt = order.VehicleHandedOverAt;
        existing.PlannedCompletionAt = order.PlannedCompletionAt;
        existing.CompletedAt = order.CompletedAt;
        existing.PickedUpAt = order.PickedUpAt;
        existing.MileageAtHandover = order.MileageAtHandover;
        existing.CostNet = order.CostNet;
        existing.CostGross = order.CostGross;
        existing.InvoiceNumber = order.InvoiceNumber;
        existing.InvoiceDocumentId = order.InvoiceDocumentId;
        existing.NextServiceMileage = order.NextServiceMileage;
        existing.Comment = order.Comment;
        existing.RowVersion = order.RowVersion;

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task ChangeStatusAsync(
        int orderId,
        WorkshopOrderStatus status,
        CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.WorkshopManage);

        var order = await _db.WorkshopOrders
            .FirstOrDefaultAsync(w => w.Id == orderId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EntityNotFoundException(nameof(WorkshopOrder), orderId);

        order.Status = status;

        switch (status)
        {
            case WorkshopOrderStatus.FahrzeugAbgegeben:
                order.VehicleHandedOverAt ??= _clock.Now;
                break;

            case WorkshopOrderStatus.Fertig:
                order.CompletedAt ??= _clock.Now;
                break;

            case WorkshopOrderStatus.Abgeholt:
                order.CompletedAt ??= _clock.Now;
                order.PickedUpAt ??= _clock.Now;
                break;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task RecordFeedbackAsync(WorkshopFeedback feedback, CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.WorkshopManage);

        var order = await _db.WorkshopOrders
            .Include(w => w.Tasks)
            .FirstOrDefaultAsync(w => w.Id == feedback.OrderId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EntityNotFoundException(nameof(WorkshopOrder), feedback.OrderId);

        order.CostNet = feedback.CostNet ?? order.CostNet;
        order.CostGross = feedback.CostGross ?? order.CostGross;
        order.CompletedAt = feedback.CompletedAt ?? order.CompletedAt;
        order.NextServiceMileage = feedback.NextServiceMileage ?? order.NextServiceMileage;
        order.InvoiceNumber = feedback.InvoiceNumber ?? order.InvoiceNumber;
        order.MileageAtHandover = feedback.MileageAtHandover ?? order.MileageAtHandover;

        if (!string.IsNullOrWhiteSpace(feedback.Comment))
        {
            order.Comment = string.IsNullOrWhiteSpace(order.Comment)
                ? feedback.Comment
                : $"{order.Comment}{Environment.NewLine}{feedback.Comment}";
        }

        var completed = feedback.CompletedTaskIds.ToHashSet();
        foreach (var task in order.Tasks)
        {
            if (completed.Contains(task.Id) && !task.IsCompleted)
            {
                task.IsCompleted = true;
                task.CompletedAt = feedback.CompletedAt ?? _clock.Now;
            }
        }

        if (order.Status is not (WorkshopOrderStatus.Abgeholt or WorkshopOrderStatus.Storniert)
            && order.CompletedAt is not null)
        {
            order.Status = WorkshopOrderStatus.Fertig;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Schaeden, deren Arbeiten erledigt sind, auf "Repariert" setzen.
        var completedDamageIds = order.Tasks
            .Where(t => t.IsCompleted && t.DamageReportId is not null)
            .Select(t => t.DamageReportId!.Value)
            .ToList();

        if (completedDamageIds.Count > 0)
        {
            var damages = await _db.DamageReports
                .Where(d => completedDamageIds.Contains(d.Id) && d.Status != DamageStatus.Geschlossen)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            foreach (var damage in damages)
            {
                damage.Status = DamageStatus.Repariert;
                damage.RepairedAt ??= order.CompletedAt ?? _clock.Now;
            }

            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        if (feedback.MileageAtHandover is { } mileage && mileage > 0)
        {
            await _mileage.AddAsync(
                order.VehicleId, mileage, order.CompletedAt ?? _clock.Now,
                MileageSource.Werkstatt, $"Rueckmeldung Werkstattauftrag {order.OrderNumber}",
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<int> AddTaskAsync(WorkshopTask task, CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.WorkshopManage);

        task.Description = Guard.NotEmpty(task.Description, "Beschreibung");

        if (task.Position <= 0)
        {
            task.Position = await _db.WorkshopTasks
                .Where(t => t.WorkshopOrderId == task.WorkshopOrderId)
                .CountAsync(cancellationToken)
                .ConfigureAwait(false) + 1;
        }

        _db.WorkshopTasks.Add(task);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return task.Id;
    }

    public async Task UpdateTaskAsync(WorkshopTask task, CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.WorkshopManage);

        var existing = await _db.WorkshopTasks
            .FirstOrDefaultAsync(t => t.Id == task.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EntityNotFoundException(nameof(WorkshopTask), task.Id);

        existing.Description = Guard.NotEmpty(task.Description, "Beschreibung");
        existing.Position = task.Position;
        existing.Cost = task.Cost;
        existing.Comment = task.Comment;
        existing.DamageReportId = task.DamageReportId;

        if (task.IsCompleted && !existing.IsCompleted)
        {
            existing.IsCompleted = true;
            existing.CompletedAt = task.CompletedAt ?? _clock.Now;
        }
        else if (!task.IsCompleted && existing.IsCompleted)
        {
            existing.IsCompleted = false;
            existing.CompletedAt = null;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task RemoveTaskAsync(int taskId, CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.WorkshopManage);

        var task = await _db.WorkshopTasks
            .FirstOrDefaultAsync(t => t.Id == taskId, cancellationToken)
            .ConfigureAwait(false);

        if (task is null)
        {
            return;
        }

        _db.WorkshopTasks.Remove(task);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Workshop>> GetWorkshopsAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Workshops.AsNoTracking().Where(w => !w.IsArchived);

        if (!includeInactive)
        {
            query = query.Where(w => w.IsActive);
        }

        return await query
            .OrderBy(w => w.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> CreateWorkshopAsync(Workshop workshop, CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.SettingsManage);

        workshop.Name = Guard.NotEmpty(workshop.Name, "Name der Werkstatt");

        _db.Workshops.Add(workshop);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return workshop.Id;
    }

    public async Task UpdateWorkshopAsync(Workshop workshop, CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.SettingsManage);

        var existing = await _db.Workshops
            .FirstOrDefaultAsync(w => w.Id == workshop.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EntityNotFoundException(nameof(Workshop), workshop.Id);

        existing.Name = Guard.NotEmpty(workshop.Name, "Name der Werkstatt");
        existing.Street = workshop.Street;
        existing.PostalCode = workshop.PostalCode;
        existing.City = workshop.City;
        existing.Phone = workshop.Phone;
        existing.Email = workshop.Email;
        existing.ContactPerson = workshop.ContactPerson;
        existing.Comment = workshop.Comment;
        existing.IsActive = workshop.IsActive;
        existing.RowVersion = workshop.RowVersion;

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
