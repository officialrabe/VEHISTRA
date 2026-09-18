using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fuhrpark.Application.Abstractions;
using Fuhrpark.Application.Dtos;
using Fuhrpark.Client.Services;
using Fuhrpark.Client.ViewModels.Dialogs;
using Fuhrpark.Domain.Entities;
using Fuhrpark.Domain.Enums;
using Fuhrpark.Domain.Security;
using Microsoft.Extensions.DependencyInjection;

namespace Fuhrpark.Client.ViewModels;

/// <summary>Werkstattuebersicht inklusive Werkstattbericht und Rueckmeldung.</summary>
public sealed partial class WorkshopViewModel : ViewModelBase
{
    private readonly IWorkshopService _workshop;
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
    private string _searchText = string.Empty;

    public WorkshopViewModel(
        IWorkshopService workshop,
        IExportService export,
        INavigationService navigation,
        ICurrentUserService currentUser,
        IDialogService dialogs,
        IReportGenerator reports,
        IServiceProvider services)
    {
        _workshop = workshop;
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

    public bool CanManage => _currentUser.HasPermission(Permissions.WorkshopManage);

    public bool CanPrint => _currentUser.HasPermission(Permissions.ReportsPrint);

    public bool CanExport => _currentUser.HasPermission(Permissions.DataExport);

    public override async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        await RunAsync(async () =>
        {
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
            return;
        }

        await RunAsync(
            async () => await _reports.CreateWorkshopReportForOrderAsync(SelectedOrder.Id).ConfigureAwait(true),
            "Der Werkstattbericht wurde erstellt.").ConfigureAwait(true);
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
}
