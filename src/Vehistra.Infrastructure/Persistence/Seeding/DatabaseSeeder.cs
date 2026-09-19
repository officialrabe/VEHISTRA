using Vehistra.Application.Abstractions;
using Vehistra.Application.Services;
using Vehistra.Domain.Entities;
using Vehistra.Domain.Enums;
using Vehistra.Domain.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Vehistra.Infrastructure.Persistence.Seeding;

/// <summary>
/// Legt die notwendigen Stammdaten an (Rollen, Rechte, Status, Kategorien, Einstellungen).
/// Der Vorgang ist wiederholbar: bereits vorhandene Datensaetze bleiben unveraendert,
/// damit ein Update niemals Kundendaten ueberschreibt.
/// </summary>
public sealed class DatabaseSeeder
{
    private readonly VehistraDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<DatabaseSeeder> _logger;

    public DatabaseSeeder(VehistraDbContext db, IPasswordHasher passwordHasher, ILogger<DatabaseSeeder> logger)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    /// <summary>Legt alle Systemstammdaten an bzw. ergaenzt fehlende Eintraege.</summary>
    public async Task SeedSystemDataAsync(CancellationToken cancellationToken = default)
    {
        await SeedPermissionsAsync(cancellationToken).ConfigureAwait(false);
        await SeedRolesAsync(cancellationToken).ConfigureAwait(false);
        await SeedVehicleStatusesAsync(cancellationToken).ConfigureAwait(false);
        await SeedVehicleCategoriesAsync(cancellationToken).ConfigureAwait(false);
        await SeedDamageCategoriesAsync(cancellationToken).ConfigureAwait(false);
        await SeedMaintenanceRulesAsync(cancellationToken).ConfigureAwait(false);
        await SeedSettingsAsync(cancellationToken).ConfigureAwait(false);
        await SeedDocumentTemplatesAsync(cancellationToken).ConfigureAwait(false);
        await SeedMigrationLockAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Legt den ersten Administrator an. Wird vom Server-Setup aufgerufen.</summary>
    public async Task<int> CreateAdministratorAsync(
        string userName,
        string password,
        string firstName,
        string lastName,
        string? email,
        CancellationToken cancellationToken = default)
    {
        var existing = await _db.Users
            .FirstOrDefaultAsync(u => u.UserName == userName, cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            throw new InvalidOperationException($"Der Benutzer '{userName}' existiert bereits.");
        }

        var adminRole = await _db.Roles
            .FirstOrDefaultAsync(r => r.Name == RoleNames.Administrator, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                "Die Administratorrolle wurde nicht gefunden. Bitte zuerst die Stammdaten einrichten.");

        var user = new User
        {
            UserName = userName,
            PasswordHash = _passwordHasher.Hash(password),
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            IsActive = true,
            CreatedAt = DateTime.Now,
            PasswordChangedAt = DateTime.Now
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _db.UserRoles.Add(new UserRole
        {
            UserId = user.Id,
            RoleId = adminRole.Id,
            AssignedAt = DateTime.Now
        });

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Administratorkonto {UserName} wurde angelegt.", userName);

        return user.Id;
    }

    private async Task SeedPermissionsAsync(CancellationToken cancellationToken)
    {
        var existing = await _db.Permissions
            .Select(p => p.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var missing = Permissions.All
            .Where(p => !existing.Contains(p.Name, StringComparer.OrdinalIgnoreCase))
            .Select(p => new Permission { Name = p.Name, DisplayName = p.DisplayName, Group = p.Group })
            .ToList();

        if (missing.Count == 0)
        {
            return;
        }

        _db.Permissions.AddRange(missing);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("{Count} Berechtigungen ergaenzt.", missing.Count);
    }

    private async Task SeedRolesAsync(CancellationToken cancellationToken)
    {
        var permissions = await _db.Permissions.ToDictionaryAsync(p => p.Name, p => p.Id, StringComparer.OrdinalIgnoreCase, cancellationToken)
            .ConfigureAwait(false);

        foreach (var definition in RoleNames.Defaults)
        {
            var role = await _db.Roles
                .Include(r => r.RolePermissions)
                .FirstOrDefaultAsync(r => r.Name == definition.Name, cancellationToken)
                .ConfigureAwait(false);

            if (role is null)
            {
                role = new Role
                {
                    Name = definition.Name,
                    DisplayName = definition.DisplayName,
                    Description = definition.Description,
                    IsSystemRole = definition.IsSystemRole,
                    CreatedAt = DateTime.Now
                };

                _db.Roles.Add(role);
                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            // Bestehende Rollen behalten ihre angepassten Rechte; nur die Administratorrolle
            // erhaelt immer saemtliche - auch neu hinzugekommene - Berechtigungen.
            var isAdministrator = definition.Name == RoleNames.Administrator;
            var assigned = role.RolePermissions.Select(rp => rp.PermissionId).ToHashSet();

            var target = isAdministrator
                ? permissions.Values.ToList()
                : definition.Permissions
                    .Where(permissions.ContainsKey)
                    .Select(name => permissions[name])
                    .ToList();

            if (!isAdministrator && assigned.Count > 0)
            {
                // Rolle wurde bereits eingerichtet - Anpassungen des Kunden nicht ueberschreiben.
                continue;
            }

            var missing = target.Where(id => !assigned.Contains(id)).ToList();
            if (missing.Count == 0)
            {
                continue;
            }

            _db.RolePermissions.AddRange(missing.Select(id => new RolePermission
            {
                RoleId = role.Id,
                PermissionId = id
            }));

            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task SeedVehicleStatusesAsync(CancellationToken cancellationToken)
    {
        if (await _db.VehicleStatuses.AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        var statuses = new (VehicleStatusKind Kind, string Name, string Color, bool Operational, bool Available)[]
        {
            (VehicleStatusKind.Aktiv, "Aktiv", "#2E7D32", true, true),
            (VehicleStatusKind.Verfuegbar, "Verfügbar", "#2E7D32", true, true),
            (VehicleStatusKind.ImEinsatz, "Im Einsatz", "#1565C0", true, false),
            (VehicleStatusKind.Werkstatt, "Werkstatt", "#EF6C00", false, false),
            (VehicleStatusKind.Schaden, "Schaden", "#EF6C00", false, false),
            (VehicleStatusKind.NichtFahrbereit, "Nicht fahrbereit", "#C62828", false, false),
            (VehicleStatusKind.AusserBetrieb, "Außer Betrieb", "#616161", false, false),
            (VehicleStatusKind.Abgemeldet, "Abgemeldet", "#616161", false, false),
            (VehicleStatusKind.Ausgemustert, "Ausgemustert", "#424242", false, false)
        };

        var sortOrder = 10;
        foreach (var status in statuses)
        {
            _db.VehicleStatuses.Add(new VehicleStatus
            {
                Name = status.Name,
                Kind = status.Kind,
                ColorHex = status.Color,
                SortOrder = sortOrder,
                IsActive = true,
                IsSystemStatus = true,
                CountsAsOperational = status.Operational,
                CountsAsAvailable = status.Available,
                CreatedAt = DateTime.Now
            });

            sortOrder += 10;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task SeedVehicleCategoriesAsync(CancellationToken cancellationToken)
    {
        if (await _db.VehicleCategories.AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        var categories = new (string Name, string Color)[]
        {
            ("Taxi", "#F9A825"),
            ("Personenbeförderung", "#1565C0"),
            ("Kurier", "#6A1B9A"),
            ("Ersatzfahrzeug", "#00838F"),
            ("Verwaltung", "#455A64"),
            ("Sonstige", "#757575")
        };

        var sortOrder = 10;
        foreach (var category in categories)
        {
            _db.VehicleCategories.Add(new VehicleCategory
            {
                Name = category.Name,
                ColorHex = category.Color,
                SortOrder = sortOrder,
                IsActive = true,
                IsSystemCategory = true,
                CreatedAt = DateTime.Now
            });

            sortOrder += 10;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task SeedDamageCategoriesAsync(CancellationToken cancellationToken)
    {
        if (await _db.DamageCategories.AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        string[] categories =
        [
            "Karosserie", "Lack", "Reifen", "Scheibe", "Motor", "Getriebe",
            "Elektrik", "Innenraum", "Unfall", "Vandalismus", "Sonstiges"
        ];

        var sortOrder = 10;
        foreach (var category in categories)
        {
            _db.DamageCategories.Add(new DamageCategory
            {
                Name = category,
                SortOrder = sortOrder,
                IsActive = true,
                IsSystemCategory = true,
                CreatedAt = DateTime.Now
            });

            sortOrder += 10;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task SeedMaintenanceRulesAsync(CancellationToken cancellationToken)
    {
        if (await _db.MaintenanceRules.AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        var rules = new[]
        {
            new MaintenanceRule
            {
                Name = "Ölwechsel", IntervalType = MaintenanceIntervalType.DatumOderKilometer,
                IntervalKilometers = 20000, IntervalMonths = 12, SortOrder = 10
            },
            new MaintenanceRule
            {
                Name = "Inspektion", IntervalType = MaintenanceIntervalType.DatumOderKilometer,
                IntervalKilometers = 30000, IntervalMonths = 24, SortOrder = 20
            },
            new MaintenanceRule
            {
                Name = "Bremsflüssigkeit", IntervalType = MaintenanceIntervalType.Datum,
                IntervalMonths = 24, SortOrder = 30
            },
            new MaintenanceRule
            {
                Name = "Reifenprüfung", IntervalType = MaintenanceIntervalType.Datum,
                IntervalMonths = 6, SortOrder = 40
            },
            new MaintenanceRule
            {
                Name = "Klimaservice", IntervalType = MaintenanceIntervalType.Datum,
                IntervalMonths = 24, SortOrder = 50
            }
        };

        foreach (var rule in rules)
        {
            rule.IsActive = true;
            rule.IsSystemRule = true;
            rule.CreatedAt = DateTime.Now;
            _db.MaintenanceRules.Add(rule);
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task SeedSettingsAsync(CancellationToken cancellationToken)
    {
        var existing = await _db.SystemSettings
            .Select(s => s.Key)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var defaults = new (string Key, string? Value, string Category, string Type, string Description)[]
        {
            (SettingsKeys.CompanyName, "Mein Unternehmen", "Unternehmen", "string", "Name des Unternehmens für Oberfläche und Berichte"),
            (SettingsKeys.CompanyStreet, null, "Unternehmen", "string", "Straße und Hausnummer"),
            (SettingsKeys.CompanyPostalCode, null, "Unternehmen", "string", "Postleitzahl"),
            (SettingsKeys.CompanyCity, null, "Unternehmen", "string", "Ort"),
            (SettingsKeys.CompanyPhone, null, "Unternehmen", "string", "Telefonnummer"),
            (SettingsKeys.CompanyEmail, null, "Unternehmen", "string", "E-Mail-Adresse"),
            (SettingsKeys.CompanyLogoPath, null, "Unternehmen", "image", "Pfad zum Firmenlogo für PDF-Berichte"),

            (SettingsKeys.InspectionWarnCriticalDays, "14", "Fristen", "int", "TÜV: Warnung 'bald fällig' ab X Tagen"),
            (SettingsKeys.InspectionWarnWarningDays, "30", "Fristen", "int", "TÜV: Hinweis ab X Tagen"),
            (SettingsKeys.InspectionWarnUrgentDays, "7", "Fristen", "int",
                "TÜV: kritisch bereits X Tage vor dem Termin"),
            (SettingsKeys.WorkshopLongStayWarnDays, "7", "Fristen", "int",
                "Werkstatt: Warnung, wenn ein Fahrzeug länger als X Tage dort steht"),
            (SettingsKeys.BackupRetentionDays, "0", "Sicherung", "int",
                "Aufbewahrungsdauer der Sicherungen in Tagen (0 = nichts löschen)"),
            (SettingsKeys.MaintenanceWarnDays, "30", "Fristen", "int", "Wartung: Vorwarnzeit in Tagen"),
            (SettingsKeys.MaintenanceWarnKilometers, "1000", "Fristen", "int", "Wartung: Vorwarnung in Kilometern"),
            (SettingsKeys.PlateReservationWarnDays, "30;14;7;3;1", "Fristen", "string", "Kennzeichenreservierung: Warnungen X Tage vorher"),

            (SettingsKeys.DocumentsPath, null, "Pfade", "path", "Zentrale Dokumentenablage (UNC-Pfad)"),
            (SettingsKeys.BackupPath, null, "Pfade", "path", "Verzeichnis für Datenbanksicherungen (serverlokal)"),
            (SettingsKeys.UpdatePath, null, "Pfade", "path", "Zentrale Updateablage (UNC-Pfad)"),

            (SettingsKeys.DateFormat, "dd.MM.yyyy", "Darstellung", "string", "Datumsformat"),
            (SettingsKeys.DateTimeFormat, "dd.MM.yyyy HH:mm", "Darstellung", "string", "Datums- und Zeitformat"),
            (SettingsKeys.Theme, "Light", "Darstellung", "string", "Farbschema: Light oder Dark"),

            (SettingsKeys.LoginMaxFailedAttempts, "5", "Sicherheit", "int", "Maximale Fehlversuche vor Kontosperre"),
            (SettingsKeys.LoginLockoutMinutes, "15", "Sicherheit", "int", "Sperrdauer in Minuten"),
            (SettingsKeys.PasswordMinimumLength, "8", "Sicherheit", "int", "Mindestlänge des Passworts"),

            (SettingsKeys.CheckForUpdatesOnStart, "true", "Updates", "bool", "Beim Programmstart nach Updates suchen"),

            (SettingsKeys.WorkshopReportNotice, null, "Berichte", "string", "Zusätzlicher Hinweistext im Werkstattbericht"),
            (SettingsKeys.AccidentReportNotice, null, "Berichte", "string", "Zusätzlicher Hinweistext im Unfallbericht")
        };

        var missing = defaults
            .Where(d => !existing.Contains(d.Key, StringComparer.OrdinalIgnoreCase))
            .Select(d => new SystemSetting
            {
                Key = d.Key,
                Value = d.Value,
                Category = d.Category,
                DataType = d.Type,
                Description = d.Description,
                IsSystemSetting = true,
                CreatedAt = DateTime.Now
            })
            .ToList();

        if (missing.Count == 0)
        {
            return;
        }

        _db.SystemSettings.AddRange(missing);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task SeedDocumentTemplatesAsync(CancellationToken cancellationToken)
    {
        var existing = await _db.DocumentTemplates
            .Select(t => t.Key)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var templates = new (string Key, string Name, string Description, string? Notice)[]
        {
            ("WORKSHOP_REPORT", "Werkstattbericht", "Auftrag an die Werkstatt (DIN A4)",
                "Bitte den ausgefüllten Bericht gemeinsam mit dem Fahrzeug in der Werkstatt abgeben."),
            ("ACCIDENT_REPORT", "Unfall- / Schadensbericht", "Zweiseitiger Unfallbericht mit Unfallskizze",
                "Kein Schuldanerkenntnis abgeben. Angaben wahrheitsgemäß und vollständig ausfüllen."),
            ("VEHICLE_FILE", "Fahrzeugakte", "Zusammenfassung aller Daten eines Fahrzeugs", null),
            ("TABLE_REPORT", "Listenbericht", "Beliebige Liste als PDF (Querformat)", null)
        };

        var missing = templates
            .Where(t => !existing.Contains(t.Key, StringComparer.OrdinalIgnoreCase))
            .Select(t => new DocumentTemplate
            {
                Key = t.Key,
                Name = t.Name,
                Description = t.Description,
                NoticeText = t.Notice,
                IsActive = true,
                IsSystemTemplate = true,
                CreatedAt = DateTime.Now
            })
            .ToList();

        if (missing.Count == 0)
        {
            return;
        }

        _db.DocumentTemplates.AddRange(missing);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task SeedMigrationLockAsync(CancellationToken cancellationToken)
    {
        if (await _db.MigrationLocks.AnyAsync(l => l.LockKey == "SCHEMA_MIGRATION", cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        _db.MigrationLocks.Add(new MigrationLock
        {
            LockKey = "SCHEMA_MIGRATION",
            IsLocked = false,
            Comment = "Zentrale Sperre für Datenbankmigrationen"
        });

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
