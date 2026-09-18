namespace Vehistra.Application.Dtos;

/// <summary>Vorschau einer Importdatei mit erkannten Spalten.</summary>
public sealed class ImportPreview
{
    public string FilePath { get; init; } = string.Empty;

    public IReadOnlyList<string> Columns { get; init; } = [];

    public IReadOnlyList<IReadOnlyList<string>> SampleRows { get; init; } = [];

    public int TotalRows { get; init; }
}

/// <summary>Zuordnung einer Dateispalte zu einem Zielfeld.</summary>
public sealed class ImportColumnMapping
{
    public string SourceColumn { get; set; } = string.Empty;

    public string? TargetField { get; set; }
}

/// <summary>Meldung aus der Importvalidierung.</summary>
public sealed record ImportIssue(int RowNumber, string Column, string Message, bool IsError);

/// <summary>Ergebnis einer Importvalidierung.</summary>
public sealed class ImportValidationResult
{
    public IReadOnlyList<ImportIssue> Issues { get; init; } = [];

    public int ValidRows { get; init; }

    public int InvalidRows { get; init; }

    public int NewRecords { get; init; }

    public int ExistingRecords { get; init; }

    public bool HasErrors => Issues.Any(i => i.IsError);
}

/// <summary>Ergebnis eines Importlaufs.</summary>
public sealed record ImportResult(int Imported, int Skipped, int Failed, IReadOnlyList<ImportIssue> Issues);

/// <summary>Auswahl des Exportbereichs.</summary>
public enum ExportArea
{
    Vehicles = 1,
    Drivers = 2,
    Inspections = 3,
    Maintenance = 4,
    Damages = 5,
    Accidents = 6,
    WorkshopOrders = 7,
    LicensePlates = 8,
    RetiredVehicles = 9
}

/// <summary>Exportformat.</summary>
public enum ExportFormat
{
    Csv = 1,
    Xlsx = 2,
    Pdf = 3
}
