using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vehistra.Application.Abstractions;
using Vehistra.Application.Common;
using Vehistra.Application.Dtos;
using Vehistra.Client.Services;
using Vehistra.Domain.Security;

namespace Vehistra.Client.ViewModels;

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

    /// <summary>
    /// Format der Listenexporte. Voreinstellung ist Excel, weil Listen meist
    /// weiterverarbeitet werden; PDF bleibt einen Klick entfernt.
    /// </summary>
    [ObservableProperty]
    private ExportFormat _listFormat = ExportFormat.Xlsx;

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

    // Die drei Schalter fuer das Format. Eigene Eigenschaften, damit die
    // Auswahlknoepfe ohne Umwandler auskommen.
    public bool FormatExcel
    {
        get => ListFormat == ExportFormat.Xlsx;
        set
        {
            if (value)
            {
                ListFormat = ExportFormat.Xlsx;
            }
        }
    }

    public bool FormatCsv
    {
        get => ListFormat == ExportFormat.Csv;
        set
        {
            if (value)
            {
                ListFormat = ExportFormat.Csv;
            }
        }
    }

    public bool FormatPdf
    {
        get => ListFormat == ExportFormat.Pdf;
        set
        {
            if (value)
            {
                ListFormat = ExportFormat.Pdf;
            }
        }
    }

    /// <summary>Beschreibt die aktuelle Auswahl im Klartext.</summary>
    public string ListFormatDescription => ListFormat switch
    {
        ExportFormat.Xlsx => "Excel-Arbeitsmappe (*.xlsx) – zum Weiterrechnen",
        ExportFormat.Csv => "Textdatei mit Semikolon (*.csv) – für andere Programme",
        _ => "PDF im Querformat (*.pdf) – zum Ausdrucken und Ablegen"
    };

    partial void OnListFormatChanged(ExportFormat value)
    {
        OnPropertyChanged(nameof(FormatExcel));
        OnPropertyChanged(nameof(FormatCsv));
        OnPropertyChanged(nameof(FormatPdf));
        OnPropertyChanged(nameof(ListFormatDescription));
    }

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

        _currentUser.DemandPermission(Permissions.DataExport);

        var (endung, filter, bezeichnung) = ListFormat switch
        {
            ExportFormat.Xlsx => ("xlsx", "Excel-Arbeitsmappe (*.xlsx)|*.xlsx", "Excel"),
            ExportFormat.Csv => ("csv", "CSV-Datei (*.csv)|*.csv", "CSV"),
            _ => ("pdf", "PDF-Dokument (*.pdf)|*.pdf", "PDF")
        };

        var target = _dialogs.SaveFile(filter,
            $"{Bezeichnung(exportArea)}_{DateTime.Now:yyyyMMdd}.{endung}",
            $"Liste als {bezeichnung} exportieren");

        if (string.IsNullOrWhiteSpace(target))
        {
            return;
        }

        await RunAsync(async () =>
        {
            await _export.ExportAsync(exportArea, ListFormat, target).ConfigureAwait(true);
            _dialogs.OpenInShell(target);
        }, $"Die Liste wurde als {bezeichnung} erstellt: {target}").ConfigureAwait(true);
    }

    /// <summary>Deutscher Dateiname statt des englischen Enum-Namens.</summary>
    private static string Bezeichnung(ExportArea area) => area switch
    {
        ExportArea.Vehicles => "Fahrzeuge",
        ExportArea.Drivers => "Fahrer",
        ExportArea.Inspections => "TUEV",
        ExportArea.Maintenance => "Wartung",
        ExportArea.Damages => "Schaeden",
        ExportArea.Accidents => "Unfaelle",
        ExportArea.WorkshopOrders => "Werkstatt",
        ExportArea.LicensePlates => "Kennzeichen",
        ExportArea.RetiredVehicles => "Ausgemustert",
        ExportArea.DriverAssignments => "Fahrerzuordnungen",
        _ => area.ToString()
    };
}
