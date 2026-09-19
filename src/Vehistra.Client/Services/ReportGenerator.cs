using Vehistra.Application.Abstractions;

namespace Vehistra.Client.Services;

/// <summary>
/// Erzeugt PDF-Berichte, zeigt die Druckvorschau an und legt sie auf Wunsch
/// in der Fahrzeugakte ab.
/// </summary>
public interface IReportGenerator
{
    /// <summary>Erzeugt den Bericht und gibt den Pfad der abgelegten PDF-Datei zurueck.</summary>
    Task<string> CreateBlankWorkshopReportAsync(CancellationToken cancellationToken = default);

    Task<string> CreateWorkshopReportForVehicleAsync(int vehicleId, CancellationToken cancellationToken = default);

    Task<string> CreateWorkshopReportForOrderAsync(int workshopOrderId, CancellationToken cancellationToken = default);

    Task<string> CreateBlankAccidentReportAsync(CancellationToken cancellationToken = default);

    Task<string> CreateAccidentReportForVehicleAsync(int vehicleId, CancellationToken cancellationToken = default);

    Task<string> CreateAccidentReportForAccidentAsync(int accidentId, CancellationToken cancellationToken = default);

    Task<string> CreateVehicleFileAsync(int vehicleId, CancellationToken cancellationToken = default);
}

/// <inheritdoc />
public sealed class ReportGenerator : IReportGenerator
{
    private readonly IReportBuilder _builder;
    private readonly IReportLauncher _launcher;
    private readonly IDialogService _dialogs;

    public ReportGenerator(IReportBuilder builder, IReportLauncher launcher, IDialogService dialogs)
    {
        _builder = builder;
        _launcher = launcher;
        _dialogs = dialogs;
    }

    public async Task<string> CreateBlankWorkshopReportAsync(CancellationToken cancellationToken = default)
    {
        var report = await _builder.BuildWorkshopReportAsync(cancellationToken: cancellationToken).ConfigureAwait(true);
        return await PresentAsync(report, cancellationToken).ConfigureAwait(true);
    }

    public async Task<string> CreateWorkshopReportForVehicleAsync(int vehicleId, CancellationToken cancellationToken = default)
    {
        var report = await _builder.BuildWorkshopReportAsync(vehicleId, cancellationToken: cancellationToken)
            .ConfigureAwait(true);
        return await PresentAsync(report, cancellationToken).ConfigureAwait(true);
    }

    public async Task<string> CreateWorkshopReportForOrderAsync(int workshopOrderId, CancellationToken cancellationToken = default)
    {
        var report = await _builder.BuildWorkshopReportAsync(null, workshopOrderId, cancellationToken)
            .ConfigureAwait(true);
        return await PresentAsync(report, cancellationToken).ConfigureAwait(true);
    }

    public async Task<string> CreateBlankAccidentReportAsync(CancellationToken cancellationToken = default)
    {
        var report = await _builder.BuildAccidentReportAsync(cancellationToken: cancellationToken).ConfigureAwait(true);
        return await PresentAsync(report, cancellationToken).ConfigureAwait(true);
    }

    public async Task<string> CreateAccidentReportForVehicleAsync(int vehicleId, CancellationToken cancellationToken = default)
    {
        var report = await _builder.BuildAccidentReportAsync(vehicleId, cancellationToken: cancellationToken)
            .ConfigureAwait(true);
        return await PresentAsync(report, cancellationToken).ConfigureAwait(true);
    }

    public async Task<string> CreateAccidentReportForAccidentAsync(int accidentId, CancellationToken cancellationToken = default)
    {
        var report = await _builder.BuildAccidentReportAsync(null, accidentId, cancellationToken).ConfigureAwait(true);
        return await PresentAsync(report, cancellationToken).ConfigureAwait(true);
    }

    public async Task<string> CreateVehicleFileAsync(int vehicleId, CancellationToken cancellationToken = default)
    {
        var report = await _builder.BuildVehicleFileAsync(vehicleId, cancellationToken).ConfigureAwait(true);
        return await PresentAsync(report, cancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Zeigt die Druckvorschau, bietet die Ablage in der Fahrzeugakte an und
    /// gibt den Pfad der Datei zurueck - damit der Anwender sie auch dann
    /// findet, wenn auf diesem Rechner kein PDF-Betrachter aufgeht.
    /// </summary>
    private async Task<string> PresentAsync(GeneratedReport report, CancellationToken cancellationToken)
    {
        var pfad = await _launcher.PreviewAsync(report.Content, report.SuggestedFileName, cancellationToken)
            .ConfigureAwait(true);

        if (report.IsBlankForm || report.VehicleId is null)
        {
            return pfad;
        }

        if (!_dialogs.Confirm(
                $"Soll der Bericht „{report.Title}“ zusätzlich in der Fahrzeugakte abgelegt werden?",
                "Bericht ablegen"))
        {
            return pfad;
        }

        await _builder.ArchiveAsync(report, report.Content, cancellationToken).ConfigureAwait(true);

        _dialogs.ShowInformation(
            "Der Bericht wurde in der Dokumentenablage gespeichert und mit dem Fahrzeug verknüpft.",
            "Bericht abgelegt");

        return pfad;
    }
}
