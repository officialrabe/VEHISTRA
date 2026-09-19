using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vehistra.Application.Abstractions;
using Vehistra.Application.Dtos;
using Vehistra.Application.Services;
using Vehistra.Client.Services;
using Vehistra.Client.ViewModels.Dialogs;
using Vehistra.Domain.Entities;
using Vehistra.Domain.Enums;
using Vehistra.Domain.Security;
using Microsoft.Extensions.DependencyInjection;

namespace Vehistra.Client.ViewModels;

/// <summary>Werkstattuebersicht inklusive Werkstattbericht und Rueckmeldung.</summary>
public sealed partial class WorkshopViewModel : ViewModelBase, IAcceptsPreset
{
    private readonly IWorkshopService _workshop;
    private readonly ISettingsService _settings;
    private readonly IExportService _export;
    private readonly INavigationService _navigation;
    private readonly ICurrentUserService _currentUser;
    private readonly IDialogService _dialogs;
    private readonly IReportGenerator _reports;
    private readonly IServiceProvider _services;

    [ObservableProperty]
    private WorkshopOrderListItem? _selectedOrder;

    [ObservableProperty]
    private Workshop? _workshopFilter;

    [ObservableProperty]
    private WorkshopOrderStatus? _statusFilter;

    [ObservableProperty]
    private bool _onlyOpen = true;

    [ObservableProperty]
    private bool _onlyInWorkshop;

    [ObservableProperty]
    private bool _onlyLongStay;

    [ObservableProperty]
    private int _longStayDays = 7;

    [ObservableProperty]
    private string _searchText = string.Empty;

    public WorkshopViewModel(
        IWorkshopService workshop,
        ISettingsService settings,
        IExportService export,
        INavigationService navigation,
        ICurrentUserService currentUser,
        IDialogService dialogs,
        IReportGenerator reports,
        IServiceProvider services)
    {
        _workshop = workshop;
        _settings = settings;
        _export = export;
        _navigation = navigation;
        _currentUser = currentUser;
        _dialogs = dialogs;
        _reports = reports;
        _services = services;
    }

    public override string Title => "Werkstatt";

    public override string? Subtitle =>
        $"{Orders.Count} Vorgänge · {Orders.Count(o => o.DaysInWorkshop is not null)} Fahrzeuge abgegeben";

    public ObservableCollection<WorkshopOrderListItem> Orders { get; } = [];

    public ObservableCollection<Workshop> Workshops { get; } = [];

    public IReadOnlyList<WorkshopOrderStatus> StatusValues { get; } = Enum.GetValues<WorkshopOrderStatus>();

    /// <summary>Beschriftung des Langzeitfilters - die Frist steht in den Einstellungen.</summary>
    public string LongStayFilterLabel => $"nur ab {LongStayDays} Tagen in der Werkstatt";

    public bool CanManage => _currentUser.HasPermission(Permissions.WorkshopManage);

    public bool CanPrint => _currentUser.HasPermission(Permissions.ReportsPrint);

    public bool CanExport => _currentUser.HasPermission(Permissions.DataExport);

    public override async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        await RunAsync(async () =>
        {
            // Dieselbe Frist wie im Ueberblick - sonst zeigte ein Klick auf die
            // Zahl dort eine andere Menge als die Liste hier.
            LongStayDays = Math.Max(1, await _settings
                .GetIntAsync(SettingsKeys.WorkshopLongStayWarnDays, 7, cancellationToken).ConfigureAwait(true));

            if (Workshops.Count == 0)
            {
                foreach (var workshop in await _workshop.GetWorkshopsAsync(true, cancellationToken).ConfigureAwait(true))
                {
                    Workshops.Add(workshop);
                }
            }

            var filter = new WorkshopFilter
            {
                WorkshopId = WorkshopFilter?.Id,
                Status = StatusFilter,
                OnlyOpen = OnlyOpen,
                OnlyInWorkshop = OnlyInWorkshop,
                MinDaysInWorkshop = OnlyLongStay ? LongStayDays : null,
                SearchText = SearchText
            };

            var orders = await _workshop.GetOrdersAsync(filter, cancellationToken).ConfigureAwait(true);

            Orders.Clear();
            foreach (var order in orders)
            {
                Orders.Add(order);
            }

            OnPropertyChanged(nameof(Subtitle));
        }).ConfigureAwait(true);
    }

    partial void OnWorkshopFilterChanged(Workshop? value) => _ = LoadAsync();

    partial void OnStatusFilterChanged(WorkshopOrderStatus? value) => _ = LoadAsync();

    partial void OnOnlyOpenChanged(bool value) => _ = LoadAsync();

    partial void OnOnlyInWorkshopChanged(bool value) => _ = LoadAsync();

    partial void OnOnlyLongStayChanged(bool value) => _ = LoadAsync();

    partial void OnLongStayDaysChanged(int value) => OnPropertyChanged(nameof(LongStayFilterLabel));

    partial void OnSearchTextChanged(string value) => _ = LoadAsync();

    [RelayCommand]
    private async Task CreateAsync()
    {
        var dialog = _services.GetRequiredService<WorkshopOrderEditViewModel>();
        await dialog.InitializeForNewAsync(null).ConfigureAwait(true);

        if (_dialogs.ShowDialog(dialog) == true)
        {
            await LoadAsync().ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private async Task EditAsync(WorkshopOrderListItem? item)
    {
        var target = item ?? SelectedOrder;

        if (target is null)
        {
            return;
        }

        var dialog = _services.GetRequiredService<WorkshopOrderEditViewModel>();
        await dialog.InitializeForEditAsync(target.Id).ConfigureAwait(true);

        if (_dialogs.ShowDialog(dialog) == true)
        {
            await LoadAsync().ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private async Task PrintReportAsync()
    {
        if (SelectedOrder is null)
        {
            // Stilles Abbrechen sieht aus wie eine kaputte Schaltflaeche.
            _dialogs.ShowInformation(
                "Bitte wählen Sie zuerst einen Werkstattvorgang in der Liste aus." + Environment.NewLine +
                Environment.NewLine +
                "Ein leeres Formular zum Ausfüllen von Hand erhalten Sie über „Blankoformular drucken“.",
                "Werkstattbericht");
            return;
        }

        var vorgang = SelectedOrder;

        await ErzeugeBerichtAsync(
            () => _reports.CreateWorkshopReportForOrderAsync(vorgang.Id),
            "Werkstattbericht").ConfigureAwait(true);
    }

    /// <summary>
    /// Leeres Werkstattformular zum Ausfuellen von Hand - dort, wo man es
    /// sucht. Bisher gab es das nur unter "Berichte &amp; Formulare".
    /// </summary>
    [RelayCommand]
    private async Task PrintBlankReportAsync() =>
        await ErzeugeBerichtAsync(
            () => _reports.CreateBlankWorkshopReportAsync(),
            "Blanko-Werkstattbericht").ConfigureAwait(true);

    /// <summary>Erzeugt den Bericht und sagt, was passiert ist - Pfad oder Fehler.</summary>
    private async Task ErzeugeBerichtAsync(Func<Task<string>> erzeugen, string bezeichnung)
    {
        if (!CanPrint)
        {
            _dialogs.ShowInformation(
                "Für das Erstellen von Berichten fehlt die Berechtigung „Berichte drucken“.", bezeichnung);
            return;
        }

        string? pfad = null;

        var erfolgreich = await RunAsync(async () =>
        {
            pfad = await erzeugen().ConfigureAwait(true);
        }).ConfigureAwait(true);

        if (erfolgreich)
        {
            StatusMessage = $"{bezeichnung} erstellt: {pfad}";
            return;
        }

        _dialogs.ShowError(ErrorMessage ?? "Der Bericht konnte nicht erstellt werden.", null, bezeichnung);
    }

    [RelayCommand]
    private async Task ChangeStatusAsync(WorkshopOrderStatus status)
    {
        if (SelectedOrder is null)
        {
            return;
        }

        await RunAsync(async () =>
        {
            await _workshop.ChangeStatusAsync(SelectedOrder.Id, status).ConfigureAwait(true);
            await LoadAsync().ConfigureAwait(true);
        }, $"Der Vorgang wurde auf „{status}“ gesetzt.").ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task OpenVehicleAsync()
    {
        if (SelectedOrder is not null)
        {
            await _navigation.OpenVehicleAsync(SelectedOrder.VehicleId, "workshop").ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private async Task ExportAsync(string? format)
    {
        var exportFormat = format switch
        {
            "xlsx" => ExportFormat.Xlsx,
            "pdf" => ExportFormat.Pdf,
            _ => ExportFormat.Csv
        };

        var extension = exportFormat.ToString().ToLowerInvariant();
        var target = _dialogs.SaveFile($"{exportFormat} (*.{extension})|*.{extension}",
            $"Werkstatt_{DateTime.Now:yyyyMMdd}.{extension}", "Werkstattliste exportieren");

        if (string.IsNullOrWhiteSpace(target))
        {
            return;
        }

        await RunAsync(
            async () => await _export.ExportAsync(ExportArea.WorkshopOrders, exportFormat, target).ConfigureAwait(true),
            $"Die Werkstattliste wurde exportiert: {target}").ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync().ConfigureAwait(true);

    /// <inheritdoc />
    public void ApplyPreset(ListPreset preset)
    {
        switch (preset)
        {
            case ListPreset.WerkstattOffen:
            case ListPreset.WerkstattUeberfaellig:
                OnlyOpen = true;
                break;

            case ListPreset.WerkstattHeute:
                OnlyInWorkshop = true;
                break;

            case ListPreset.WerkstattLangzeit:
                OnlyInWorkshop = true;
                OnlyLongStay = true;
                break;
        }
    }
}
