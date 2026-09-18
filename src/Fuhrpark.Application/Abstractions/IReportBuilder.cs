namespace Fuhrpark.Application.Abstractions;

/// <summary>
/// Stellt die Daten fuer die PDF-Berichte aus der Datenbank zusammen und erzeugt das Dokument.
/// Blankoformulare entstehen, indem keine Datensatzkennung uebergeben wird.
/// </summary>
public interface IReportBuilder
{
    /// <summary>Werkstattbericht - blanko, fuer ein Fahrzeug oder fuer einen Werkstattvorgang.</summary>
    Task<GeneratedReport> BuildWorkshopReportAsync(
        int? vehicleId = null,
        int? workshopOrderId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Unfallbericht - blanko, fuer ein Fahrzeug oder fuer einen erfassten Unfall.</summary>
    Task<GeneratedReport> BuildAccidentReportAsync(
        int? vehicleId = null,
        int? accidentId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Fahrzeugakte als PDF.</summary>
    Task<GeneratedReport> BuildVehicleFileAsync(int vehicleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Legt einen erzeugten Bericht in der Dokumentenablage ab und verknuepft ihn
    /// mit Fahrzeug, Werkstattvorgang oder Unfall.
    /// </summary>
    Task<int> ArchiveAsync(
        GeneratedReport report,
        byte[] content,
        CancellationToken cancellationToken = default);
}

/// <summary>Ergebnis einer Berichtserzeugung.</summary>
public sealed record GeneratedReport(
    byte[] Content,
    string TemplateKey,
    string Title,
    string SuggestedFileName,
    int? VehicleId,
    int? WorkshopOrderId,
    int? AccidentReportId,
    bool IsBlankForm);
