using Vehistra.Application.Abstractions;
using Vehistra.Domain.Entities;
using Vehistra.Domain.Enums;
using Vehistra.Domain.Exceptions;
using Vehistra.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace Vehistra.Application.Services;

/// <inheritdoc />
public sealed class ReportBuilder : IReportBuilder
{
    private readonly IVehistraDbContext _db;
    private readonly IReportService _reports;
    private readonly ISettingsService _settings;
    private readonly IDocumentStorage _storage;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditWriter _audit;
    private readonly IClock _clock;

    public ReportBuilder(
        IVehistraDbContext db,
        IReportService reports,
        ISettingsService settings,
        IDocumentStorage storage,
        ICurrentUserService currentUser,
        IAuditWriter audit,
        IClock clock)
    {
        _db = db;
        _reports = reports;
        _settings = settings;
        _storage = storage;
        _currentUser = currentUser;
        _audit = audit;
        _clock = clock;
    }

    public async Task<GeneratedReport> BuildWorkshopReportAsync(
        int? vehicleId = null,
        int? workshopOrderId = null,
        CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.ReportsPrint);

        var header = await BuildHeaderAsync(cancellationToken).ConfigureAwait(false);
        var notice = await GetTemplateNoticeAsync("WORKSHOP_REPORT", SettingsKeys.WorkshopReportNotice, cancellationToken)
            .ConfigureAwait(false);

        var data = new WorkshopReportData
        {
            Header = header,
            IsBlankForm = vehicleId is null && workshopOrderId is null,
            Notice = notice,
            Date = _clock.Today
        };

        WorkshopOrder? order = null;

        if (workshopOrderId is { } orderId)
        {
            order = await _db.WorkshopOrders
                .AsNoTracking()
                .Include(o => o.Vehicle)
                .Include(o => o.Driver)
                .Include(o => o.Workshop)
                .Include(o => o.Tasks.OrderBy(t => t.Position))
                .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new EntityNotFoundException(nameof(WorkshopOrder), orderId);

            vehicleId = order.VehicleId;
        }

        if (vehicleId is { } id)
        {
            var vehicle = await _db.Vehicles
                .AsNoTracking()
                .Include(v => v.CurrentDriver)
                .FirstOrDefaultAsync(v => v.Id == id, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new EntityNotFoundException(nameof(Vehicle), id);

            data.LicensePlate = vehicle.LicensePlate;
            data.InternalNumber = vehicle.InternalNumber;
            data.VehicleDescription = $"{vehicle.Manufacturer} {vehicle.Model} {vehicle.Variant}".Trim();
            data.Mileage = vehicle.CurrentMileage;
            data.DriverName = order?.Driver?.FullName ?? vehicle.CurrentDriver?.FullName;
            data.DriverPhone = order?.Driver?.Phone ?? vehicle.CurrentDriver?.Phone;
            data.WorkshopName = order?.Workshop?.Name;
            data.ProposedAppointment = order?.ProposedDate ?? order?.AppointmentDate;
            data.OrderNumber = order?.OrderNumber;

            var lines = new List<WorkshopReportTaskLine>();
            var position = 0;

            if (order is not null)
            {
                foreach (var task in order.Tasks.OrderBy(t => t.Position))
                {
                    lines.Add(new WorkshopReportTaskLine(++position, task.Description, task.IsCompleted));
                }

                data.CheckedStandardTasks = order.Tasks
                    .Where(t => !string.IsNullOrWhiteSpace(t.StandardTaskKey))
                    .Select(t => t.StandardTaskKey!)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                if (!string.IsNullOrWhiteSpace(order.WorkToPerform))
                {
                    foreach (var line in order.WorkToPerform.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                    {
                        lines.Add(new WorkshopReportTaskLine(++position, line.Trim(), false));
                    }
                }
            }
            else
            {
                // Offene Schaeden des Fahrzeugs als auszufuehrende Arbeiten vorbelegen.
                var damages = await _db.DamageReports
                    .AsNoTracking()
                    .Where(d => d.VehicleId == id && d.Status != DamageStatus.Geschlossen)
                    .OrderByDescending(d => d.Priority)
                    .ThenBy(d => d.OccurredAt)
                    .Take(12)
                    .Select(d => new { d.DamageNumber, d.Description })
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                foreach (var damage in damages)
                {
                    lines.Add(new WorkshopReportTaskLine(++position, $"{damage.DamageNumber}: {damage.Description}", false));
                }

                // Faellige Hauptuntersuchung ankreuzen.
                if (vehicle.NextInspectionDue is { } due && (due.Date - _clock.Today).Days <= 30)
                {
                    data.CheckedStandardTasks.Add(WorkshopStandardTasks.Hu);
                }
            }

            data.TaskLines = lines;
        }

        var content = await _reports.CreateWorkshopReportAsync(data, cancellationToken).ConfigureAwait(false);

        var fileName = data.IsBlankForm
            ? $"Werkstattbericht-blanko_{_clock.Now:yyyyMMdd}.pdf"
            : $"Werkstattbericht_{Sanitize(data.LicensePlate ?? data.InternalNumber)}_{_clock.Now:yyyyMMdd_HHmm}.pdf";

        await _audit.WriteAsync(AuditAction.Printed, "WorkshopReport", workshopOrderId?.ToString(),
            data.LicensePlate ?? data.InternalNumber, null, cancellationToken).ConfigureAwait(false);

        return new GeneratedReport(content, "WORKSHOP_REPORT", "Werkstattbericht", fileName,
            vehicleId, workshopOrderId, null, data.IsBlankForm);
    }

    public async Task<GeneratedReport> BuildAccidentReportAsync(
        int? vehicleId = null,
        int? accidentId = null,
        CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.ReportsPrint);

        var header = await BuildHeaderAsync(cancellationToken).ConfigureAwait(false);
        var notice = await GetTemplateNoticeAsync("ACCIDENT_REPORT", SettingsKeys.AccidentReportNotice, cancellationToken)
            .ConfigureAwait(false);

        var data = new AccidentReportData
        {
            Header = header,
            IsBlankForm = vehicleId is null && accidentId is null,
            Notice = notice
        };

        AccidentReport? accident = null;

        if (accidentId is { } id)
        {
            accident = await _db.AccidentReports
                .AsNoTracking()
                .Include(a => a.Vehicle)
                .Include(a => a.Driver)
                .Include(a => a.Participants)
                .Include(a => a.Witnesses)
                .FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new EntityNotFoundException(nameof(AccidentReport), id);

            vehicleId = accident.VehicleId;

            data.AccidentNumber = accident.AccidentNumber;
            data.OccurredAt = accident.OccurredAt;
            data.Location = accident.Location;
            data.Mileage = accident.Mileage;
            data.DamageKindKey = accident.Type.ToString();
            data.CourseOfEvents = accident.CourseOfEvents;
            data.PoliceInvolved = accident.PoliceInvolved;
            data.PersonalInjury = accident.PersonalInjury;
            data.VehicleDriveable = accident.VehicleDriveable;
            data.PoliceStation = accident.PoliceStation;
            data.PoliceFileNumber = accident.PoliceFileNumber;
            data.DriverName = accident.Driver?.FullName;
            data.DriverPhone = accident.DriverPhone ?? accident.Driver?.Phone;

            var participant = accident.Participants.FirstOrDefault();
            if (participant is not null)
            {
                data.Participant = new AccidentReportParticipant
                {
                    LicensePlate = participant.LicensePlate,
                    Name = $"{participant.LastName}, {participant.FirstName}".Trim(' ', ','),
                    Phone = participant.Phone,
                    Address = string.Join(", ", new[]
                    {
                        participant.Street,
                        string.Join(" ", new[] { participant.PostalCode, participant.City }
                            .Where(s => !string.IsNullOrWhiteSpace(s)))
                    }.Where(s => !string.IsNullOrWhiteSpace(s))),
                    InsuranceCompany = participant.InsuranceCompany,
                    InsuranceNumber = participant.InsuranceNumber
                };
            }

            var witness = accident.Witnesses.FirstOrDefault();
            if (witness is not null)
            {
                data.WitnessName = witness.FullName;
                data.WitnessPhone = witness.Phone;
            }
        }

        if (vehicleId is { } currentVehicleId)
        {
            var vehicle = await _db.Vehicles
                .AsNoTracking()
                .Include(v => v.CurrentDriver)
                .FirstOrDefaultAsync(v => v.Id == currentVehicleId, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new EntityNotFoundException(nameof(Vehicle), currentVehicleId);

            data.LicensePlate = vehicle.LicensePlate;
            data.InternalNumber = vehicle.InternalNumber;
            data.VehicleDescription = $"{vehicle.Manufacturer} {vehicle.Model} {vehicle.Variant}".Trim();
            data.Mileage ??= vehicle.CurrentMileage;
            data.DriverName ??= vehicle.CurrentDriver?.FullName;
            data.DriverPhone ??= vehicle.CurrentDriver?.Phone;
        }

        var content = await _reports.CreateAccidentReportAsync(data, cancellationToken).ConfigureAwait(false);

        var fileName = data.IsBlankForm
            ? $"Unfallbericht-blanko_{_clock.Now:yyyyMMdd}.pdf"
            : $"Unfallbericht_{Sanitize(data.LicensePlate ?? data.InternalNumber)}_{_clock.Now:yyyyMMdd_HHmm}.pdf";

        await _audit.WriteAsync(AuditAction.Printed, "AccidentReport", accidentId?.ToString(),
            data.AccidentNumber ?? data.LicensePlate, null, cancellationToken).ConfigureAwait(false);

        return new GeneratedReport(content, "ACCIDENT_REPORT", "Unfall- / Schadensbericht", fileName,
            vehicleId, null, accidentId, data.IsBlankForm);
    }

    public async Task<GeneratedReport> BuildVehicleFileAsync(
        int vehicleId,
        CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.ReportsPrint);

        var vehicle = await _db.Vehicles
            .AsNoTracking()
            .Include(v => v.Status)
            .Include(v => v.CurrentDriver)
            .Include(v => v.CategoryAssignments).ThenInclude(a => a.Category)
            .FirstOrDefaultAsync(v => v.Id == vehicleId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EntityNotFoundException(nameof(Vehicle), vehicleId);

        var header = await BuildHeaderAsync(cancellationToken).ConfigureAwait(false);

        var data = new VehicleFileReportData
        {
            Header = header,
            VehicleDisplay = vehicle.DisplayName,
            MasterData =
            [
                ("Interne Nummer", vehicle.InternalNumber),
                ("Kennzeichen", vehicle.LicensePlate),
                ("FIN / VIN", vehicle.Vin),
                ("Hersteller", vehicle.Manufacturer),
                ("Modell", vehicle.Model),
                ("Variante", vehicle.Variant),
                ("Baujahr", vehicle.BuildYear?.ToString()),
                ("Erstzulassung", vehicle.FirstRegistration?.ToString("dd.MM.yyyy")),
                ("Kraftstoff", vehicle.FuelType.ToString()),
                ("Getriebe", vehicle.Transmission.ToString()),
                ("Leistung", vehicle.PowerKw is null ? null : $"{vehicle.PowerKw} kW"),
                ("Farbe", vehicle.Color),
                ("Sitzplätze", vehicle.Seats?.ToString()),
                ("Kilometerstand", $"{vehicle.CurrentMileage:N0} km"),
                ("HSN / TSN", $"{vehicle.Hsn} / {vehicle.Tsn}".Trim(' ', '/')),
                ("Status", vehicle.Status?.Name),
                ("Fester Fahrer", vehicle.CurrentDriver?.DisplayName),
                ("Einsatzbereiche", string.Join(", ", vehicle.CategoryAssignments
                    .Where(a => a.Category is not null)
                    .Select(a => a.Category!.Name))),
                ("Nächste HU", vehicle.NextInspectionDue?.ToString("dd.MM.yyyy")),
                ("Angemeldet", vehicle.IsRegistered ? "Ja" : "Nein"),
                ("Ausgemustert", vehicle.IsRetired ? "Ja" : "Nein")
            ],
            Sections = await BuildVehicleSectionsAsync(vehicleId, cancellationToken).ConfigureAwait(false)
        };

        var content = await _reports.CreateVehicleFileReportAsync(data, cancellationToken).ConfigureAwait(false);
        var fileName = $"Fahrzeugakte_{Sanitize(vehicle.LicensePlate ?? vehicle.InternalNumber)}_{_clock.Now:yyyyMMdd}.pdf";

        await _audit.WriteAsync(AuditAction.Printed, "VehicleFile", vehicleId.ToString(), vehicle.DisplayName,
            null, cancellationToken).ConfigureAwait(false);

        return new GeneratedReport(content, "VEHICLE_FILE", "Fahrzeugakte", fileName, vehicleId, null, null, false);
    }

    public async Task<int> ArchiveAsync(
        GeneratedReport report,
        byte[] content,
        CancellationToken cancellationToken = default)
    {
        if (!_storage.IsConfigured)
        {
            throw new BusinessRuleException(
                "Es ist keine Dokumentenablage konfiguriert. Der Bericht kann daher nicht am Fahrzeug abgelegt werden.");
        }

        var category = report.TemplateKey switch
        {
            "WORKSHOP_REPORT" => DocumentCategory.Werkstattbericht,
            "ACCIDENT_REPORT" => DocumentCategory.Unfallbericht,
            _ => DocumentCategory.Sonstiges
        };

        var folder = report.VehicleId is { } vehicleId
            ? Path.Combine("Fahrzeuge", await GetInternalNumberAsync(vehicleId, cancellationToken).ConfigureAwait(false),
                category.ToString())
            : Path.Combine("Allgemein", category.ToString());

        using var stream = new MemoryStream(content);
        var stored = await _storage.StoreAsync(stream, report.SuggestedFileName, folder, cancellationToken)
            .ConfigureAwait(false);

        var document = new VehicleDocument
        {
            VehicleId = report.VehicleId,
            Category = category,
            Title = report.Title,
            OriginalFileName = report.SuggestedFileName,
            RelativePath = stored.RelativePath,
            FileSizeBytes = stored.SizeBytes,
            Sha256 = stored.Sha256,
            ContentType = stored.ContentType,
            DocumentDate = _clock.Today
        };

        _db.VehicleDocuments.Add(document);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _db.GeneratedDocuments.Add(new GeneratedDocument
        {
            TemplateKey = report.TemplateKey,
            Title = report.Title,
            VehicleId = report.VehicleId,
            WorkshopOrderId = report.WorkshopOrderId,
            AccidentReportId = report.AccidentReportId,
            VehicleDocumentId = document.Id,
            GeneratedAt = _clock.Now,
            GeneratedByUserId = _currentUser.User?.Id,
            GeneratedByUserName = _currentUser.User?.DisplayName,
            IsBlankForm = report.IsBlankForm
        });

        if (report.WorkshopOrderId is { } orderId)
        {
            _db.WorkshopDocuments.Add(new WorkshopDocument
            {
                WorkshopOrderId = orderId,
                VehicleDocumentId = document.Id,
                Category = category,
                LinkedAt = _clock.Now,
                LinkedByUserId = _currentUser.User?.Id
            });
        }

        if (report.AccidentReportId is { } accidentId)
        {
            _db.AccidentAttachments.Add(new AccidentAttachment
            {
                AccidentReportId = accidentId,
                VehicleDocumentId = document.Id,
                LinkedAt = _clock.Now,
                LinkedByUserId = _currentUser.User?.Id,
                Caption = report.Title
            });
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return document.Id;
    }

    private async Task<ReportHeaderData> BuildHeaderAsync(CancellationToken cancellationToken)
    {
        var company = await _settings.GetCompanyProfileAsync(cancellationToken).ConfigureAwait(false);

        return new ReportHeaderData
        {
            CompanyName = company.Name,
            CompanyAddress = company.AddressLine,
            LogoPath = string.IsNullOrWhiteSpace(company.LogoPath) || !File.Exists(company.LogoPath)
                ? null
                : company.LogoPath,
            PrintedAt = _clock.Now,
            PrintedBy = _currentUser.User?.DisplayName,
            ApplicationVersion = await _settings
                .GetOrDefaultAsync(SettingsKeys.DatabaseSchemaVersion, "1.0.0", cancellationToken)
                .ConfigureAwait(false)
        };
    }

    private async Task<string?> GetTemplateNoticeAsync(
        string templateKey,
        string settingKey,
        CancellationToken cancellationToken)
    {
        var fromSettings = await _settings.GetAsync(settingKey, cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(fromSettings))
        {
            return fromSettings;
        }

        return await _db.DocumentTemplates
            .AsNoTracking()
            .Where(t => t.Key == templateKey)
            .Select(t => t.NoticeText)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<(string, IReadOnlyList<string>, IReadOnlyList<IReadOnlyList<string?>>)>>
        BuildVehicleSectionsAsync(int vehicleId, CancellationToken cancellationToken)
    {
        var sections = new List<(string, IReadOnlyList<string>, IReadOnlyList<IReadOnlyList<string?>>)>();

        var inspections = await _db.VehicleInspections
            .AsNoTracking()
            .Where(i => i.VehicleId == vehicleId)
            .OrderByDescending(i => i.InspectionDate)
            .Take(20)
            .Select(i => new { i.Type, i.InspectionDate, i.NextDueDate, i.Result, i.TestCenter, i.Mileage })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        sections.Add(("Hauptuntersuchung",
            new[] { "Art", "Prüfdatum", "Nächste Fälligkeit", "Ergebnis", "Prüfstelle", "Kilometerstand" },
            inspections.Select(i => (IReadOnlyList<string?>)
            [
                i.Type.ToString(), i.InspectionDate.ToString("dd.MM.yyyy"), i.NextDueDate.ToString("dd.MM.yyyy"),
                i.Result.ToString(), i.TestCenter, i.Mileage?.ToString("N0")
            ]).ToList()));

        var damages = await _db.DamageReports
            .AsNoTracking()
            .Where(d => d.VehicleId == vehicleId)
            .OrderByDescending(d => d.OccurredAt)
            .Take(30)
            .Select(d => new { d.DamageNumber, d.OccurredAt, d.Description, d.Priority, d.Status, d.CostActual })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        sections.Add(("Schäden",
            new[] { "Nummer", "Datum", "Beschreibung", "Priorität", "Status", "Kosten" },
            damages.Select(d => (IReadOnlyList<string?>)
            [
                d.DamageNumber, d.OccurredAt.ToString("dd.MM.yyyy"), d.Description,
                d.Priority.ToString(), d.Status.ToString(), d.CostActual?.ToString("N2")
            ]).ToList()));

        var orders = await _db.WorkshopOrders
            .AsNoTracking()
            .Where(o => o.VehicleId == vehicleId)
            .OrderByDescending(o => o.CreatedOn)
            .Take(30)
            .Select(o => new
            {
                o.OrderNumber, o.CreatedOn, WorkshopName = o.Workshop != null ? o.Workshop.Name : null,
                o.Status, o.CostNet, o.InvoiceNumber
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        sections.Add(("Werkstattvorgänge",
            new[] { "Vorgang", "Erstellt", "Werkstatt", "Status", "Kosten netto", "Rechnung" },
            orders.Select(o => (IReadOnlyList<string?>)
            [
                o.OrderNumber, o.CreatedOn.ToString("dd.MM.yyyy"), o.WorkshopName,
                o.Status.ToString(), o.CostNet?.ToString("N2"), o.InvoiceNumber
            ]).ToList()));

        var drivers = await _db.VehicleDriverAssignments
            .AsNoTracking()
            .Include(a => a.Driver)
            .Where(a => a.VehicleId == vehicleId)
            .OrderByDescending(a => a.ValidFrom)
            .Take(20)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        sections.Add(("Fahrerhistorie",
            new[] { "Fahrer", "Von", "Bis", "Zugewiesen durch" },
            drivers.Select(a => (IReadOnlyList<string?>)
            [
                a.Driver?.DisplayName, a.ValidFrom.ToString("dd.MM.yyyy"),
                a.ValidTo?.ToString("dd.MM.yyyy") ?? "aktuell", a.AssignedByUserName
            ]).ToList()));

        var plates = await _db.LicensePlateAssignments
            .AsNoTracking()
            .Include(a => a.LicensePlate)
            .Where(a => a.VehicleId == vehicleId)
            .OrderByDescending(a => a.ValidFrom)
            .Take(20)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        sections.Add(("Kennzeichenhistorie",
            new[] { "Kennzeichen", "Von", "Bis", "Grund" },
            plates.Select(a => (IReadOnlyList<string?>)
            [
                a.LicensePlate?.Plate, a.ValidFrom.ToString("dd.MM.yyyy"),
                a.ValidTo?.ToString("dd.MM.yyyy") ?? "aktuell", a.Reason
            ]).ToList()));

        return sections;
    }

    private async Task<string> GetInternalNumberAsync(int vehicleId, CancellationToken cancellationToken)
    {
        var number = await _db.Vehicles
            .Where(v => v.Id == vehicleId)
            .Select(v => v.InternalNumber)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return Sanitize(number ?? vehicleId.ToString());
    }

    private static string Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Fahrzeug";
        }

        var invalid = Path.GetInvalidFileNameChars();
        return new string(value.Select(c => invalid.Contains(c) || c == ' ' ? '_' : c).ToArray());
    }
}
