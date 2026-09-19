using Vehistra.Application.Abstractions;
using Vehistra.Application.Dtos;
using Vehistra.Domain.Common;
using Vehistra.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace Vehistra.Application.Services;

/// <inheritdoc />
public sealed class SearchService : ISearchService
{
    private readonly IVehistraDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public SearchService(IVehistraDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<SearchResultItem>> SearchAsync(
        string searchText,
        int maxResultsPerArea = 10,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(searchText) || searchText.Trim().Length < 2)
        {
            return [];
        }

        var text = searchText.Trim();
        var plateKey = LicensePlateFormatter.Normalize(text);
        var results = new List<SearchResultItem>();

        if (_currentUser.HasPermission(Permissions.VehicleView))
        {
            var vehicles = await _db.Vehicles
                .AsNoTracking()
                .Where(v => v.InternalNumber.Contains(text)
                            || (v.LicensePlate != null && (v.LicensePlate.Contains(text) || v.LicensePlate.Contains(plateKey)))
                            || (v.Vin != null && v.Vin.Contains(text))
                            || v.Manufacturer.Contains(text)
                            || v.Model.Contains(text))
                .OrderBy(v => v.InternalNumber)
                .Take(maxResultsPerArea)
                .Select(v => new
                {
                    v.Id,
                    v.InternalNumber,
                    v.LicensePlate,
                    v.Manufacturer,
                    v.Model,
                    v.Vin,
                    StatusName = v.Status != null ? v.Status.Name : string.Empty
                })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            results.AddRange(vehicles.Select(v => new SearchResultItem(
                "Fahrzeug", v.Id,
                $"{v.LicensePlate ?? v.InternalNumber} - {v.Manufacturer} {v.Model}",
                $"Interne Nummer {v.InternalNumber} · {v.StatusName}",
                v.Vin != null && v.Vin.Contains(text, StringComparison.OrdinalIgnoreCase) ? "FIN" : "Fahrzeug",
                v.Id)));
        }

        if (_currentUser.HasPermission(Permissions.DriverView))
        {
            var drivers = await _db.Drivers
                .AsNoTracking()
                .Where(d => d.FirstName.Contains(text)
                            || d.LastName.Contains(text)
                            || (d.PersonnelNumber != null && d.PersonnelNumber.Contains(text)))
                .OrderBy(d => d.LastName)
                .Take(maxResultsPerArea)
                .Select(d => new { d.Id, d.FirstName, d.LastName, d.PersonnelNumber, d.Phone })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            results.AddRange(drivers.Select(d => new SearchResultItem(
                "Fahrer", d.Id, $"{d.FirstName} {d.LastName}".Trim(),
                string.Join(" · ", new[] { d.PersonnelNumber, d.Phone }.Where(s => !string.IsNullOrWhiteSpace(s))),
                "Fahrer", null)));
        }

        if (_currentUser.HasPermission(Permissions.DamageView))
        {
            var damages = await _db.DamageReports
                .AsNoTracking()
                .Where(d => d.DamageNumber.Contains(text) || d.Description.Contains(text))
                .OrderByDescending(d => d.OccurredAt)
                .Take(maxResultsPerArea)
                .Select(d => new
                {
                    d.Id,
                    d.DamageNumber,
                    d.Description,
                    d.VehicleId,
                    d.OccurredAt,
                    VehicleDisplay = d.Vehicle != null ? (d.Vehicle.LicensePlate ?? d.Vehicle.InternalNumber) : null
                })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            results.AddRange(damages.Select(d => new SearchResultItem(
                "Schaden", d.Id, $"{d.DamageNumber} - {d.Description}",
                $"{d.VehicleDisplay} · {d.OccurredAt:dd.MM.yyyy}", "Schadensnummer", d.VehicleId)));
        }

        if (_currentUser.HasPermission(Permissions.AccidentView))
        {
            var accidents = await _db.AccidentReports
                .AsNoTracking()
                .Where(a => a.AccidentNumber.Contains(text)
                            || (a.Location != null && a.Location.Contains(text))
                            || (a.PoliceFileNumber != null && a.PoliceFileNumber.Contains(text)))
                .OrderByDescending(a => a.OccurredAt)
                .Take(maxResultsPerArea)
                .Select(a => new
                {
                    a.Id,
                    a.AccidentNumber,
                    a.Location,
                    a.VehicleId,
                    a.OccurredAt,
                    VehicleDisplay = a.Vehicle != null ? (a.Vehicle.LicensePlate ?? a.Vehicle.InternalNumber) : null
                })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            results.AddRange(accidents.Select(a => new SearchResultItem(
                "Unfall", a.Id, $"{a.AccidentNumber} - {a.Location}",
                $"{a.VehicleDisplay} · {a.OccurredAt:dd.MM.yyyy}", "Unfallnummer", a.VehicleId)));
        }

        if (_currentUser.HasPermission(Permissions.WorkshopView))
        {
            var orders = await _db.WorkshopOrders
                .AsNoTracking()
                .Where(w => w.OrderNumber.Contains(text)
                            || (w.InvoiceNumber != null && w.InvoiceNumber.Contains(text))
                            || (w.Reason != null && w.Reason.Contains(text)))
                .OrderByDescending(w => w.CreatedOn)
                .Take(maxResultsPerArea)
                .Select(w => new
                {
                    w.Id,
                    w.OrderNumber,
                    w.InvoiceNumber,
                    w.Reason,
                    w.VehicleId,
                    w.CreatedOn,
                    WorkshopName = w.Workshop != null ? w.Workshop.Name : null,
                    VehicleDisplay = w.Vehicle != null ? (w.Vehicle.LicensePlate ?? w.Vehicle.InternalNumber) : null
                })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            results.AddRange(orders.Select(w => new SearchResultItem(
                "Werkstattvorgang", w.Id, $"{w.OrderNumber} - {w.Reason}",
                $"{w.VehicleDisplay} · {w.WorkshopName} · {w.CreatedOn:dd.MM.yyyy}",
                w.InvoiceNumber != null && w.InvoiceNumber.Contains(text, StringComparison.OrdinalIgnoreCase)
                    ? "Rechnungsnummer"
                    : "Vorgangsnummer",
                w.VehicleId)));
        }

        if (_currentUser.HasPermission(Permissions.LicensePlateView))
        {
            var plates = await _db.LicensePlates
                .AsNoTracking()
                .Where(p => p.Plate.Contains(text) || p.Plate.Contains(plateKey))
                .OrderBy(p => p.Plate)
                .Take(maxResultsPerArea)
                .Select(p => new
                {
                    p.Id,
                    p.Plate,
                    p.Status,
                    p.CurrentVehicleId,
                    VehicleDisplay = p.CurrentVehicle != null ? p.CurrentVehicle.InternalNumber : null
                })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            results.AddRange(plates.Select(p => new SearchResultItem(
                "Kennzeichen", p.Id, p.Plate,
                $"{p.Status}{(p.VehicleDisplay is null ? string.Empty : $" · Fahrzeug {p.VehicleDisplay}")}",
                "Kennzeichen", p.CurrentVehicleId)));
        }

        return results;
    }
}
