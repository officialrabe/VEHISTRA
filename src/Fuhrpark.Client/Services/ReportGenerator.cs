using Fuhrpark.Application.Abstractions;

namespace Fuhrpark.Client.Services;

/// <summary>
/// Erzeugt PDF-Berichte, zeigt die Druckvorschau an und legt sie auf Wunsch
/// in der Fahrzeugakte ab.
/// </summary>
public interface IReportGenerator
{
    Task CreateBlankWorkshopReportAsync(CancellationToken cancellationToken = default);

    Task CreateWorkshopReportForVehicleAsync(int vehicleId, CancellationToken cancellationToken = default);

    Task CreateWorkshopReportForOrderAsync(int workshopOrderId, CancellationToken cancellationToken = default);

    Task CreateBlankAccidentReportAsync(CancellationToken cancellationToken = default);

    Task CreateAccidentReportForVehicleAsync(int vehicleId, CancellationToken cancellationToken = default);

    Task CreateAccidentReportForAccidentAsync(int accidentId, CancellationToken cancellationToken = default);

    Task CreateVehicleFileAsync(int vehicleId, CancellationToken cancellationToken = default);
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

    public async Task CreateBlankWorkshopReportAsync(CancellationToken cancellationToken = default)
    {
        var report = await _builder.BuildWorkshopReportAsync(cancellationToken: cancellationToken).ConfigureAwait(true);
        await PresentAsync(report, cancellationToken).ConfigureAwait(true);
    }

    public async Task CreateWorkshopReportForVehicleAsync(int vehicleId, CancellationToken cancellationToken = default)
    {
        var report = await _builder.BuildWorkshopReportAsync(vehicleId, cancellationToken: cancellationToken)
            .ConfigureAwait(true);
        await PresentAsync(report, cancellationToken).ConfigureAwait(true);
    }

    public async Task CreateWorkshopReportForOrderAsync(int workshopOrderId, CancellationToken cancellationToken = default)
    {
        var report = await _builder.BuildWorkshopReportAsync(null, workshopOrderId, cancellationToken)
            .ConfigureAwait(true);
        await PresentAsync(report, cancellationToken).ConfigureAwait(true);
    }

    public async Task CreateBlankAccidentReportAsync(CancellationToken cancellationToken = default)
    {
        var report = await _builder.BuildAccidentReportAsync(cancellationToken: cancellationToken).ConfigureAwait(true);
        await PresentAsync(report, cancellationToken).ConfigureAwait(true);
    }

    public async Task CreateAccidentReportForVehicleAsync(int vehicleId, CancellationToken cancellationToken = default)
    {
        var report = await _builder.BuildAccidentReportAsync(vehicleId, cancellationToken: cancellationToken)
            .ConfigureAwait(true);
        await PresentAsync(report, cancellationToken).ConfigureAwait(true);
    }

    public async Task CreateAccidentReportForAccidentAsync(int accidentId, CancellationToken cancellationToken = default)
    {
        var report = await _builder.BuildAccidentReportAsync(null, accidentId, cancellationToken).ConfigureAwait(true);
        await PresentAsync(report, cancellationToken).ConfigureAwait(true);
    }

    public async Task CreateVehicleFileAsync(int vehicleId, CancellationToken cancellationToken = default)
    {
        var report = await _builder.BuildVehicleFileAsync(vehicleId, cancellationToken).ConfigureAwait(true);
        await PresentAsync(report, cancellationToken).ConfigureAwait(true);
    }

    /// <summary>Zeigt die Druckvorschau und bietet die Ablage in der Fahrzeugakte an.</summary>
    private async Task PresentAsync(GeneratedReport report, CancellationToken cancellationToken)
    {
        await _launcher.PreviewAsync(report.Content, report.SuggestedFileName, cancellationToken).ConfigureAwait(true);

        if (report.IsBlankForm || report.VehicleId is null)
        {
            return;
        }

        if (!_dialogs.Confirm(
                $"Soll der Bericht „{report.Title}“ zusätzlich in der Fahrzeugakte abgelegt werden?",
                "Bericht ablegen"))
        {
            return;
        }

        await _builder.ArchiveAsync(report, report.Content, cancellationToken).ConfigureAwait(true);

        _dialogs.ShowInformation(
            "Der Bericht wurde in der Dokumentenablage gespeichert und mit dem Fahrzeug verknüpft.",
            "Bericht abgelegt");
    }
}
