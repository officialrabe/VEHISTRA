using Fuhrpark.Application.Abstractions;
using Fuhrpark.Domain.Entities;
using Fuhrpark.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fuhrpark.Infrastructure.Persistence.Seeding;

/// <summary>
/// Erzeugt Beispieldaten fuer Entwicklung und Schulung.
/// Diese Daten werden im Produktivbetrieb NIEMALS automatisch angelegt -
/// der Aufruf erfolgt ausschliesslich explizit.
/// </summary>
public sealed class DevelopmentDataSeeder
{
    private readonly FuhrparkDbContext _db;
    private readonly IClock _clock;
    private readonly ILogger<DevelopmentDataSeeder> _logger;

    public DevelopmentDataSeeder(FuhrparkDbContext db, IClock clock, ILogger<DevelopmentDataSeeder> logger)
    {
        _db = db;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>Legt zehn Fahrzeuge, fuenf Fahrer und zugehoerige Vorgaenge an.</summary>
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (await _db.Vehicles.AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            _logger.LogInformation("Beispieldaten werden uebersprungen: es sind bereits Fahrzeuge vorhanden.");
            return;
        }

        var today = _clock.Today;

        var statuses = await _db.VehicleStatuses.ToListAsync(cancellationToken).ConfigureAwait(false);
        var categories = await _db.VehicleCategories.ToListAsync(cancellationToken).ConfigureAwait(false);
        var damageCategories = await _db.DamageCategories.ToListAsync(cancellationToken).ConfigureAwait(false);
        var rules = await _db.MaintenanceRules.ToListAsync(cancellationToken).ConfigureAwait(false);

        int StatusId(VehicleStatusKind kind) => statuses.First(s => s.Kind == kind).Id;
        int CategoryId(string name) => categories.First(c => c.Name == name).Id;

        // Werkstaetten
        var workshop = new Workshop
        {
            Name = "Autohaus Beispiel GmbH",
            Street = "Industriestraße 12",
            PostalCode = "72250",
            City = "Freudenstadt",
            Phone = "07441 123456",
            ContactPerson = "Herr Meier",
            IsActive = true
        };

        var secondWorkshop = new Workshop
        {
            Name = "Reifenservice Süd",
            Street = "Bahnhofstraße 5",
            PostalCode = "72270",
            City = "Baiersbronn",
            Phone = "07442 654321",
            IsActive = true
        };

        _db.Workshops.AddRange(workshop, secondWorkshop);

        // Fahrer
        var drivers = new List<Driver>
        {
            new() { PersonnelNumber = "P-1001", FirstName = "Markus", LastName = "Berger", Phone = "0170 1111111", IsActive = true },
            new() { PersonnelNumber = "P-1002", FirstName = "Sabine", LastName = "Hoffmann", Phone = "0170 2222222", IsActive = true },
            new() { PersonnelNumber = "P-1003", FirstName = "Ali", LastName = "Yilmaz", Phone = "0170 3333333", IsActive = true },
            new() { PersonnelNumber = "P-1004", FirstName = "Petra", LastName = "Schneider", Phone = "0170 4444444", IsActive = true },
            new() { PersonnelNumber = "P-1005", FirstName = "Thomas", LastName = "Keller", Phone = "0170 5555555", IsActive = true }
        };

        _db.Drivers.AddRange(drivers);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Fahrzeuge
        var definitions = new (string Number, string Plate, string Make, string Model, string Category,
            VehicleStatusKind Status, int Mileage, int InspectionOffsetDays, int DriverIndex)[]
        {
            ("FZ-001", "FDS-AB 123", "Mercedes-Benz", "E 220 d", "Taxi", VehicleStatusKind.ImEinsatz, 184_500, 8, 0),
            ("FZ-002", "FDS-CD 456", "Volkswagen", "Caddy Maxi", "Kurier", VehicleStatusKind.Verfuegbar, 96_200, 45, 1),
            ("FZ-003", "FDS-EF 789", "Mercedes-Benz", "Vito Tourer", "Personenbeförderung", VehicleStatusKind.Schaden, 212_800, 120, 2),
            ("FZ-004", "FDS-GH 123", "Škoda", "Octavia Combi", "Taxi", VehicleStatusKind.Werkstatt, 143_000, 210, 3),
            ("FZ-005", "FDS-IJ 234", "Ford", "Transit Custom", "Kurier", VehicleStatusKind.Verfuegbar, 78_400, 300, 4),
            ("FZ-006", "FDS-KL 345", "Toyota", "Corolla Touring", "Ersatzfahrzeug", VehicleStatusKind.Verfuegbar, 32_100, 25, -1),
            ("FZ-007", "FDS-MN 456", "Volkswagen", "Passat Variant", "Verwaltung", VehicleStatusKind.Aktiv, 58_900, 180, -1),
            ("FZ-008", "FDS-OP 567", "Mercedes-Benz", "Sprinter", "Personenbeförderung", VehicleStatusKind.NichtFahrbereit, 265_300, -12, -1),
            ("FZ-009", "FDS-QR 678", "Opel", "Vivaro", "Kurier", VehicleStatusKind.Abgemeldet, 198_700, 60, -1),
            ("FZ-010", "FDS-ST 789", "Renault", "Kangoo", "Sonstige", VehicleStatusKind.Ausgemustert, 289_400, -400, -1)
        };

        var vehicles = new List<Vehicle>();

        foreach (var definition in definitions)
        {
            var vehicle = new Vehicle
            {
                InternalNumber = definition.Number,
                LicensePlate = definition.Plate,
                Vin = $"WDB{definition.Number.Replace("-", string.Empty)}0000{vehicles.Count + 1:D5}",
                Manufacturer = definition.Make,
                Model = definition.Model,
                BuildYear = today.Year - 4 - (vehicles.Count % 5),
                FirstRegistration = today.AddYears(-4 - (vehicles.Count % 5)).AddMonths(-vehicles.Count),
                FuelType = vehicles.Count % 3 == 0 ? FuelType.Diesel : FuelType.HybridBenzin,
                Transmission = vehicles.Count % 2 == 0 ? TransmissionType.Automatik : TransmissionType.Schaltgetriebe,
                PowerKw = 90 + vehicles.Count * 7,
                Color = vehicles.Count % 2 == 0 ? "Silber" : "Weiß",
                Seats = definition.Category == "Personenbeförderung" ? 8 : 5,
                CurrentMileage = definition.Mileage,
                CurrentMileageAt = _clock.Now.AddDays(-2),
                VehicleStatusId = StatusId(definition.Status),
                NextInspectionDue = today.AddDays(definition.InspectionOffsetDays),
                CurrentDriverId = definition.DriverIndex >= 0 ? drivers[definition.DriverIndex].Id : null,
                IsRegistered = definition.Status is not (VehicleStatusKind.Abgemeldet or VehicleStatusKind.Ausgemustert),
                IsRetired = definition.Status == VehicleStatusKind.Ausgemustert
            };

            vehicles.Add(vehicle);
            _db.Vehicles.Add(vehicle);
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Kategorien, Status- und Fahrerhistorie, Kilometerstaende, HU
        for (var i = 0; i < vehicles.Count; i++)
        {
            var vehicle = vehicles[i];
            var definition = definitions[i];

            _db.VehicleCategoryAssignments.Add(new VehicleCategoryAssignment
            {
                VehicleId = vehicle.Id,
                VehicleCategoryId = CategoryId(definition.Category),
                IsPrimary = true,
                AssignedAt = _clock.Now.AddMonths(-6)
            });

            _db.VehicleStatusHistory.Add(new VehicleStatusHistory
            {
                VehicleId = vehicle.Id,
                NewStatusId = vehicle.VehicleStatusId,
                ChangedAt = _clock.Now.AddMonths(-6),
                ChangedByUserName = "Beispieldaten",
                Reason = "Erfassung im System"
            });

            if (definition.DriverIndex >= 0)
            {
                _db.VehicleDriverAssignments.Add(new VehicleDriverAssignment
                {
                    VehicleId = vehicle.Id,
                    DriverId = drivers[definition.DriverIndex].Id,
                    ValidFrom = _clock.Now.AddMonths(-6),
                    AssignedByUserName = "Beispieldaten"
                });
            }

            for (var month = 6; month >= 0; month--)
            {
                _db.MileageEntries.Add(new MileageEntry
                {
                    VehicleId = vehicle.Id,
                    Mileage = vehicle.CurrentMileage - month * 1800,
                    RecordedAt = _clock.Now.AddMonths(-month),
                    Source = MileageSource.ManuelleEingabe,
                    RecordedByUserName = "Beispieldaten"
                });
            }

            _db.VehicleInspections.Add(new VehicleInspection
            {
                VehicleId = vehicle.Id,
                Type = InspectionType.HauptUndAbgasuntersuchung,
                InspectionDate = vehicle.NextInspectionDue!.Value.AddYears(-2),
                NextDueDate = vehicle.NextInspectionDue.Value,
                Result = InspectionResult.Bestanden,
                TestCenter = i % 2 == 0 ? "TÜV Süd Freudenstadt" : "DEKRA Horb",
                Mileage = Math.Max(0, vehicle.CurrentMileage - 25_000)
            });

            var rule = rules.FirstOrDefault(r => r.Name == "Ölwechsel");
            if (rule is not null)
            {
                _db.MaintenanceEntries.Add(new MaintenanceEntry
                {
                    VehicleId = vehicle.Id,
                    MaintenanceRuleId = rule.Id,
                    PerformedAt = _clock.Now.AddMonths(-8),
                    Mileage = Math.Max(0, vehicle.CurrentMileage - 18_500),
                    NextDueDate = _clock.Now.AddMonths(4),
                    NextDueMileage = Math.Max(0, vehicle.CurrentMileage - 18_500) + 20_000,
                    Workshop = workshop,
                    Cost = 189.90m
                });
            }
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Kennzeichen inklusive Reservierungen
        var plateEntities = new List<LicensePlate>();

        foreach (var vehicle in vehicles.Where(v => !string.IsNullOrWhiteSpace(v.LicensePlate)))
        {
            var plate = new LicensePlate
            {
                Plate = vehicle.LicensePlate!,
                Status = vehicle.IsRetired ? LicensePlateStatus.Verfuegbar : LicensePlateStatus.Vergeben,
                CurrentVehicleId = vehicle.IsRetired ? null : vehicle.Id,
                RegistrationOffice = "Landratsamt Freudenstadt"
            };

            plateEntities.Add(plate);
            _db.LicensePlates.Add(plate);
        }

        var reservedPlate = new LicensePlate
        {
            Plate = "FDS-UV 890",
            Status = LicensePlateStatus.Reserviert,
            RegistrationOffice = "Landratsamt Freudenstadt"
        };

        var freePlate = new LicensePlate
        {
            Plate = "FDS-WX 901",
            Status = LicensePlateStatus.Verfuegbar,
            RegistrationOffice = "Landratsamt Freudenstadt"
        };

        _db.LicensePlates.AddRange(reservedPlate, freePlate);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        foreach (var plate in plateEntities.Where(p => p.CurrentVehicleId is not null))
        {
            _db.LicensePlateAssignments.Add(new LicensePlateAssignment
            {
                LicensePlateId = plate.Id,
                VehicleId = plate.CurrentVehicleId!.Value,
                ValidFrom = _clock.Now.AddMonths(-6),
                Reason = "Erstzulassung",
                AssignedByUserName = "Beispieldaten"
            });
        }

        _db.LicensePlateReservations.Add(new LicensePlateReservation
        {
            LicensePlateId = reservedPlate.Id,
            ReservedAt = _clock.Now.AddDays(-87),
            ReservedUntil = _clock.Now.AddDays(3),
            RegistrationOffice = "Landratsamt Freudenstadt",
            ReservationNumber = "RES-2026-0042"
        });

        // Schaeden
        var bodyCategory = damageCategories.First(c => c.Name == "Karosserie");
        var glassCategory = damageCategories.First(c => c.Name == "Scheibe");
        var accidentCategory = damageCategories.First(c => c.Name == "Unfall");

        _db.DamageReports.AddRange(
            new DamageReport
            {
                DamageNumber = $"SCH-{today.Year}-00001",
                VehicleId = vehicles[2].Id,
                DriverId = drivers[2].Id,
                OccurredAt = _clock.Now.AddDays(-9),
                Mileage = vehicles[2].CurrentMileage - 400,
                Description = "Tiefe Kratzer und Delle an der Beifahrertür",
                Area = DamageArea.SeiteRechts,
                DamageCategoryId = bodyCategory.Id,
                Priority = DamagePriority.Kritisch,
                IsDriveable = true,
                RepairRequired = true,
                Status = DamageStatus.Geprueft,
                CostEstimate = 1250m
            },
            new DamageReport
            {
                DamageNumber = $"SCH-{today.Year}-00002",
                VehicleId = vehicles[7].Id,
                OccurredAt = _clock.Now.AddDays(-21),
                Mileage = vehicles[7].CurrentMileage - 1200,
                Description = "Steinschlag in der Windschutzscheibe, Riss über gesamte Breite",
                Area = DamageArea.VorneMitte,
                DamageCategoryId = glassCategory.Id,
                Priority = DamagePriority.Hoch,
                IsDriveable = false,
                RepairRequired = true,
                Status = DamageStatus.ReparaturGeplant,
                IsInsuranceCase = true,
                CostEstimate = 890m
            },
            new DamageReport
            {
                DamageNumber = $"SCH-{today.Year}-00003",
                VehicleId = vehicles[1].Id,
                DriverId = drivers[1].Id,
                OccurredAt = _clock.Now.AddDays(-65),
                Description = "Kleiner Parkrempler an der hinteren Stoßstange",
                Area = DamageArea.HintenLinks,
                DamageCategoryId = bodyCategory.Id,
                Priority = DamagePriority.Niedrig,
                Status = DamageStatus.Geschlossen,
                RepairedAt = _clock.Now.AddDays(-40),
                ClosedAt = _clock.Now.AddDays(-38),
                CostActual = 340m
            });

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Unfall
        var accident = new AccidentReport
        {
            AccidentNumber = $"UNF-{today.Year}-00001",
            VehicleId = vehicles[0].Id,
            DriverId = drivers[0].Id,
            DriverPhone = drivers[0].Phone,
            OccurredAt = _clock.Now.AddDays(-30),
            Location = "Kreuzung Stuttgarter Straße / Ringstraße",
            Street = "Stuttgarter Straße",
            PostalCode = "72250",
            City = "Freudenstadt",
            Mileage = vehicles[0].CurrentMileage - 2100,
            Type = AccidentType.UnfallMitFremdbeteiligung,
            CourseOfEvents = "Auffahrunfall an der Ampel. Der nachfolgende Pkw konnte nicht rechtzeitig bremsen.",
            ThirdPartyInvolved = true,
            PersonalInjury = false,
            PoliceInvolved = true,
            PoliceStation = "Polizeirevier Freudenstadt",
            PoliceFileNumber = "TB 2026/001234",
            VehicleDriveable = true
        };

        _db.AccidentReports.Add(accident);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _db.AccidentParticipants.Add(new AccidentParticipant
        {
            AccidentReportId = accident.Id,
            LicensePlate = "S-XY 4711",
            FirstName = "Jörg",
            LastName = "Wagner",
            Phone = "0711 998877",
            Street = "Hauptstraße 3",
            PostalCode = "70173",
            City = "Stuttgart",
            InsuranceCompany = "Beispiel Versicherung AG",
            InsuranceNumber = "VS-998877-1"
        });

        _db.AccidentWitnesses.Add(new AccidentWitness
        {
            AccidentReportId = accident.Id,
            FirstName = "Anna",
            LastName = "Klein",
            Phone = "0160 4455667"
        });

        _db.DamageReports.Add(new DamageReport
        {
            DamageNumber = $"SCH-{today.Year}-00004",
            VehicleId = vehicles[0].Id,
            DriverId = drivers[0].Id,
            OccurredAt = accident.OccurredAt,
            Mileage = accident.Mileage,
            Description = "Heckschaden durch Auffahrunfall",
            Area = DamageArea.HintenMitte,
            DamageCategoryId = accidentCategory.Id,
            Priority = DamagePriority.Hoch,
            IsDriveable = true,
            Status = DamageStatus.Werkstatt,
            IsInsuranceCase = true,
            AccidentReportId = accident.Id,
            CostEstimate = 3400m
        });

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Werkstattvorgaenge
        var order = new WorkshopOrder
        {
            OrderNumber = $"WS-{today.Year}-00001",
            VehicleId = vehicles[3].Id,
            DriverId = drivers[3].Id,
            WorkshopId = workshop.Id,
            CreatedOn = _clock.Now.AddDays(-19),
            ProposedDate = _clock.Now.AddDays(-18),
            AppointmentDate = _clock.Now.AddDays(-17),
            VehicleHandedOverAt = _clock.Now.AddDays(-17),
            PlannedCompletionAt = _clock.Now.AddDays(-12),
            Reason = "Inspektion und Bremsen",
            Status = WorkshopOrderStatus.WartetAufTeile,
            MileageAtHandover = vehicles[3].CurrentMileage
        };

        _db.WorkshopOrders.Add(order);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _db.WorkshopTasks.AddRange(
            new WorkshopTask { WorkshopOrderId = order.Id, Position = 1, Description = "Inspektion nach Herstellervorgabe" },
            new WorkshopTask { WorkshopOrderId = order.Id, Position = 2, Description = "Bremsbeläge vorne erneuern" },
            new WorkshopTask { WorkshopOrderId = order.Id, Position = 3, Description = "Ölwechsel inkl. Filter", IsCompleted = true, CompletedAt = _clock.Now.AddDays(-16) });

        var secondOrder = new WorkshopOrder
        {
            OrderNumber = $"WS-{today.Year}-00002",
            VehicleId = vehicles[7].Id,
            WorkshopId = secondWorkshop.Id,
            CreatedOn = _clock.Now.AddDays(-4),
            AppointmentDate = _clock.Now.Date.AddHours(9),
            Reason = "Windschutzscheibe erneuern",
            Status = WorkshopOrderStatus.TerminVereinbart
        };

        _db.WorkshopOrders.Add(secondOrder);

        // Versicherungen
        foreach (var vehicle in vehicles.Take(5))
        {
            _db.VehicleInsurances.Add(new VehicleInsurance
            {
                VehicleId = vehicle.Id,
                Company = "Beispiel Versicherung AG",
                PolicyNumber = $"VS-{vehicle.InternalNumber}",
                ContractNumber = $"VTR-{vehicle.Id:D4}",
                Kind = InsuranceKind.Vollkasko,
                ValidFrom = today.AddYears(-1),
                ValidTo = today.AddMonths(2),
                ContactPerson = "Frau Sommer",
                Phone = "0800 1234567",
                AnnualPremium = 1240m,
                DeductibleComprehensive = 1000m,
                DeductiblePartial = 300m,
                IsActive = true
            });
        }

        // Schluessel
        foreach (var vehicle in vehicles.Take(6))
        {
            _db.VehicleKeys.Add(new VehicleKey
            {
                VehicleId = vehicle.Id,
                KeyNumber = $"{vehicle.InternalNumber}-S1",
                Count = 2,
                StorageLocation = "Schlüsselschrank Disposition",
                IssuedToDriverId = vehicle.CurrentDriverId,
                IssuedAt = vehicle.CurrentDriverId is null ? null : _clock.Now.AddMonths(-6)
            });
        }

        // Ausmusterung des letzten Fahrzeugs
        _db.VehicleRetirements.Add(new VehicleRetirement
        {
            VehicleId = vehicles[9].Id,
            RetiredAt = _clock.Now.AddDays(-45),
            Reason = RetirementReason.WirtschaftlicherTotalschaden,
            IsDeregistered = true,
            DeregisteredAt = _clock.Now.AddDays(-46),
            LicensePlateRemoved = true,
            LastMileage = vehicles[9].CurrentMileage,
            IsScrapped = true,
            IsTotalLoss = true,
            Comment = "Reparaturkosten überstiegen den Restwert deutlich."
        });

        _db.VehicleRegistrations.Add(new VehicleRegistration
        {
            VehicleId = vehicles[8].Id,
            EventType = RegistrationEventType.Abmeldung,
            RegisteredAt = _clock.Now.AddYears(-3),
            DeregisteredAt = _clock.Now.AddDays(-20),
            LicensePlate = vehicles[8].LicensePlate,
            Reason = "Saisonale Stilllegung"
        });

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Beispieldaten wurden angelegt: {Vehicles} Fahrzeuge, {Drivers} Fahrer.",
            vehicles.Count, drivers.Count);
    }
}
