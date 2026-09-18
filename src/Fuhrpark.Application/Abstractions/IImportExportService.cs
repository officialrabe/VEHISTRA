using Fuhrpark.Application.Dtos;

namespace Fuhrpark.Application.Abstractions;

/// <summary>Import-Assistent fuer CSV- und XLSX-Dateien.</summary>
public interface IImportService
{
    /// <summary>Liest Kopfzeile und Beispielzeilen einer Datei.</summary>
    Task<ImportPreview> PreviewAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>Verfuegbare Zielfelder fuer einen Importbereich.</summary>
    IReadOnlyList<ImportTargetField> GetTargetFields(ExportArea area);

    /// <summary>Schlaegt anhand der Spaltennamen eine Zuordnung vor.</summary>
    IReadOnlyList<ImportColumnMapping> SuggestMapping(ExportArea area, IReadOnlyList<string> columns);

    Task<ImportValidationResult> ValidateAsync(
        string filePath,
        ExportArea area,
        IReadOnlyList<ImportColumnMapping> mapping,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fuehrt den Import aus. Bestehende Datensaetze werden nur aktualisiert,
    /// wenn dies ausdruecklich gewuenscht ist - niemals stillschweigend.
    /// </summary>
    Task<ImportResult> ImportAsync(
        string filePath,
        ExportArea area,
        IReadOnlyList<ImportColumnMapping> mapping,
        bool updateExisting,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default);
}

/// <summary>Beschreibung eines Importzielfelds.</summary>
public sealed record ImportTargetField(string Name, string DisplayName, bool IsRequired, bool IsKey);

/// <summary>Export von Listen nach CSV, XLSX und PDF.</summary>
public interface IExportService
{
    Task<string> ExportAsync(
        ExportArea area,
        ExportFormat format,
        string targetPath,
        CancellationToken cancellationToken = default);

    /// <summary>Exportiert eine beliebige Tabelle (z. B. die aktuell gefilterte Ansicht).</summary>
    Task<string> ExportTableAsync(
        string title,
        IReadOnlyList<string> columns,
        IReadOnlyList<IReadOnlyList<string?>> rows,
        ExportFormat format,
        string targetPath,
        CancellationToken cancellationToken = default);
}
