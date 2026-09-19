using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using Vehistra.Application.Abstractions;
using Vehistra.Application.Dtos;
using Vehistra.Domain.Enums;
using Vehistra.Domain.Security;
using Vehistra.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Vehistra.Infrastructure.ImportExport;

/// <summary>Exportiert Listen nach CSV, XLSX und PDF.</summary>
public sealed class ExportService : IExportService
{
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

    private readonly VehistraDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ISettingsService _settings;
    private readonly IReportService? _reports;
    private readonly IAuditWriter _audit;
    private readonly IClock _clock;
    private readonly ILogger<ExportService> _logger;

    public ExportService(
        VehistraDbContext db,
        ICurrentUserService currentUser,
        ISettingsService settings,
        IAuditWriter audit,
        IClock clock,
        ILogger<ExportService> logger,
        IReportService? reports = null)
    {
        _db = db;
        _currentUser = currentUser;
        _settings = settings;
        _audit = audit;
        _clock = clock;
        _logger = logger;
        _reports = reports;
    }

    public async Task<string> ExportAsync(
        ExportArea area,
        ExportFormat format,
        string targetPath,
        CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.DataExport);

        var (title, columns, rows) = await LoadAsync(area, cancellationToken).ConfigureAwait(false);
        var path = await ExportTableAsync(title, columns, rows, format, targetPath, cancellationToken)
            .ConfigureAwait(false);

        await _audit.WriteAsync(AuditAction.Exported, area.ToString(), null, title,
            $"{rows.Count} Datensaetze nach {format}", cancellationToken).ConfigureAwait(false);

        return path;
    }

    public async Task<string> ExportTableAsync(
        string title,
        IReadOnlyList<string> columns,
        IReadOnlyList<IReadOnlyList<string?>> rows,
        ExportFormat format,
        string targetPath,
        CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.DataExport);

        var directory = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        switch (format)
        {
            case ExportFormat.Csv:
                await WriteCsvAsync(targetPath, columns, rows, cancellationToken).ConfigureAwait(false);
                break;

            case ExportFormat.Xlsx:
                WriteExcel(targetPath, title, columns, rows);
                break;

            case ExportFormat.Pdf:
                await WritePdfAsync(targetPath, title, columns, rows, cancellationToken).ConfigureAwait(false);
                break;

            default:
                throw new NotSupportedException($"Das Exportformat '{format}' wird nicht unterstuetzt.");
        }

        _logger.LogInformation("Export erstellt: {Path} ({Rows} Zeilen)", targetPath, rows.Count);
        return targetPath;
    }

    private static async Task WriteCsvAsync(
        string targetPath,
        IReadOnlyList<string> columns,
        IReadOnlyList<IReadOnlyList<string?>> rows,
        CancellationToken cancellationToken)
    {
        // UTF-8 mit BOM, damit Excel Umlaute korrekt anzeigt.
        await using var writer = new StreamWriter(targetPath, false, new UTF8Encoding(true));

        await writer.WriteLineAsync(string.Join(';', columns.Select(EscapeCsv))).ConfigureAwait(false);

        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await writer.WriteLineAsync(string.Join(';', row.Select(value => EscapeCsv(value ?? string.Empty))))
                .ConfigureAwait(false);
        }
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains(';') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return '"' + value.Replace("\"", "\"\"") + '"';
        }

        return value;
    }

    private static void WriteExcel(
        string targetPath,
        string title,
        IReadOnlyList<string> columns,
        IReadOnlyList<IReadOnlyList<string?>> rows)
    {
        using var workbook = new XLWorkbook();
        var sheetName = title.Length > 31 ? title[..31] : title;
        var worksheet = workbook.Worksheets.Add(string.IsNullOrWhiteSpace(sheetName) ? "Export" : sheetName);

        for (var i = 0; i < columns.Count; i++)
        {
            var cell = worksheet.Cell(1, i + 1);
            cell.Value = columns[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.LightGray;
        }

        for (var r = 0; r < rows.Count; r++)
        {
            for (var c = 0; c < columns.Count && c < rows[r].Count; c++)
            {
                worksheet.Cell(r + 2, c + 1).Value = rows[r][c] ?? string.Empty;
            }
        }

        worksheet.SheetView.FreezeRows(1);
        worksheet.RangeUsed()?.SetAutoFilter();
        worksheet.Columns().AdjustToContents(1, 60d);

        workbook.SaveAs(targetPath);
    }

    private async Task WritePdfAsync(
        string targetPath,
        string title,
        IReadOnlyList<string> columns,
        IReadOnlyList<IReadOnlyList<string?>> rows,
        CancellationToken cancellationToken)
    {
        if (_reports is null)
        {
            throw new InvalidOperationException(
                "Der PDF-Export steht in dieser Programmkomponente nicht zur Verfuegung.");
        }

        var company = await _settings.GetCompanyProfileAsync(cancellationToken).ConfigureAwait(false);

        var bytes = await _reports.CreateTableReportAsync(new TableReportData
        {
            Header = new ReportHeaderData
            {
                CompanyName = company.Name,
                CompanyAddress = company.AddressLine,
                LogoPath = company.LogoPath,
                PrintedAt = _clock.Now,
                PrintedBy = _currentUser.User?.DisplayName
            },
            Title = title,
            Subtitle = $"{rows.Count} Datensätze · Stand {_clock.Now:dd.MM.yyyy HH:mm}",
            Columns = columns,
            Rows = rows,
            Landscape = columns.Count > 6
        }, cancellationToken).ConfigureAwait(false);

        await File.WriteAllBytesAsync(targetPath, bytes, cancellationToken).ConfigureAwait(false);
    }

    private async Task<(string Title, IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyList<string?>> Rows)>
        LoadAsync(ExportArea area, CancellationToken cancellationToken)
    {
        switch (area)
        {
            case ExportArea.Vehicles:
            {
                var data = await _db.Vehicles.AsNoTracking()
                    .OrderBy(v => v.InternalNumber)
                    .Select(v => new
                    {
                        v.InternalNumber, v.LicensePlate, v.Vin, v.Manufacturer, v.Model, v.Variant,
                        v.BuildYear, v.FirstRegistration, v.FuelType, v.Transmission, v.PowerKw,
                        v.Color, v.Seats, v.CurrentMileage, v.Hsn, v.Tsn, v.NextInspectionDue,
                        v.IsRegistered, v.IsRetired,
                        Status = v.Status != null ? v.Status.Name : null,
                        Driver = v.CurrentDriver != null ? v.CurrentDriver.FirstName + " " + v.CurrentDriver.LastName : null,
                        Categories = string.Join(", ", v.CategoryAssignments.Where(a => a.Category != null).Select(a => a.Category!.Name))
                    })
                    .ToListAsync(cancellationToken).ConfigureAwait(false);

                return ("Fahrzeuge",
                    ["Interne Nummer", "Kennzeichen", "FIN", "Hersteller", "Modell", "Variante", "Baujahr",
                     "Erstzulassung", "Kraftstoff", "Getriebe", "Leistung (kW)", "Farbe", "Sitzplätze",
                     "Kilometerstand", "HSN", "TSN", "Nächste HU", "Status", "Fester Fahrer", "Kategorien",
                     "Angemeldet", "Ausgemustert"],
                    data.Select(v => (IReadOnlyList<string?>)
                    [
                        v.InternalNumber, v.LicensePlate, v.Vin, v.Manufacturer, v.Model, v.Variant,
                        v.BuildYear?.ToString(), Format(v.FirstRegistration), v.FuelType.ToString(),
                        v.Transmission.ToString(), v.PowerKw?.ToString(), v.Color, v.Seats?.ToString(),
                        v.CurrentMileage.ToString("N0", German), v.Hsn, v.Tsn, Format(v.NextInspectionDue),
                        v.Status, v.Driver, v.Categories, YesNo(v.IsRegistered), YesNo(v.IsRetired)
                    ]).ToList());
            }

            case ExportArea.Drivers:
            {
                var data = await _db.Drivers.AsNoTracking()
                    .OrderBy(d => d.LastName).ThenBy(d => d.FirstName)
                    .Select(d => new
                    {
                        d.PersonnelNumber, d.FirstName, d.LastName, d.Phone, d.Mobile, d.Email, d.IsActive,
                        Vehicle = _db.Vehicles.Where(v => v.CurrentDriverId == d.Id)
                            .Select(v => v.LicensePlate ?? v.InternalNumber).FirstOrDefault()
                    })
                    .ToListAsync(cancellationToken).ConfigureAwait(false);

                return ("Fahrer",
                    ["Personalnummer", "Vorname", "Nachname", "Telefon", "Mobil", "E-Mail", "Aktiv", "Aktuelles Fahrzeug"],
                    data.Select(d => (IReadOnlyList<string?>)
                    [
                        d.PersonnelNumber, d.FirstName, d.LastName, d.Phone, d.Mobile, d.Email,
                        YesNo(d.IsActive), d.Vehicle
                    ]).ToList());
            }

            case ExportArea.Inspections:
            {
                var data = await _db.VehicleInspections.AsNoTracking()
                    .OrderByDescending(i => i.InspectionDate)
                    .Select(i => new
                    {
                        Vehicle = i.Vehicle != null ? (i.Vehicle.LicensePlate ?? i.Vehicle.InternalNumber) : null,
                        i.Type, i.InspectionDate, i.NextDueDate, i.Result, i.TestCenter, i.Mileage, i.Cost, i.Defects
                    })
                    .ToListAsync(cancellationToken).ConfigureAwait(false);

                return ("TÜV und Fristen",
                    ["Fahrzeug", "Art", "Prüfdatum", "Nächste Fälligkeit", "Ergebnis", "Prüfstelle",
                     "Kilometerstand", "Kosten", "Mängel"],
                    data.Select(i => (IReadOnlyList<string?>)
                    [
                        i.Vehicle, i.Type.ToString(), Format(i.InspectionDate), Format(i.NextDueDate),
                        i.Result.ToString(), i.TestCenter, i.Mileage?.ToString("N0", German),
                        i.Cost?.ToString("N2", German), i.Defects
                    ]).ToList());
            }

            case ExportArea.Maintenance:
            {
                var data = await _db.MaintenanceEntries.AsNoTracking()
                    .OrderByDescending(m => m.PerformedAt)
                    .Select(m => new
                    {
                        Vehicle = m.Vehicle != null ? (m.Vehicle.LicensePlate ?? m.Vehicle.InternalNumber) : null,
                        Rule = m.Rule != null ? m.Rule.Name : m.Title,
                        m.PerformedAt, m.Mileage, m.NextDueDate, m.NextDueMileage, m.Cost,
                        Workshop = m.Workshop != null ? m.Workshop.Name : m.PerformedBy
                    })
                    .ToListAsync(cancellationToken).ConfigureAwait(false);

                return ("Wartung",
                    ["Fahrzeug", "Wartung", "Durchgeführt am", "Kilometerstand", "Nächste Fälligkeit",
                     "Nächste Fälligkeit (km)", "Kosten", "Ausgeführt von"],
                    data.Select(m => (IReadOnlyList<string?>)
                    [
                        m.Vehicle, m.Rule, Format(m.PerformedAt), m.Mileage?.ToString("N0", German),
                        Format(m.NextDueDate), m.NextDueMileage?.ToString("N0", German),
                        m.Cost?.ToString("N2", German), m.Workshop
                    ]).ToList());
            }

            case ExportArea.Damages:
            {
                var data = await _db.DamageReports.AsNoTracking()
                    .OrderByDescending(d => d.OccurredAt)
                    .Select(d => new
                    {
                        d.DamageNumber,
                        Vehicle = d.Vehicle != null ? (d.Vehicle.LicensePlate ?? d.Vehicle.InternalNumber) : null,
                        Driver = d.Driver != null ? d.Driver.FirstName + " " + d.Driver.LastName : null,
                        d.OccurredAt, d.Description,
                        Category = d.Category != null ? d.Category.Name : null,
                        d.Priority, d.Status, d.IsDriveable, d.CostActual, d.IsInsuranceCase, d.RepairedAt
                    })
                    .ToListAsync(cancellationToken).ConfigureAwait(false);

                return ("Schäden",
                    ["Schadensnummer", "Fahrzeug", "Fahrer", "Datum", "Beschreibung", "Kategorie", "Priorität",
                     "Status", "Fahrbereit", "Kosten", "Versicherungsfall", "Repariert am"],
                    data.Select(d => (IReadOnlyList<string?>)
                    [
                        d.DamageNumber, d.Vehicle, d.Driver, Format(d.OccurredAt), d.Description, d.Category,
                        d.Priority.ToString(), d.Status.ToString(), YesNo(d.IsDriveable),
                        d.CostActual?.ToString("N2", German), YesNo(d.IsInsuranceCase), Format(d.RepairedAt)
                    ]).ToList());
            }

            case ExportArea.Accidents:
            {
                var data = await _db.AccidentReports.AsNoTracking()
                    .OrderByDescending(a => a.OccurredAt)
                    .Select(a => new
                    {
                        a.AccidentNumber,
                        Vehicle = a.Vehicle != null ? (a.Vehicle.LicensePlate ?? a.Vehicle.InternalNumber) : null,
                        Driver = a.Driver != null ? a.Driver.FirstName + " " + a.Driver.LastName : null,
                        a.OccurredAt, a.Location, a.Type, a.ThirdPartyInvolved, a.PersonalInjury,
                        a.PoliceInvolved, a.PoliceFileNumber, a.VehicleDriveable, a.ClosedAt
                    })
                    .ToListAsync(cancellationToken).ConfigureAwait(false);

                return ("Unfälle",
                    ["Unfallnummer", "Fahrzeug", "Fahrer", "Datum", "Ort", "Art", "Fremdbeteiligung",
                     "Personenschaden", "Polizei", "Tagebuchnummer", "Fahrbereit", "Abgeschlossen am"],
                    data.Select(a => (IReadOnlyList<string?>)
                    [
                        a.AccidentNumber, a.Vehicle, a.Driver, Format(a.OccurredAt), a.Location, a.Type.ToString(),
                        YesNo(a.ThirdPartyInvolved), YesNo(a.PersonalInjury), YesNo(a.PoliceInvolved),
                        a.PoliceFileNumber, YesNo(a.VehicleDriveable), Format(a.ClosedAt)
                    ]).ToList());
            }

            case ExportArea.WorkshopOrders:
            {
                var data = await _db.WorkshopOrders.AsNoTracking()
                    .OrderByDescending(w => w.CreatedOn)
                    .Select(w => new
                    {
                        w.OrderNumber,
                        Vehicle = w.Vehicle != null ? (w.Vehicle.LicensePlate ?? w.Vehicle.InternalNumber) : null,
                        Workshop = w.Workshop != null ? w.Workshop.Name : null,
                        w.CreatedOn, w.AppointmentDate, w.Status, w.CompletedAt, w.PickedUpAt,
                        w.CostNet, w.CostGross, w.InvoiceNumber, w.NextServiceMileage, w.Reason
                    })
                    .ToListAsync(cancellationToken).ConfigureAwait(false);

                return ("Werkstattvorgänge",
                    ["Vorgangsnummer", "Fahrzeug", "Werkstatt", "Erstellt am", "Termin", "Status", "Fertig am",
                     "Abgeholt am", "Kosten netto", "Kosten brutto", "Rechnungsnummer", "Nächster Service (km)", "Grund"],
                    data.Select(w => (IReadOnlyList<string?>)
                    [
                        w.OrderNumber, w.Vehicle, w.Workshop, Format(w.CreatedOn), Format(w.AppointmentDate),
                        w.Status.ToString(), Format(w.CompletedAt), Format(w.PickedUpAt),
                        w.CostNet?.ToString("N2", German), w.CostGross?.ToString("N2", German),
                        w.InvoiceNumber, w.NextServiceMileage?.ToString("N0", German), w.Reason
                    ]).ToList());
            }

            case ExportArea.LicensePlates:
            {
                var data = await _db.LicensePlates.AsNoTracking()
                    .OrderBy(p => p.Plate)
                    .Select(p => new
                    {
                        p.Plate, p.Status,
                        Vehicle = p.CurrentVehicle != null ? p.CurrentVehicle.InternalNumber : null,
                        p.RegistrationOffice,
                        ReservedUntil = p.Reservations.Where(r => !r.IsReleased)
                            .OrderByDescending(r => r.ReservedUntil).Select(r => (DateTime?)r.ReservedUntil).FirstOrDefault(),
                        ReservationNumber = p.Reservations.Where(r => !r.IsReleased)
                            .OrderByDescending(r => r.ReservedUntil).Select(r => r.ReservationNumber).FirstOrDefault(),
                        p.Comment
                    })
                    .ToListAsync(cancellationToken).ConfigureAwait(false);

                return ("Kennzeichen",
                    ["Kennzeichen", "Status", "Fahrzeug", "Zulassungsstelle", "Reserviert bis",
                     "Reservierungsnummer", "Bemerkung"],
                    data.Select(p => (IReadOnlyList<string?>)
                    [
                        p.Plate, p.Status.ToString(), p.Vehicle, p.RegistrationOffice,
                        Format(p.ReservedUntil), p.ReservationNumber, p.Comment
                    ]).ToList());
            }

            case ExportArea.RetiredVehicles:
            {
                var data = await _db.VehicleRetirements.AsNoTracking()
                    .OrderByDescending(r => r.RetiredAt)
                    .Select(r => new
                    {
                        Vehicle = r.Vehicle != null ? r.Vehicle.InternalNumber : null,
                        Plate = r.Vehicle != null ? r.Vehicle.LicensePlate : null,
                        Model = r.Vehicle != null ? r.Vehicle.Manufacturer + " " + r.Vehicle.Model : null,
                        r.RetiredAt, r.Reason, r.ReasonText, r.IsDeregistered, r.DeregisteredAt,
                        r.LastMileage, r.IsSold, r.SalePrice, r.IsScrapped, r.IsTotalLoss,
                        r.IsLeasingReturn, r.IsSparePartDonor, r.Comment
                    })
                    .ToListAsync(cancellationToken).ConfigureAwait(false);

                return ("Ausgemusterte Fahrzeuge",
                    ["Interne Nummer", "Kennzeichen", "Fahrzeug", "Ausgemustert am", "Grund", "Grund (Text)",
                     "Abgemeldet", "Abmeldedatum", "Letzter Kilometerstand", "Verkauft", "Verkaufspreis",
                     "Verschrottet", "Totalschaden", "Leasingrückgabe", "Ersatzteilspender", "Bemerkung"],
                    data.Select(r => (IReadOnlyList<string?>)
                    [
                        r.Vehicle, r.Plate, r.Model, Format(r.RetiredAt), r.Reason.ToString(), r.ReasonText,
                        YesNo(r.IsDeregistered), Format(r.DeregisteredAt), r.LastMileage?.ToString("N0", German),
                        YesNo(r.IsSold), r.SalePrice?.ToString("N2", German), YesNo(r.IsScrapped),
                        YesNo(r.IsTotalLoss), YesNo(r.IsLeasingReturn), YesNo(r.IsSparePartDonor), r.Comment
                    ]).ToList());
            }

            default:
                throw new NotSupportedException($"Der Exportbereich '{area}' wird nicht unterstuetzt.");
        }
    }

    private static string? Format(DateTime? value) => value?.ToString("dd.MM.yyyy", German);

    private static string YesNo(bool value) => value ? "Ja" : "Nein";
}
