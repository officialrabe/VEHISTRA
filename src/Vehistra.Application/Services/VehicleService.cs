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
public sealed class VehicleService : IVehicleService
{
    private readonly IVehistraDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ISettingsService _settings;
    private readonly IClock _clock;

    public VehicleService(
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

    public async Task<PagedResult<VehicleListItem>> GetListAsync(
        VehicleFilter filter,
        CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.VehicleView);

        var thresholds = await _settings.GetInspectionThresholdsAsync(cancellationToken).ConfigureAwait(false);
        var today = _clock.Today;

        var query = _db.Vehicles.AsNoTracking().AsQueryable();

        if (filter.IsRetired is { } retired)
        {
            query = query.Where(v => v.IsRetired == retired);
        }

        if (filter.IsRegistered is { } registered)
        {
            query = query.Where(v => v.IsRegistered == registered);
        }

        if (filter.StatusId is { } statusId)
        {
            query = query.Where(v => v.VehicleStatusId == statusId);
        }

        if (filter.DriverId is { } driverId)
        {
            query = query.Where(v => v.CurrentDriverId == driverId);
        }

        if (filter.CategoryId is { } categoryId)
        {
            query = query.Where(v => v.CategoryAssignments.Any(a => a.VehicleCategoryId == categoryId));
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchText))
        {
            var text = filter.SearchText.Trim();
            query = query.Where(v =>
                v.InternalNumber.Contains(text) ||
                (v.LicensePlate != null && v.LicensePlate.Contains(text)) ||
                (v.Vin != null && v.Vin.Contains(text)) ||
                v.Manufacturer.Contains(text) ||
                v.Model.Contains(text) ||
                (v.CurrentDriver != null &&
                    (v.CurrentDriver.FirstName.Contains(text) || v.CurrentDriver.LastName.Contains(text))));
        }

        if (filter.OnlyWithOpenDamages)
        {
            query = query.Where(v => v.Damages.Any(d => d.Status != DamageStatus.Geschlossen));
        }

        if (filter.OnlyInWorkshop)
        {
            query = query.Where(v => v.WorkshopOrders.Any(w =>
                w.Status == WorkshopOrderStatus.FahrzeugAbgegeben ||
                w.Status == WorkshopOrderStatus.InBearbeitung ||
                w.Status == WorkshopOrderStatus.WartetAufTeile ||
                w.Status == WorkshopOrderStatus.Fertig));
        }

        if (filter.OnlyInspectionExpired)
        {
            query = query.Where(v => v.NextInspectionDue != null && v.NextInspectionDue < today);
        }
        else if (filter.OnlyInspectionDue)
        {
            query = query.Where(v => v.NextInspectionDue != null && v.NextInspectionDue <= today);
        }
        else if (filter.InspectionDueWithinDays is { } days)
        {
            var limit = today.AddDays(days);
            query = query.Where(v => v.NextInspectionDue != null && v.NextInspectionDue <= limit);
        }

        if (!string.IsNullOrWhiteSpace(filter.Manufacturer))
        {
            var hersteller = filter.Manufacturer.Trim();
            query = query.Where(v => v.Manufacturer == hersteller);
        }

        if (filter.HasDriver is { } hasDriver)
        {
            query = hasDriver
                ? query.Where(v => v.CurrentDriverId != null)
                : query.Where(v => v.CurrentDriverId == null);
        }

        query = ApplySorting(query, filter);

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        var pageSize = filter.PageSize <= 0 ? 100 : filter.PageSize;
        var pageNumber = filter.PageNumber <= 0 ? 1 : filter.PageNumber;

        var rows = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(v => new
            {
                v.Id,
                v.InternalNumber,
                v.LicensePlate,
                v.Manufacturer,
                v.Model,
                v.CurrentMileage,
                v.NextInspectionDue,
                v.IsRetired,
                v.IsRegistered,
                StatusName = v.Status != null ? v.Status.Name : string.Empty,
                StatusKind = v.Status != null ? v.Status.Kind : null,
                StatusColor = v.Status != null ? v.Status.ColorHex : null,
                DriverName = v.CurrentDriver != null
                    ? v.CurrentDriver.FirstName + " " + v.CurrentDriver.LastName
                    : null,
                Categories = string.Join(", ", v.CategoryAssignments
                    .Where(a => a.Category != null)
                    .Select(a => a.Category!.Name)),
                OpenDamages = v.Damages.Count(d => d.Status != DamageStatus.Geschlossen),
                CriticalDamages = v.Damages.Count(d =>
                    d.Status != DamageStatus.Geschlossen && d.Priority == DamagePriority.Kritisch),
                WorkshopStatus = v.WorkshopOrders
                    .Where(w => w.Status != WorkshopOrderStatus.Abgeholt && w.Status != WorkshopOrderStatus.Storniert)
                    .OrderByDescending(w => w.CreatedOn)
                    .Select(w => (WorkshopOrderStatus?)w.Status)
                    .FirstOrDefault(),
                NextServiceDate = v.MaintenanceEntries
                    .Where(m => m.NextDueDate != null)
                    .OrderBy(m => m.NextDueDate)
                    .Select(m => m.NextDueDate)
                    .FirstOrDefault(),
                NextServiceMileage = v.MaintenanceEntries
                    .Where(m => m.NextDueMileage != null)
                    .OrderBy(m => m.NextDueMileage)
                    .Select(m => m.NextDueMileage)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = rows.Select(r => new VehicleListItem
        {
            Id = r.Id,
            InternalNumber = r.InternalNumber,
            LicensePlate = r.LicensePlate,
            Manufacturer = r.Manufacturer,
            Model = r.Model,
            Categories = r.Categories,
            StatusName = r.StatusName,
            StatusKind = r.StatusKind,
            StatusColorHex = r.StatusColor,
            DriverName = r.DriverName,
            CurrentMileage = r.CurrentMileage,
            NextInspectionDue = r.NextInspectionDue,
            InspectionWarning = DueDateCalculator.Evaluate(r.NextInspectionDue, today, thresholds),
            OpenDamageCount = r.OpenDamages,
            HasCriticalDamage = r.CriticalDamages > 0,
            WorkshopStatus = r.WorkshopStatus?.ToString(),
            NextServiceDue = r.NextServiceDate,
            NextServiceMileage = r.NextServiceMileage,
            ServiceWarning = EvaluateService(r.NextServiceDate, r.NextServiceMileage, r.CurrentMileage, today, thresholds),
            IsRetired = r.IsRetired,
            IsRegistered = r.IsRegistered
        }).ToList();

        return new PagedResult<VehicleListItem>(items, total, pageNumber, pageSize);
    }

    public async Task<Vehicle?> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.VehicleView);

        return await _db.Vehicles
            .Include(v => v.Status)
            .Include(v => v.CurrentDriver)
            .Include(v => v.CategoryAssignments)
                .ThenInclude(a => a.Category)
            .FirstOrDefaultAsync(v => v.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<VehicleHeader?> GetHeaderAsync(int id, CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.VehicleView);

        var thresholds = await _settings.GetInspectionThresholdsAsync(cancellationToken).ConfigureAwait(false);

        var row = await _db.Vehicles
            .AsNoTracking()
            .Where(v => v.Id == id)
            .Select(v => new
            {
                v.Id,
                v.InternalNumber,
                v.LicensePlate,
                v.Manufacturer,
                v.Model,
                v.Variant,
                v.CurrentMileage,
                v.NextInspectionDue,
                v.IsRetired,
                StatusName = v.Status != null ? v.Status.Name : string.Empty,
                StatusColor = v.Status != null ? v.Status.ColorHex : null,
                DriverName = v.CurrentDriver != null
                    ? v.CurrentDriver.FirstName + " " + v.CurrentDriver.LastName
                    : null,
                DriverPhone = v.CurrentDriver != null ? v.CurrentDriver.Phone : null,
                OpenDamages = v.Damages.Count(d => d.Status != DamageStatus.Geschlossen)
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            return null;
        }

        return new VehicleHeader
        {
            Id = row.Id,
            InternalNumber = row.InternalNumber,
            LicensePlate = row.LicensePlate,
            Manufacturer = row.Manufacturer,
            Model = row.Model,
            Variant = row.Variant,
            StatusName = row.StatusName,
            StatusColorHex = row.StatusColor,
            DriverName = row.DriverName,
            DriverPhone = row.DriverPhone,
            CurrentMileage = row.CurrentMileage,
            NextInspectionDue = row.NextInspectionDue,
            InspectionWarning = DueDateCalculator.Evaluate(row.NextInspectionDue, _clock.Today, thresholds),
            OpenDamageCount = row.OpenDamages,
            IsRetired = row.IsRetired
        };
    }

    public async Task<int> CreateAsync(
        Vehicle vehicle,
        IEnumerable<int> categoryIds,
        CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.VehicleCreate);

        vehicle.InternalNumber = Guard.NotEmpty(vehicle.InternalNumber, "Interne Fahrzeugnummer");
        vehicle.Manufacturer = Guard.NotEmpty(vehicle.Manufacturer, "Hersteller");
        vehicle.Model = Guard.NotEmpty(vehicle.Model, "Modell");
        vehicle.LicensePlate = LicensePlateFormatter.Normalize(vehicle.LicensePlate);
        if (string.IsNullOrWhiteSpace(vehicle.LicensePlate))
        {
            vehicle.LicensePlate = null;
        }

        var duplicate = await InternalNumberExistsAsync(vehicle.InternalNumber, null, cancellationToken)
            .ConfigureAwait(false);
        Guard.That(!duplicate, $"Die interne Fahrzeugnummer '{vehicle.InternalNumber}' ist bereits vergeben.");

        if (!string.IsNullOrWhiteSpace(vehicle.Vin))
        {
            var vinDuplicate = await _db.Vehicles
                .AnyAsync(v => v.Vin == vehicle.Vin, cancellationToken)
                .ConfigureAwait(false);
            Guard.That(!vinDuplicate, $"Die FIN '{vehicle.Vin}' ist bereits einem anderen Fahrzeug zugeordnet.");
        }

        if (vehicle.VehicleStatusId == 0)
        {
            vehicle.VehicleStatusId = await GetDefaultStatusIdAsync(cancellationToken).ConfigureAwait(false);
        }

        _db.Vehicles.Add(vehicle);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await ReplaceCategoriesAsync(vehicle.Id, categoryIds, cancellationToken).ConfigureAwait(false);

        _db.VehicleStatusHistory.Add(new VehicleStatusHistory
        {
            VehicleId = vehicle.Id,
            OldStatusId = null,
            NewStatusId = vehicle.VehicleStatusId,
            ChangedAt = _clock.Now,
            ChangedByUserId = _currentUser.User?.Id,
            ChangedByUserName = _currentUser.User?.DisplayName,
            Reason = "Fahrzeug angelegt"
        });

        if (vehicle.CurrentMileage > 0)
        {
            _db.MileageEntries.Add(new MileageEntry
            {
                VehicleId = vehicle.Id,
                Mileage = vehicle.CurrentMileage,
                RecordedAt = _clock.Now,
                RecordedByUserId = _currentUser.User?.Id,
                RecordedByUserName = _currentUser.User?.DisplayName,
                Source = MileageSource.ManuelleEingabe,
                Comment = "Erfassung bei Fahrzeuganlage"
            });
            vehicle.CurrentMileageAt = _clock.Now;
        }

        if (vehicle.CurrentDriverId is { } driverId)
        {
            _db.VehicleDriverAssignments.Add(new VehicleDriverAssignment
            {
                VehicleId = vehicle.Id,
                DriverId = driverId,
                ValidFrom = _clock.Now,
                AssignedByUserId = _currentUser.User?.Id,
                AssignedByUserName = _currentUser.User?.DisplayName,
                Comment = "Zuweisung bei Fahrzeuganlage"
            });
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return vehicle.Id;
    }

    public async Task UpdateAsync(
        Vehicle vehicle,
        IEnumerable<int> categoryIds,
        CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.VehicleEdit);

        var existing = await _db.Vehicles
            .FirstOrDefaultAsync(v => v.Id == vehicle.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EntityNotFoundException(nameof(Vehicle), vehicle.Id);

        vehicle.InternalNumber = Guard.NotEmpty(vehicle.InternalNumber, "Interne Fahrzeugnummer");
        var duplicate = await InternalNumberExistsAsync(vehicle.InternalNumber, vehicle.Id, cancellationToken)
            .ConfigureAwait(false);
        Guard.That(!duplicate, $"Die interne Fahrzeugnummer '{vehicle.InternalNumber}' ist bereits vergeben.");

        var previousStatusId = existing.VehicleStatusId;

        existing.InternalNumber = vehicle.InternalNumber;
        existing.LicensePlate = string.IsNullOrWhiteSpace(vehicle.LicensePlate)
            ? null
            : LicensePlateFormatter.Normalize(vehicle.LicensePlate);
        existing.Vin = vehicle.Vin;
        existing.Manufacturer = Guard.NotEmpty(vehicle.Manufacturer, "Hersteller");
        existing.Model = Guard.NotEmpty(vehicle.Model, "Modell");
        existing.Variant = vehicle.Variant;
        existing.BuildYear = vehicle.BuildYear;
        existing.FirstRegistration = vehicle.FirstRegistration;
        existing.FuelType = vehicle.FuelType;
        existing.Transmission = vehicle.Transmission;
        existing.PowerKw = vehicle.PowerKw;
        existing.Color = vehicle.Color;
        existing.Seats = vehicle.Seats;
        existing.Hsn = vehicle.Hsn;
        existing.Tsn = vehicle.Tsn;
        existing.PurchaseDate = vehicle.PurchaseDate;
        existing.PurchasePrice = vehicle.PurchasePrice;
        existing.IsLeased = vehicle.IsLeased;
        existing.LeasingCompany = vehicle.LeasingCompany;
        existing.LeasingContractNumber = vehicle.LeasingContractNumber;
        existing.LeasingStart = vehicle.LeasingStart;
        existing.LeasingEnd = vehicle.LeasingEnd;
        existing.Comment = vehicle.Comment;
        existing.VehicleStatusId = vehicle.VehicleStatusId;
        existing.RowVersion = vehicle.RowVersion;

        if (previousStatusId != vehicle.VehicleStatusId)
        {
            _db.VehicleStatusHistory.Add(new VehicleStatusHistory
            {
                VehicleId = existing.Id,
                OldStatusId = previousStatusId,
                NewStatusId = vehicle.VehicleStatusId,
                ChangedAt = _clock.Now,
                ChangedByUserId = _currentUser.User?.Id,
                ChangedByUserName = _currentUser.User?.DisplayName,
                Reason = "Aenderung der Fahrzeugstammdaten"
            });
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await ReplaceCategoriesAsync(existing.Id, categoryIds, cancellationToken).ConfigureAwait(false);
    }

    public async Task ChangeStatusAsync(
        int vehicleId,
        int newStatusId,
        string? reason,
        string? comment,
        CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.VehicleEdit);

        var vehicle = await _db.Vehicles.FirstOrDefaultAsync(v => v.Id == vehicleId, cancellationToken).ConfigureAwait(false)
            ?? throw new EntityNotFoundException(nameof(Vehicle), vehicleId);

        if (vehicle.VehicleStatusId == newStatusId)
        {
            return;
        }

        var statusExists = await _db.VehicleStatuses.AnyAsync(s => s.Id == newStatusId, cancellationToken)
            .ConfigureAwait(false);
        Guard.That(statusExists, "Der gewaehlte Fahrzeugstatus existiert nicht.");

        var oldStatusId = vehicle.VehicleStatusId;
        vehicle.VehicleStatusId = newStatusId;

        _db.VehicleStatusHistory.Add(new VehicleStatusHistory
        {
            VehicleId = vehicleId,
            OldStatusId = oldStatusId,
            NewStatusId = newStatusId,
            ChangedAt = _clock.Now,
            ChangedByUserId = _currentUser.User?.Id,
            ChangedByUserName = _currentUser.User?.DisplayName,
            Reason = reason,
            Comment = comment
        });

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<VehicleStatusHistory>> GetStatusHistoryAsync(
        int vehicleId,
        CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.VehicleView);

        return await _db.VehicleStatusHistory
            .AsNoTracking()
            .Include(h => h.OldStatus)
            .Include(h => h.NewStatus)
            .Where(h => h.VehicleId == vehicleId)
            .OrderByDescending(h => h.ChangedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<string>> GetManufacturersAsync(CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.VehicleView);

        // Nur was wirklich im Bestand vorkommt - eine feste Liste waere
        // sofort veraltet.
        return await _db.Vehicles
            .AsNoTracking()
            .Where(v => v.Manufacturer != "")
            .Select(v => v.Manufacturer)
            .Distinct()
            .OrderBy(m => m)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<VehicleStatus>> GetStatusesAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var query = _db.VehicleStatuses.AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(s => s.IsActive);
        }

        return await query
            .OrderBy(s => s.SortOrder)
            .ThenBy(s => s.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<VehicleStatus> CreateStatusAsync(string name, CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.SettingsManage);

        name = Guard.NotEmpty(name, "Name des Status");

        await DemandUniqueStatusNameAsync(name, null, cancellationToken).ConfigureAwait(false);

        var letzte = await _db.VehicleStatuses
            .OrderByDescending(s => s.SortOrder)
            .Select(s => (int?)s.SortOrder)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var status = new VehicleStatus
        {
            Name = name,
            SortOrder = (letzte ?? 0) + 10,
            IsActive = true,
            IsSystemStatus = false,
            // Eine Systemzuordnung bekommt nur, was das Programm selbst
            // mitbringt. Sonst wuerden zwei Status denselben Sinn tragen.
            Kind = null,
            CountsAsOperational = false,
            CountsAsAvailable = false
        };

        _db.VehicleStatuses.Add(status);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return status;
    }

    public async Task UpdateStatusAsync(VehicleStatus status, CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.SettingsManage);

        ArgumentNullException.ThrowIfNull(status);

        var gespeichert = await _db.VehicleStatuses
            .FirstOrDefaultAsync(s => s.Id == status.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EntityNotFoundException("Fahrzeugstatus", status.Id);

        var name = Guard.NotEmpty(status.Name, "Name des Status");

        await DemandUniqueStatusNameAsync(name, status.Id, cancellationToken).ConfigureAwait(false);

        // Die Vergleichswerte kommen frisch aus der Datenbank, nicht vom
        // geladenen Objekt: Aufrufer koennen genau dieses Objekt bearbeitet
        // haben - dann waeren "vorher" und "nachher" dasselbe und die
        // folgenden Pruefungen liefen stillschweigend ins Leere.
        var vorher = await _db.VehicleStatuses
            .AsNoTracking()
            .Where(s => s.Id == status.Id)
            .Select(s => new { s.IsActive, s.Kind })
            .FirstAsync(cancellationToken)
            .ConfigureAwait(false);

        var warAktiv = vorher.IsActive;
        var bisherigeBedeutung = vorher.Kind;

        if (warAktiv && !status.IsActive)
        {
            // Ohne einen aktiven Status liesse sich kein Fahrzeug mehr anlegen.
            var weitereAktive = await _db.VehicleStatuses
                .CountAsync(s => s.IsActive && s.Id != status.Id, cancellationToken)
                .ConfigureAwait(false);

            Guard.That(weitereAktive > 0,
                $"„{gespeichert.Name}“ ist der letzte aktive Status. " +
                "Ohne einen aktiven Status könnte kein Fahrzeug mehr angelegt werden.");
        }

        // Die fachliche Bedeutung darf umgehaengt werden - daran haengen Ablaeufe
        // wie Ausmusterung, Werkstatt und Import. Zwei Regeln halten das zusammen:
        // jede Bedeutung traegt genau ein Status, und entfernen laesst sie sich
        // nicht. Beim Umhaengen gibt der bisherige Traeger sie im selben Schritt
        // ab - so ist sie nie doppelt und nie verschwunden.
        if (status.Kind != bisherigeBedeutung)
        {
            if (status.Kind is { } neueBedeutung)
            {
                var bisher = await _db.VehicleStatuses
                    .FirstOrDefaultAsync(s => s.Id != status.Id && s.Kind == neueBedeutung, cancellationToken)
                    .ConfigureAwait(false);

                if (bisher is not null)
                {
                    bisher.Kind = null;
                }
            }
            else
            {
                Guard.That(false,
                    $"Die Bedeutung von „{gespeichert.Name}“ kann nicht entfernt werden. " +
                    "Abläufe wie Ausmusterung oder Werkstatt suchen ihren Status darüber und " +
                    "würden sonst stillschweigend nichts mehr setzen. " +
                    "Sie können die Bedeutung aber einem anderen Status geben.");
            }

            gespeichert.Kind = status.Kind;
        }

        gespeichert.Name = name;
        gespeichert.Description = status.Description;
        gespeichert.ColorHex = string.IsNullOrWhiteSpace(status.ColorHex) ? null : status.ColorHex.Trim();
        gespeichert.SortOrder = status.SortOrder;
        gespeichert.IsActive = status.IsActive;
        gespeichert.CountsAsOperational = status.CountsAsOperational;
        gespeichert.CountsAsAvailable = status.CountsAsAvailable;

        // IsSystemStatus bleibt unberuehrt.
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteStatusAsync(int statusId, CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.SettingsManage);

        var status = await _db.VehicleStatuses
            .FirstOrDefaultAsync(s => s.Id == statusId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EntityNotFoundException("Fahrzeugstatus", statusId);

        Guard.That(!status.IsSystemStatus && status.Kind is null,
            $"Der mitgelieferte Status „{status.Name}“ kann nicht gelöscht werden. " +
            "An ihm hängen Abläufe wie Ausmusterung, Werkstatt und Import. " +
            "Sie können ihn aber stilllegen, dann erscheint er nicht mehr zur Auswahl.");

        var fahrzeuge = await _db.Vehicles
            .CountAsync(v => v.VehicleStatusId == statusId, cancellationToken)
            .ConfigureAwait(false);

        Guard.That(fahrzeuge == 0,
            $"Der Status „{status.Name}“ ist noch bei {fahrzeuge} Fahrzeug(en) gesetzt. " +
            "Bitte diese Fahrzeuge zuerst umstellen oder den Status stilllegen.");

        var historie = await _db.VehicleStatusHistory
            .CountAsync(h => h.NewStatusId == statusId || h.OldStatusId == statusId, cancellationToken)
            .ConfigureAwait(false);

        Guard.That(historie == 0,
            $"Der Status „{status.Name}“ kommt noch in der Statushistorie von Fahrzeugen vor. " +
            "Historien werden nicht verändert; bitte den Status stilllegen.");

        _db.VehicleStatuses.Remove(status);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task DemandUniqueStatusNameAsync(
        string name,
        int? exceptId,
        CancellationToken cancellationToken)
    {
        var vorhanden = await _db.VehicleStatuses
            .AnyAsync(s => s.Id != exceptId && s.Name.ToLower() == name.ToLower(), cancellationToken)
            .ConfigureAwait(false);

        Guard.That(!vorhanden, $"Der Status „{name}“ ist bereits vorhanden.");
    }

    public async Task<IReadOnlyList<VehicleCategory>> GetCategoriesAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var query = _db.VehicleCategories.AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(c => c.IsActive);
        }

        return await query
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<VehicleCategory> CreateCategoryAsync(
        string name,
        string? colorHex = null,
        CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.SettingsManage);

        name = Guard.NotEmpty(name, "Name des Einsatzbereichs");

        await DemandUniqueCategoryNameAsync(name, null, cancellationToken).ConfigureAwait(false);

        // Hinten anstellen, damit die mitgelieferten Bereiche ihre Reihenfolge behalten.
        var letzte = await _db.VehicleCategories
            .OrderByDescending(c => c.SortOrder)
            .Select(c => (int?)c.SortOrder)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var category = new VehicleCategory
        {
            Name = name,
            ColorHex = string.IsNullOrWhiteSpace(colorHex) ? null : colorHex.Trim(),
            SortOrder = (letzte ?? 0) + 10,
            IsActive = true,
            IsSystemCategory = false
        };

        _db.VehicleCategories.Add(category);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return category;
    }

    public async Task UpdateCategoryAsync(VehicleCategory category, CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.SettingsManage);

        ArgumentNullException.ThrowIfNull(category);

        var gespeichert = await _db.VehicleCategories
            .FirstOrDefaultAsync(c => c.Id == category.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EntityNotFoundException("Einsatzbereich", category.Id);

        var name = Guard.NotEmpty(category.Name, "Name des Einsatzbereichs");

        await DemandUniqueCategoryNameAsync(name, category.Id, cancellationToken).ConfigureAwait(false);

        gespeichert.Name = name;
        gespeichert.ColorHex = string.IsNullOrWhiteSpace(category.ColorHex) ? null : category.ColorHex.Trim();
        gespeichert.Description = category.Description;
        gespeichert.SortOrder = category.SortOrder;
        gespeichert.IsActive = category.IsActive;

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteCategoryAsync(int categoryId, CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.SettingsManage);

        var category = await _db.VehicleCategories
            .FirstOrDefaultAsync(c => c.Id == categoryId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EntityNotFoundException("Einsatzbereich", categoryId);

        Guard.That(!category.IsSystemCategory,
            $"Der mitgelieferte Einsatzbereich '{category.Name}' kann nicht gelöscht werden. " +
            "Sie können ihn aber stilllegen, dann erscheint er nicht mehr zur Auswahl.");

        var fahrzeuge = await _db.VehicleCategoryAssignments
            .CountAsync(a => a.VehicleCategoryId == categoryId, cancellationToken)
            .ConfigureAwait(false);

        Guard.That(fahrzeuge == 0,
            $"Der Einsatzbereich '{category.Name}' ist noch {fahrzeuge} Fahrzeug(en) zugeordnet. " +
            "Bitte die Zuordnung zuerst entfernen oder den Bereich stilllegen.");

        var regeln = await _db.MaintenanceRules
            .CountAsync(r => r.VehicleCategoryId == categoryId, cancellationToken)
            .ConfigureAwait(false);

        Guard.That(regeln == 0,
            $"Der Einsatzbereich '{category.Name}' wird noch von {regeln} Wartungsregel(n) verwendet. " +
            "Bitte die Regeln zuerst anpassen oder den Bereich stilllegen.");

        _db.VehicleCategories.Remove(category);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Zwei Einsatzbereiche mit gleichem Namen waeren in jeder Auswahlliste ein Ratespiel.</summary>
    private async Task DemandUniqueCategoryNameAsync(
        string name,
        int? exceptId,
        CancellationToken cancellationToken)
    {
        var vorhanden = await _db.VehicleCategories
            .AnyAsync(c => c.Id != exceptId && c.Name.ToLower() == name.ToLower(), cancellationToken)
            .ConfigureAwait(false);

        Guard.That(!vorhanden, $"Der Einsatzbereich '{name}' ist bereits vorhanden.");
    }

    public async Task<IReadOnlyList<int>> GetCategoryIdsAsync(int vehicleId, CancellationToken cancellationToken = default)
    {
        return await _db.VehicleCategoryAssignments
            .AsNoTracking()
            .Where(a => a.VehicleId == vehicleId)
            .Select(a => a.VehicleCategoryId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> InternalNumberExistsAsync(
        string internalNumber,
        int? exceptVehicleId = null,
        CancellationToken cancellationToken = default)
    {
        return await _db.Vehicles
            .AnyAsync(
                v => v.InternalNumber == internalNumber && (exceptVehicleId == null || v.Id != exceptVehicleId),
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<VehicleTimelineEntry>> GetTimelineAsync(
        int vehicleId,
        CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.VehicleView);

        var entries = new List<VehicleTimelineEntry>();

        var vehicle = await _db.Vehicles
            .AsNoTracking()
            .Where(v => v.Id == vehicleId)
            .Select(v => new { v.CreatedAt, v.CreatedByUserName, v.InternalNumber })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (vehicle is not null)
        {
            entries.Add(new VehicleTimelineEntry(
                vehicle.CreatedAt, "Fahrzeug", "Fahrzeug angelegt", null, vehicle.InternalNumber,
                vehicle.CreatedByUserName, null));
        }

        var statusChanges = await _db.VehicleStatusHistory
            .AsNoTracking()
            .Include(h => h.OldStatus)
            .Include(h => h.NewStatus)
            .Where(h => h.VehicleId == vehicleId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        entries.AddRange(statusChanges.Select(h => new VehicleTimelineEntry(
            h.ChangedAt, "Status", "Status geaendert",
            h.OldStatus?.Name, h.NewStatus?.Name, h.ChangedByUserName, h.Reason)));

        var driverChanges = await _db.VehicleDriverAssignments
            .AsNoTracking()
            .Include(a => a.Driver)
            .Where(a => a.VehicleId == vehicleId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var assignment in driverChanges)
        {
            entries.Add(new VehicleTimelineEntry(
                assignment.ValidFrom, "Fahrer", "Fester Fahrer zugewiesen",
                null, assignment.Driver?.DisplayName, assignment.AssignedByUserName, assignment.Comment));

            if (assignment.ValidTo is { } validTo)
            {
                entries.Add(new VehicleTimelineEntry(
                    validTo, "Fahrer", "Fahrerzuweisung beendet",
                    assignment.Driver?.DisplayName, null, assignment.AssignedByUserName, null));
            }
        }

        var plateChanges = await _db.LicensePlateAssignments
            .AsNoTracking()
            .Include(a => a.LicensePlate)
            .Where(a => a.VehicleId == vehicleId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var assignment in plateChanges)
        {
            entries.Add(new VehicleTimelineEntry(
                assignment.ValidFrom, "Kennzeichen", "Kennzeichen zugewiesen",
                null, assignment.LicensePlate?.Plate, assignment.AssignedByUserName, assignment.Reason));

            if (assignment.ValidTo is { } validTo)
            {
                entries.Add(new VehicleTimelineEntry(
                    validTo, "Kennzeichen", "Kennzeichen abgegeben",
                    assignment.LicensePlate?.Plate, null, assignment.AssignedByUserName, assignment.Reason));
            }
        }

        var mileages = await _db.MileageEntries
            .AsNoTracking()
            .Where(m => m.VehicleId == vehicleId)
            .OrderByDescending(m => m.RecordedAt)
            .Take(200)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        entries.AddRange(mileages.Select(m => new VehicleTimelineEntry(
            m.RecordedAt, "Kilometer", "Kilometerstand erfasst",
            null, $"{m.Mileage:N0} km", m.RecordedByUserName, m.Source.ToString())));

        var inspections = await _db.VehicleInspections
            .AsNoTracking()
            .Where(i => i.VehicleId == vehicleId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        entries.AddRange(inspections.Select(i => new VehicleTimelineEntry(
            i.InspectionDate, "TÜV", $"{i.Type} eingetragen",
            null, $"naechste Faelligkeit {i.NextDueDate:dd.MM.yyyy}", i.CreatedByUserName, i.TestCenter)));

        var maintenance = await _db.MaintenanceEntries
            .AsNoTracking()
            .Include(m => m.Rule)
            .Where(m => m.VehicleId == vehicleId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        entries.AddRange(maintenance.Select(m => new VehicleTimelineEntry(
            m.PerformedAt, "Wartung", m.Rule?.Name ?? m.Title ?? "Wartung durchgefuehrt",
            null, m.Mileage is null ? null : $"{m.Mileage:N0} km", m.CreatedByUserName, m.Comment)));

        var damages = await _db.DamageReports
            .AsNoTracking()
            .Where(d => d.VehicleId == vehicleId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        entries.AddRange(damages.Select(d => new VehicleTimelineEntry(
            d.OccurredAt, "Schaden", "Schaden gemeldet",
            null, d.Description, d.CreatedByUserName, d.DamageNumber)));

        entries.AddRange(damages.Where(d => d.ClosedAt is not null).Select(d => new VehicleTimelineEntry(
            d.ClosedAt!.Value, "Schaden", "Schaden geschlossen",
            d.Description, null, d.ModifiedByUserName, d.DamageNumber)));

        var accidents = await _db.AccidentReports
            .AsNoTracking()
            .Where(a => a.VehicleId == vehicleId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        entries.AddRange(accidents.Select(a => new VehicleTimelineEntry(
            a.OccurredAt, "Unfall", "Unfall gemeldet",
            null, a.Location, a.CreatedByUserName, a.AccidentNumber)));

        var orders = await _db.WorkshopOrders
            .AsNoTracking()
            .Include(w => w.Workshop)
            .Where(w => w.VehicleId == vehicleId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var order in orders)
        {
            entries.Add(new VehicleTimelineEntry(
                order.CreatedOn, "Werkstatt", "Werkstattauftrag erstellt",
                null, order.Workshop?.Name, order.CreatedByUserName, order.OrderNumber));

            if (order.CompletedAt is { } completed)
            {
                entries.Add(new VehicleTimelineEntry(
                    completed, "Werkstatt", "Werkstattvorgang abgeschlossen",
                    null, order.Workshop?.Name, order.ModifiedByUserName, order.OrderNumber));
            }
        }

        var registrations = await _db.VehicleRegistrations
            .AsNoTracking()
            .Where(r => r.VehicleId == vehicleId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var registration in registrations)
        {
            entries.Add(new VehicleTimelineEntry(
                registration.RegisteredAt, "Zulassung", registration.EventType.ToString(),
                null, registration.LicensePlate, registration.CreatedByUserName, registration.Reason));

            if (registration.DeregisteredAt is { } deregistered)
            {
                entries.Add(new VehicleTimelineEntry(
                    deregistered, "Zulassung", "Abmeldung",
                    registration.LicensePlate, null, registration.ModifiedByUserName, registration.Reason));
            }
        }

        var retirement = await _db.VehicleRetirements
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.VehicleId == vehicleId, cancellationToken)
            .ConfigureAwait(false);

        if (retirement is not null)
        {
            entries.Add(new VehicleTimelineEntry(
                retirement.RetiredAt, "Ausmusterung", "Fahrzeug ausgemustert",
                null, retirement.Reason.ToString(), retirement.CreatedByUserName, retirement.Comment));
        }

        var documents = await _db.VehicleDocuments
            .AsNoTracking()
            .Where(d => d.VehicleId == vehicleId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        entries.AddRange(documents.Select(d => new VehicleTimelineEntry(
            d.CreatedAt, "Dokument", "Dokument hinzugefuegt",
            null, d.Title, d.CreatedByUserName, d.Category.ToString())));

        return entries.OrderByDescending(e => e.Timestamp).ToList();
    }

    private static IQueryable<Vehicle> ApplySorting(IQueryable<Vehicle> query, VehicleFilter filter)
    {
        var descending = filter.SortDescending;

        return filter.SortColumn switch
        {
            nameof(VehicleListItem.LicensePlate) => descending
                ? query.OrderByDescending(v => v.LicensePlate)
                : query.OrderBy(v => v.LicensePlate),
            nameof(VehicleListItem.Manufacturer) => descending
                ? query.OrderByDescending(v => v.Manufacturer).ThenByDescending(v => v.Model)
                : query.OrderBy(v => v.Manufacturer).ThenBy(v => v.Model),
            nameof(VehicleListItem.Model) => descending
                ? query.OrderByDescending(v => v.Model)
                : query.OrderBy(v => v.Model),
            nameof(VehicleListItem.CurrentMileage) => descending
                ? query.OrderByDescending(v => v.CurrentMileage)
                : query.OrderBy(v => v.CurrentMileage),
            nameof(VehicleListItem.NextInspectionDue) => descending
                ? query.OrderByDescending(v => v.NextInspectionDue)
                : query.OrderBy(v => v.NextInspectionDue),
            nameof(VehicleListItem.StatusName) => descending
                ? query.OrderByDescending(v => v.Status!.SortOrder)
                : query.OrderBy(v => v.Status!.SortOrder),
            _ => descending
                ? query.OrderByDescending(v => v.InternalNumber)
                : query.OrderBy(v => v.InternalNumber)
        };
    }

    private static WarningLevel EvaluateService(
        DateTime? nextServiceDate,
        int? nextServiceMileage,
        int currentMileage,
        DateTime today,
        DueDateThresholds thresholds)
    {
        var byDate = DueDateCalculator.Evaluate(nextServiceDate, today, thresholds);

        var byMileage = WarningLevel.Inaktiv;
        if (nextServiceMileage is { } dueMileage)
        {
            var remaining = dueMileage - currentMileage;
            byMileage = remaining switch
            {
                <= 0 => WarningLevel.Kritisch,
                <= 1000 => WarningLevel.BaldFaellig,
                <= 3000 => WarningLevel.Hinweis,
                _ => WarningLevel.Ok
            };
        }

        if (byDate == WarningLevel.Inaktiv)
        {
            return byMileage;
        }

        if (byMileage == WarningLevel.Inaktiv)
        {
            return byDate;
        }

        // Die dringendere der beiden Faelligkeiten bestimmt die Warnstufe.
        return (WarningLevel)Math.Max((int)byDate, (int)byMileage);
    }

    private async Task<int> GetDefaultStatusIdAsync(CancellationToken cancellationToken)
    {
        var status = await _db.VehicleStatuses
            .Where(s => s.Kind == VehicleStatusKind.Aktiv)
            .Select(s => s.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (status != 0)
        {
            return status;
        }

        status = await _db.VehicleStatuses
            .OrderBy(s => s.SortOrder)
            .Select(s => s.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        Guard.That(status != 0, "Es sind keine Fahrzeugstatus hinterlegt. Bitte zuerst die Stammdaten einrichten.");
        return status;
    }

    private async Task ReplaceCategoriesAsync(
        int vehicleId,
        IEnumerable<int> categoryIds,
        CancellationToken cancellationToken)
    {
        var target = categoryIds.Distinct().ToList();

        var existing = await _db.VehicleCategoryAssignments
            .Where(a => a.VehicleId == vehicleId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        _db.VehicleCategoryAssignments.RemoveRange(existing.Where(a => !target.Contains(a.VehicleCategoryId)));

        var first = true;
        foreach (var categoryId in target)
        {
            var assignment = existing.FirstOrDefault(a => a.VehicleCategoryId == categoryId);
            if (assignment is null)
            {
                _db.VehicleCategoryAssignments.Add(new VehicleCategoryAssignment
                {
                    VehicleId = vehicleId,
                    VehicleCategoryId = categoryId,
                    IsPrimary = first,
                    AssignedAt = _clock.Now,
                    AssignedByUserId = _currentUser.User?.Id
                });
            }
            else
            {
                assignment.IsPrimary = first;
            }

            first = false;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
