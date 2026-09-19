using System.Globalization;
using Vehistra.Application.Abstractions;
using Vehistra.Application.Dtos;
using Vehistra.Domain.Common;
using Vehistra.Domain.Entities;
using Vehistra.Domain.Enums;
using Vehistra.Domain.Security;
using Vehistra.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Vehistra.Infrastructure.ImportExport;

/// <summary>
/// Import-Assistent fuer Fahrzeuge, Fahrer und Kennzeichen aus CSV- und XLSX-Dateien.
/// Bestehende Datensaetze werden niemals stillschweigend ueberschrieben.
/// </summary>
public sealed class ImportService : IImportService
{
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

    private readonly VehistraDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditWriter _audit;
    private readonly IClock _clock;
    private readonly ILogger<ImportService> _logger;

    public ImportService(
        VehistraDbContext db,
        ICurrentUserService currentUser,
        IAuditWriter audit,
        IClock clock,
        ILogger<ImportService> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _audit = audit;
        _clock = clock;
        _logger = logger;
    }

    public Task<ImportPreview> PreviewAsync(string filePath, CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.DataImport);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Die Datei '{filePath}' wurde nicht gefunden.", filePath);
        }

        var (columns, rows) = TabularFileReader.Read(filePath, maxRows: 10);

        return Task.FromResult(new ImportPreview
        {
            FilePath = filePath,
            Columns = columns,
            SampleRows = rows.Select(r => (IReadOnlyList<string>)r).ToList(),
            TotalRows = TabularFileReader.CountRows(filePath)
        });
    }

    public IReadOnlyList<ImportTargetField> GetTargetFields(ExportArea area) => area switch
    {
        ExportArea.Vehicles =>
        [
            new("InternalNumber", "Interne Fahrzeugnummer", true, true),
            new("LicensePlate", "Kennzeichen", false, false),
            new("Vin", "FIN / VIN", false, false),
            new("Manufacturer", "Hersteller", true, false),
            new("Model", "Modell", true, false),
            new("Variant", "Variante", false, false),
            new("BuildYear", "Baujahr", false, false),
            new("FirstRegistration", "Erstzulassung", false, false),
            new("FuelType", "Kraftstoffart", false, false),
            new("Transmission", "Getriebe", false, false),
            new("PowerKw", "Leistung (kW)", false, false),
            new("Color", "Farbe", false, false),
            new("Seats", "Sitzplätze", false, false),
            new("CurrentMileage", "Kilometerstand", false, false),
            new("Hsn", "HSN", false, false),
            new("Tsn", "TSN", false, false),
            new("Category", "Kategorie", false, false),
            new("Comment", "Bemerkung", false, false)
        ],

        ExportArea.Drivers =>
        [
            new("PersonnelNumber", "Personalnummer", false, true),
            new("FirstName", "Vorname", false, false),
            new("LastName", "Nachname", true, false),
            new("Phone", "Telefon", false, false),
            new("Mobile", "Mobil", false, false),
            new("Email", "E-Mail", false, false),
            new("Comment", "Bemerkung", false, false)
        ],

        ExportArea.LicensePlates =>
        [
            new("Plate", "Kennzeichen", true, true),
            new("RegistrationOffice", "Zulassungsstelle", false, false),
            new("Comment", "Bemerkung", false, false)
        ],

        _ => throw new NotSupportedException(
            $"Fuer den Bereich '{area}' steht kein Import zur Verfuegung. " +
            "Unterstuetzt werden Fahrzeuge, Fahrer und Kennzeichen.")
    };

    public IReadOnlyList<ImportColumnMapping> SuggestMapping(ExportArea area, IReadOnlyList<string> columns)
    {
        var fields = GetTargetFields(area);

        return columns.Select(column =>
        {
            var normalized = Normalize(column);

            var match = fields.FirstOrDefault(f =>
                Normalize(f.DisplayName) == normalized || Normalize(f.Name) == normalized)
                ?? fields.FirstOrDefault(f =>
                    Normalize(f.DisplayName).StartsWith(normalized, StringComparison.Ordinal)
                    || normalized.StartsWith(Normalize(f.DisplayName), StringComparison.Ordinal));

            return new ImportColumnMapping { SourceColumn = column, TargetField = match?.Name };
        }).ToList();
    }

    public async Task<ImportValidationResult> ValidateAsync(
        string filePath,
        ExportArea area,
        IReadOnlyList<ImportColumnMapping> mapping,
        CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.DataImport);

        var fields = GetTargetFields(area);
        var (columns, rows) = TabularFileReader.Read(filePath);
        var issues = new List<ImportIssue>();

        // Pflichtfelder pruefen
        var mapped = mapping.Where(m => !string.IsNullOrWhiteSpace(m.TargetField))
            .Select(m => m.TargetField!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var required in fields.Where(f => f.IsRequired && !mapped.Contains(f.Name)))
        {
            issues.Add(new ImportIssue(0, required.DisplayName,
                $"Das Pflichtfeld '{required.DisplayName}' wurde keiner Spalte zugeordnet.", true));
        }

        var keyField = fields.FirstOrDefault(f => f.IsKey);
        var existingKeys = keyField is null
            ? []
            : await LoadExistingKeysAsync(area, cancellationToken).ConfigureAwait(false);

        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var valid = 0;
        var invalid = 0;
        var newRecords = 0;
        var existingRecords = 0;

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var rowNumber = rowIndex + 2; // Kopfzeile ist Zeile 1
            var values = ReadRow(columns, rows[rowIndex], mapping);
            var rowHasError = false;

            foreach (var required in fields.Where(f => f.IsRequired && mapped.Contains(f.Name)))
            {
                if (string.IsNullOrWhiteSpace(values.GetValueOrDefault(required.Name)))
                {
                    issues.Add(new ImportIssue(rowNumber, required.DisplayName,
                        $"'{required.DisplayName}' darf nicht leer sein.", true));
                    rowHasError = true;
                }
            }

            // Datentypen pruefen
            foreach (var (field, value) in values)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                var error = ValidateFieldValue(field, value);
                if (error is not null)
                {
                    issues.Add(new ImportIssue(rowNumber, field, error, true));
                    rowHasError = true;
                }
            }

            if (keyField is not null && values.TryGetValue(keyField.Name, out var key) && !string.IsNullOrWhiteSpace(key))
            {
                var normalizedKey = area == ExportArea.LicensePlates ? LicensePlateFormatter.Normalize(key) : key.Trim();

                if (!seenKeys.Add(normalizedKey))
                {
                    issues.Add(new ImportIssue(rowNumber, keyField.DisplayName,
                        $"Der Wert '{normalizedKey}' kommt in der Datei mehrfach vor.", true));
                    rowHasError = true;
                }
                else if (existingKeys.Contains(normalizedKey))
                {
                    existingRecords++;
                    issues.Add(new ImportIssue(rowNumber, keyField.DisplayName,
                        $"'{normalizedKey}' existiert bereits. Der Datensatz wird nur bei aktivierter " +
                        "Option 'Bestehende aktualisieren' geaendert.", false));
                }
                else
                {
                    newRecords++;
                }
            }

            if (rowHasError)
            {
                invalid++;
            }
            else
            {
                valid++;
            }
        }

        return new ImportValidationResult
        {
            Issues = issues,
            ValidRows = valid,
            InvalidRows = invalid,
            NewRecords = newRecords,
            ExistingRecords = existingRecords
        };
    }

    public async Task<ImportResult> ImportAsync(
        string filePath,
        ExportArea area,
        IReadOnlyList<ImportColumnMapping> mapping,
        bool updateExisting,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _currentUser.DemandPermission(Permissions.DataImport);

        var validation = await ValidateAsync(filePath, area, mapping, cancellationToken).ConfigureAwait(false);
        if (validation.HasErrors)
        {
            return new ImportResult(0, 0, validation.InvalidRows,
                validation.Issues.Where(i => i.IsError).ToList());
        }

        var (columns, rows) = TabularFileReader.Read(filePath);
        var issues = new List<ImportIssue>();
        var imported = 0;
        var skipped = 0;
        var failed = 0;

        var defaultStatusId = await _db.VehicleStatuses
            .Where(s => s.Kind == VehicleStatusKind.Aktiv)
            .Select(s => s.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var categories = await _db.VehicleCategories
            .ToDictionaryAsync(c => c.Name, c => c.Id, StringComparer.OrdinalIgnoreCase, cancellationToken)
            .ConfigureAwait(false);

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var rowNumber = rowIndex + 2;
            var values = ReadRow(columns, rows[rowIndex], mapping);

            try
            {
                var result = area switch
                {
                    ExportArea.Vehicles => await ImportVehicleAsync(
                        values, updateExisting, defaultStatusId, categories, cancellationToken).ConfigureAwait(false),
                    ExportArea.Drivers => await ImportDriverAsync(values, updateExisting, cancellationToken)
                        .ConfigureAwait(false),
                    ExportArea.LicensePlates => await ImportLicensePlateAsync(values, updateExisting, cancellationToken)
                        .ConfigureAwait(false),
                    _ => throw new NotSupportedException($"Der Importbereich '{area}' wird nicht unterstuetzt.")
                };

                if (result)
                {
                    imported++;
                }
                else
                {
                    skipped++;
                }
            }
            catch (Exception exception)
            {
                failed++;
                issues.Add(new ImportIssue(rowNumber, string.Empty, exception.Message, true));
                _logger.LogWarning(exception, "Importfehler in Zeile {Row}", rowNumber);
                _db.ChangeTracker.Clear();
            }

            progress?.Report((int)((rowIndex + 1) * 100.0 / Math.Max(1, rows.Count)));
        }

        await _audit.WriteAsync(AuditAction.Imported, area.ToString(), null, Path.GetFileName(filePath),
            $"{imported} importiert, {skipped} uebersprungen, {failed} fehlerhaft", cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Import abgeschlossen: {Imported} importiert, {Skipped} uebersprungen, {Failed} fehlerhaft.",
            imported, skipped, failed);

        return new ImportResult(imported, skipped, failed, issues);
    }

    private async Task<bool> ImportVehicleAsync(
        Dictionary<string, string?> values,
        bool updateExisting,
        int defaultStatusId,
        Dictionary<string, int> categories,
        CancellationToken cancellationToken)
    {
        var internalNumber = values.GetValueOrDefault("InternalNumber")?.Trim();
        if (string.IsNullOrWhiteSpace(internalNumber))
        {
            return false;
        }

        var existing = await _db.Vehicles
            .FirstOrDefaultAsync(v => v.InternalNumber == internalNumber, cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null && !updateExisting)
        {
            return false;
        }

        var vehicle = existing ?? new Vehicle
        {
            InternalNumber = internalNumber,
            VehicleStatusId = defaultStatusId
        };

        vehicle.LicensePlate = Coalesce(LicensePlateFormatter.Normalize(values.GetValueOrDefault("LicensePlate")), vehicle.LicensePlate);
        vehicle.Vin = Coalesce(values.GetValueOrDefault("Vin"), vehicle.Vin);
        vehicle.Manufacturer = Coalesce(values.GetValueOrDefault("Manufacturer"), vehicle.Manufacturer) ?? string.Empty;
        vehicle.Model = Coalesce(values.GetValueOrDefault("Model"), vehicle.Model) ?? string.Empty;
        vehicle.Variant = Coalesce(values.GetValueOrDefault("Variant"), vehicle.Variant);
        vehicle.BuildYear = ParseInt(values.GetValueOrDefault("BuildYear")) ?? vehicle.BuildYear;
        vehicle.FirstRegistration = ParseDate(values.GetValueOrDefault("FirstRegistration")) ?? vehicle.FirstRegistration;
        vehicle.FuelType = ParseEnum<FuelType>(values.GetValueOrDefault("FuelType")) ?? vehicle.FuelType;
        vehicle.Transmission = ParseEnum<TransmissionType>(values.GetValueOrDefault("Transmission")) ?? vehicle.Transmission;
        vehicle.PowerKw = ParseInt(values.GetValueOrDefault("PowerKw")) ?? vehicle.PowerKw;
        vehicle.Color = Coalesce(values.GetValueOrDefault("Color"), vehicle.Color);
        vehicle.Seats = ParseInt(values.GetValueOrDefault("Seats")) ?? vehicle.Seats;
        vehicle.Hsn = Coalesce(values.GetValueOrDefault("Hsn"), vehicle.Hsn);
        vehicle.Tsn = Coalesce(values.GetValueOrDefault("Tsn"), vehicle.Tsn);
        vehicle.Comment = Coalesce(values.GetValueOrDefault("Comment"), vehicle.Comment);

        var mileage = ParseInt(values.GetValueOrDefault("CurrentMileage"));
        if (mileage is { } km && km > vehicle.CurrentMileage)
        {
            vehicle.CurrentMileage = km;
            vehicle.CurrentMileageAt = _clock.Now;
        }

        if (existing is null)
        {
            _db.Vehicles.Add(vehicle);
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (mileage is { } value && value > 0)
        {
            var hasEntry = await _db.MileageEntries
                .AnyAsync(m => m.VehicleId == vehicle.Id && m.Mileage == value, cancellationToken)
                .ConfigureAwait(false);

            if (!hasEntry)
            {
                _db.MileageEntries.Add(new MileageEntry
                {
                    VehicleId = vehicle.Id,
                    Mileage = value,
                    RecordedAt = _clock.Now,
                    Source = MileageSource.Import,
                    RecordedByUserId = _currentUser.User?.Id,
                    RecordedByUserName = _currentUser.User?.DisplayName,
                    Comment = "Datenimport"
                });
            }
        }

        var categoryName = values.GetValueOrDefault("Category")?.Trim();
        if (!string.IsNullOrWhiteSpace(categoryName) && categories.TryGetValue(categoryName, out var categoryId))
        {
            var assigned = await _db.VehicleCategoryAssignments
                .AnyAsync(a => a.VehicleId == vehicle.Id && a.VehicleCategoryId == categoryId, cancellationToken)
                .ConfigureAwait(false);

            if (!assigned)
            {
                _db.VehicleCategoryAssignments.Add(new VehicleCategoryAssignment
                {
                    VehicleId = vehicle.Id,
                    VehicleCategoryId = categoryId,
                    IsPrimary = true,
                    AssignedAt = _clock.Now,
                    AssignedByUserId = _currentUser.User?.Id
                });
            }
        }

        if (existing is null)
        {
            _db.VehicleStatusHistory.Add(new VehicleStatusHistory
            {
                VehicleId = vehicle.Id,
                NewStatusId = vehicle.VehicleStatusId,
                ChangedAt = _clock.Now,
                ChangedByUserId = _currentUser.User?.Id,
                ChangedByUserName = _currentUser.User?.DisplayName,
                Reason = "Datenimport"
            });
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    private async Task<bool> ImportDriverAsync(
        Dictionary<string, string?> values,
        bool updateExisting,
        CancellationToken cancellationToken)
    {
        var lastName = values.GetValueOrDefault("LastName")?.Trim();
        if (string.IsNullOrWhiteSpace(lastName))
        {
            return false;
        }

        var personnelNumber = values.GetValueOrDefault("PersonnelNumber")?.Trim();

        Driver? existing = null;
        if (!string.IsNullOrWhiteSpace(personnelNumber))
        {
            existing = await _db.Drivers
                .FirstOrDefaultAsync(d => d.PersonnelNumber == personnelNumber, cancellationToken)
                .ConfigureAwait(false);
        }

        if (existing is not null && !updateExisting)
        {
            return false;
        }

        var driver = existing ?? new Driver { PersonnelNumber = personnelNumber };

        driver.FirstName = Coalesce(values.GetValueOrDefault("FirstName"), driver.FirstName) ?? string.Empty;
        driver.LastName = lastName;
        driver.Phone = Coalesce(values.GetValueOrDefault("Phone"), driver.Phone);
        driver.Mobile = Coalesce(values.GetValueOrDefault("Mobile"), driver.Mobile);
        driver.Email = Coalesce(values.GetValueOrDefault("Email"), driver.Email);
        driver.Comment = Coalesce(values.GetValueOrDefault("Comment"), driver.Comment);

        if (existing is null)
        {
            _db.Drivers.Add(driver);
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    private async Task<bool> ImportLicensePlateAsync(
        Dictionary<string, string?> values,
        bool updateExisting,
        CancellationToken cancellationToken)
    {
        var plate = LicensePlateFormatter.Normalize(values.GetValueOrDefault("Plate"));
        if (string.IsNullOrWhiteSpace(plate))
        {
            return false;
        }

        var existing = await _db.LicensePlates
            .FirstOrDefaultAsync(p => p.Plate == plate, cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null && !updateExisting)
        {
            return false;
        }

        var entity = existing ?? new LicensePlate { Plate = plate, Status = LicensePlateStatus.Verfuegbar };

        entity.RegistrationOffice = Coalesce(values.GetValueOrDefault("RegistrationOffice"), entity.RegistrationOffice);
        entity.Comment = Coalesce(values.GetValueOrDefault("Comment"), entity.Comment);

        if (existing is null)
        {
            _db.LicensePlates.Add(entity);
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    private async Task<HashSet<string>> LoadExistingKeysAsync(ExportArea area, CancellationToken cancellationToken)
    {
        return area switch
        {
            ExportArea.Vehicles => (await _db.Vehicles.Select(v => v.InternalNumber)
                .ToListAsync(cancellationToken).ConfigureAwait(false))
                .ToHashSet(StringComparer.OrdinalIgnoreCase),

            ExportArea.Drivers => (await _db.Drivers
                .Where(d => d.PersonnelNumber != null)
                .Select(d => d.PersonnelNumber!)
                .ToListAsync(cancellationToken).ConfigureAwait(false))
                .ToHashSet(StringComparer.OrdinalIgnoreCase),

            ExportArea.LicensePlates => (await _db.LicensePlates.Select(p => p.Plate)
                .ToListAsync(cancellationToken).ConfigureAwait(false))
                .ToHashSet(StringComparer.OrdinalIgnoreCase),

            _ => []
        };
    }

    private static Dictionary<string, string?> ReadRow(
        IReadOnlyList<string> columns,
        IReadOnlyList<string> row,
        IReadOnlyList<ImportColumnMapping> mapping)
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        foreach (var map in mapping)
        {
            if (string.IsNullOrWhiteSpace(map.TargetField))
            {
                continue;
            }

            var index = columns.ToList().FindIndex(c =>
                string.Equals(c, map.SourceColumn, StringComparison.OrdinalIgnoreCase));

            if (index >= 0 && index < row.Count)
            {
                values[map.TargetField] = row[index];
            }
        }

        return values;
    }

    private static string? ValidateFieldValue(string field, string value) => field switch
    {
        "BuildYear" when ParseInt(value) is null => "Das Baujahr muss eine ganze Zahl sein.",
        "BuildYear" when ParseInt(value) is < 1900 or > 2100 => "Das Baujahr ist unplausibel.",
        "PowerKw" or "Seats" or "CurrentMileage" when ParseInt(value) is null =>
            "Der Wert muss eine ganze Zahl sein.",
        "CurrentMileage" when ParseInt(value) is < 0 => "Der Kilometerstand darf nicht negativ sein.",
        "FirstRegistration" when ParseDate(value) is null =>
            "Das Datum konnte nicht gelesen werden (erwartet: TT.MM.JJJJ).",
        "Plate" when !LicensePlateFormatter.IsPlausible(value) =>
            "Das Kennzeichen entspricht nicht dem erwarteten Format (z. B. FDS-AB 123).",
        _ => null
    };

    private static string? Coalesce(string? value, string? fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static int? ParseInt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var cleaned = value.Replace(".", string.Empty).Replace(" ", string.Empty).Replace("km", string.Empty,
            StringComparison.OrdinalIgnoreCase);

        return int.TryParse(cleaned, NumberStyles.Integer, German, out var result) ? result : null;
    }

    private static DateTime? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string[] formats = ["dd.MM.yyyy", "d.M.yyyy", "yyyy-MM-dd", "dd/MM/yyyy", "MM/yyyy", "MM.yyyy"];

        if (DateTime.TryParseExact(value.Trim(), formats, German, DateTimeStyles.None, out var exact))
        {
            return exact;
        }

        return DateTime.TryParse(value, German, DateTimeStyles.None, out var parsed) ? parsed : null;
    }

    private static TEnum? ParseEnum<TEnum>(string? value) where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Enum.TryParse<TEnum>(value.Replace(" ", string.Empty), ignoreCase: true, out var result)
            ? result
            : null;
    }

    private static string Normalize(string value) =>
        new(value.ToLowerInvariant()
            .Replace("ä", "a").Replace("ö", "o").Replace("ü", "u").Replace("ß", "ss")
            .Where(char.IsLetterOrDigit).ToArray());
}
