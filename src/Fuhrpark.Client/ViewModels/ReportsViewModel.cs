using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fuhrpark.Application.Abstractions;
using Fuhrpark.Application.Common;
using Fuhrpark.Application.Dtos;
using Fuhrpark.Client.Services;
using Fuhrpark.Domain.Security;

namespace Fuhrpark.Client.ViewModels;

/// <summary>Zentraler Bereich fuer Berichte und Formulare.</summary>
public sealed partial class ReportsViewModel : ViewModelBase
{
    private readonly IReportGenerator _reports;
    private readonly IVehicleService _vehicles;
    private readonly IExportService _export;
    private readonly ICurrentUserService _currentUser;
    private readonly IDialogService _dialogs;

    [ObservableProperty]
    private VehicleListItem? _selectedVehicle;

    [ObservableProperty]
    private string _vehicleSearch = string.Empty;

    public ReportsViewModel(
        IReportGenerator reports,
        IVehicleService vehicles,
        IExportService export,
        ICurrentUserService currentUser,
        IDialogService dialogs)
    {
        _reports = reports;
        _vehicles = vehicles;
        _export = export;
        _currentUser = currentUser;
        _dialogs = dialogs;
    }

    public override string Title => "Berichte & Formulare";

    public override string? Subtitle =>
        "Alle Berichte werden als vektorbasierte DIN-A4-PDF-Dokumente erzeugt";

    public ObservableCollection<VehicleListItem> Vehicles { get; } = [];

    public bool CanPrint => _currentUser.HasPermission(Permissions.ReportsPrint);

    public bool CanExport => _currentUser.HasPermission(Permissions.DataExport);

    public override async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        await RunAsync(async () =>
        {
            var result = await _vehicles.GetListAsync(new VehicleFilter
            {
                SearchText = VehicleSearch,
                IsRetired = false,
                PageSize = 300
            }, cancellationToken).ConfigureAwait(true);

            Vehicles.Clear();
            foreach (var item in result.Items)
            {
                Vehicles.Add(item);
            }
        }).ConfigureAwait(true);
    }

    partial void OnVehicleSearchChanged(string value) => _ = LoadAsync();

    [RelayCommand]
    private async Task BlankWorkshopReportAsync() =>
        await RunAsync(async () => await _reports.CreateBlankWorkshopReportAsync().ConfigureAwait(true),
            "Der Blanko-Werkstattbericht wurde erstellt.").ConfigureAwait(true);

    [RelayCommand]
    private async Task WorkshopReportForVehicleAsync()
    {
        if (SelectedVehicle is null)
        {
            _dialogs.ShowInformation("Bitte wählen Sie zuerst ein Fahrzeug aus.", "Werkstattbericht");
            return;
        }

        await RunAsync(
            async () => await _reports.CreateWorkshopReportForVehicleAsync(SelectedVehicle.Id).ConfigureAwait(true),
            "Der Werkstattbericht wurde erstellt.").ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task BlankAccidentReportAsync() =>
        await RunAsync(async () => await _reports.CreateBlankAccidentReportAsync().ConfigureAwait(true),
            "Der Blanko-Unfallbericht wurde erstellt.").ConfigureAwait(true);

    [RelayCommand]
    private async Task AccidentReportForVehicleAsync()
    {
        if (SelectedVehicle is null)
        {
            _dialogs.ShowInformation("Bitte wählen Sie zuerst ein Fahrzeug aus.", "Unfallbericht");
            return;
        }

        await RunAsync(
            async () => await _reports.CreateAccidentReportForVehicleAsync(SelectedVehicle.Id).ConfigureAwait(true),
            "Der Unfallbericht wurde erstellt.").ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task VehicleFileAsync()
    {
        if (SelectedVehicle is null)
        {
            _dialogs.ShowInformation("Bitte wählen Sie zuerst ein Fahrzeug aus.", "Fahrzeugakte");
            return;
        }

        await RunAsync(
            async () => await _reports.CreateVehicleFileAsync(SelectedVehicle.Id).ConfigureAwait(true),
            "Die Fahrzeugakte wurde erstellt.").ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task ExportListAsync(string? area)
    {
        if (!Enum.TryParse<ExportArea>(area, out var exportArea))
        {
            return;
        }

        var target = _dialogs.SaveFile("PDF-Dokument (*.pdf)|*.pdf",
            $"{exportArea}_{DateTime.Now:yyyyMMdd}.pdf", "Liste als PDF exportieren");

        if (string.IsNullOrWhiteSpace(target))
        {
            return;
        }

        await RunAsync(async () =>
        {
            await _export.ExportAsync(exportArea, ExportFormat.Pdf, target).ConfigureAwait(true);
            _dialogs.OpenInShell(target);
        }, "Die Liste wurde erstellt.").ConfigureAwait(true);
    }
}
