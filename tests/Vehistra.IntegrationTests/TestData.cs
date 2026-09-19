using Microsoft.EntityFrameworkCore;
using Vehistra.Domain.Entities;
using Vehistra.Domain.Enums;
using Vehistra.Infrastructure.Persistence;

namespace Vehistra.IntegrationTests;

/// <summary>Legt wiederkehrende Testdaten an.</summary>
public static class TestData
{
    public static async Task<Vehicle> AddVehicleAsync(
        VehistraDbContext db,
        string internalNumber = "T-01",
        string licensePlate = "FDS-AB 123",
        int mileage = 100_000,
        CancellationToken cancellationToken = default)
    {
        var statusId = await db.VehicleStatuses
            .OrderBy(s => s.SortOrder)
            .Select(s => s.Id)
            .FirstAsync(cancellationToken);

        var vehicle = new Vehicle
        {
            InternalNumber = internalNumber,
            LicensePlate = licensePlate,
            Manufacturer = "Mercedes-Benz",
            Model = "E-Klasse",
            BuildYear = 2022,
            FuelType = FuelType.Diesel,
            Transmission = TransmissionType.Automatik,
            CurrentMileage = mileage,
            VehicleStatusId = statusId
        };

        db.Vehicles.Add(vehicle);
        await db.SaveChangesAsync(cancellationToken);

        return vehicle;
    }

    public static async Task<Driver> AddDriverAsync(
        VehistraDbContext db,
        string firstName = "Anna",
        string lastName = "Beispiel",
        CancellationToken cancellationToken = default)
    {
        var driver = new Driver
        {
            FirstName = firstName,
            LastName = lastName,
            IsActive = true
        };

        db.Drivers.Add(driver);
        await db.SaveChangesAsync(cancellationToken);

        return driver;
    }
}
