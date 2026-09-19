using Vehistra.Application.Abstractions;
using Vehistra.Reporting.Documents;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace Vehistra.Reporting;

/// <summary>
/// Erzeugt alle PDF-Berichte. Die Ausgabe ist vektorbasiert und fuer DIN A4 ausgelegt;
/// es werden keine Bildschirmabbilder verwendet.
/// </summary>
public sealed class QuestPdfReportService : IReportService
{
    static QuestPdfReportService()
    {
        // QuestPDF Community-Lizenz (kostenfrei fuer Unternehmen unterhalb der Umsatzgrenze).
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.ThrowOnMissingTextGlyphs = false;
    }

    public Task<byte[]> CreateWorkshopReportAsync(
        WorkshopReportData data,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => new WorkshopReportDocument(data).GeneratePdf(), cancellationToken);

    public Task<byte[]> CreateAccidentReportAsync(
        AccidentReportData data,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => new AccidentReportDocument(data).GeneratePdf(), cancellationToken);

    public Task<byte[]> CreateVehicleFileReportAsync(
        VehicleFileReportData data,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => new VehicleFileDocument(data).GeneratePdf(), cancellationToken);

    public Task<byte[]> CreateTableReportAsync(
        TableReportData data,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => new TableReportDocument(data).GeneratePdf(), cancellationToken);
}
