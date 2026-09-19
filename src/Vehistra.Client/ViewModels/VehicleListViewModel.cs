using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vehistra.Application.Abstractions;
using Vehistra.Application.Dtos;
using Vehistra.Client.Services;
using Vehistra.Client.ViewModels.Dialogs;
using Vehistra.Domain.Entities;
using Vehistra.Domain.Security;
using Microsoft.Extensions.DependencyInjection;

namespace Vehistra.Client.ViewModels;

/// <summary>Fahrzeuguebersicht mit Suche, Filter, Sortierung und Export.</summary>
public sealed partial class VehicleListViewModel : ViewModelBase
{
    private readonly IVehicleService _vehicles;
    private readonly IExportService _export;
    private readonly INavigationService _navigation;
    private readonly ICurrentUserService _currentUser;
    private readonly IDialogService _dialogs;
    private readonly IServiceProvider _services;

    private CancellationTokenSource? _searchCancellation;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private VehicleStatus? _selectedStatus;

    [ObservableProperty]
    private VehicleCategory? _selectedCategory;

    [ObservableProperty]
    private VehicleListItem? _selectedVehicle;

    [ObservableProperty]
    private bool _onlyOpenDamages;

    [ObservableProperty]
    private bool _onlyInWorkshop;

    [ObservableProperty]
    private bool _onlyInspectionDue;

    [ObservableProperty]
    private bool _includeRetired;

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private int _pageNumber = 1;

    [ObservableProperty]
    private int _pageCount = 1;

    public VehicleListViewModel(
        IVehicleService vehicles,
        IExportService export,
        INavigationService navigation,
        ICurrentUserService currentUser,
        IDialogService dialogs,
        IServiceProvider services)
    {
        _vehicles = vehicles;
        _export = export;
        _navigation = navigation;
        _currentUser = currentUser;
        _dialogs = dialogs;
        _services = services;
    }

    public override string Title => "Fahrzeuge";

    public override string? Subtitle => $"{TotalCount} Fahrzeuge · Doppelklick öffnet die Fahrzeugakte";

    public ObservableCollection<VehicleListItem> Vehicles { get; } = [];

    public ObservableCollection<VehicleStatus> Statuses { get; } = [];

    public ObservableCollection<VehicleCategory> Categories { get; } = [];

    public bool CanCreate => _currentUser.HasPermission(Permissions.VehicleCreate);

    public bool CanEdit => _currentUser.HasPermission(Permissions.VehicleEdit);

    public bool CanExport => _currentUser.HasPermission(Permissions.DataExport);

    public bool CanImport => _currentUser.HasPermission(Permissions.DataImport);

    public bool CanEditMileage => _currentUser.HasPermission(Permissions.MileageEdit);

    public override async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        await RunAsync(async () =>
        {
            if (Statuses.Count == 0)
            {
                foreach (var status in await _vehicles.GetStatusesAsync(cancellationToken).ConfigureAwait(true))
                {
                    Statuses.Add(status);
                }

                foreach (var category in await _vehicles.GetCategoriesAsync(cancellationToken).ConfigureAwait(true))
                {
                    Categories.Add(category);
                }
            }

            await ReloadAsync(cancellationToken).ConfigureAwait(true);
        }).ConfigureAwait(true);
    }

    partial void OnSearchTextChanged(string value) => _ = DelayedReloadAsync();

    partial void OnSelectedStatusChanged(VehicleStatus? value) => _ = ReloadCommand.ExecuteAsync(null);

    partial void OnSelectedCategoryChanged(VehicleCategory? value) => _ = ReloadCommand.ExecuteAsync(null);

    partial void OnOnlyOpenDamagesChanged(bool value) => _ = ReloadCommand.ExecuteAsync(null);

    partial void OnOnlyInWorkshopChanged(bool value) => _ = ReloadCommand.ExecuteAsync(null);

    partial void OnOnlyInspectionDueChanged(bool value) => _ = ReloadCommand.ExecuteAsync(null);

    partial void OnIncludeRetiredChanged(bool value) => _ = ReloadCommand.ExecuteAsync(null);

    [RelayCommand]
    private async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        await RunAsync(async () =>
        {
            var filter = new VehicleFilter
            {
                SearchText = SearchText,
                StatusId = SelectedStatus?.Id,
                CategoryId = SelectedCategory?.Id,
                IsRetired = IncludeRetired ? null : false,
                OnlyWithOpenDamages = OnlyOpenDamages,
                OnlyInWorkshop = OnlyInWorkshop,
                OnlyInspectionDue = OnlyInspectionDue,
                PageNumber = PageNumber,
                PageSize = 200
            };

            var result = await _vehicles.GetListAsync(filter, cancellationToken).ConfigureAwait(true);

            Vehicles.Clear();
            foreach (var item in result.Items)
            {
                Vehicles.Add(item);
            }

            TotalCount = result.TotalCount;
            PageCount = Math.Max(1, result.PageCount);
            OnPropertyChanged(nameof(Subtitle));
        }).ConfigureAwait(true);
    }

    [RelayCommand]
    private void ResetFilter()
    {
        SearchText = string.Empty;
        SelectedStatus = null;
        SelectedCategory = null;
        OnlyOpenDamages = false;
        OnlyInWorkshop = false;
        OnlyInspectionDue = false;
        IncludeRetired = false;
        PageNumber = 1;
    }

    [RelayCommand]
    private async Task OpenAsync(VehicleListItem? item)
    {
        var vehicle = item ?? SelectedVehicle;

        if (vehicle is not null)
        {
            await _navigation.OpenVehicleAsync(vehicle.Id).ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private async Task CreateAsync()
    {
        _currentUser.DemandPermission(Permissions.VehicleCreate);

        var dialog = _services.GetRequiredService<VehicleEditViewModel>();
        await dialog.InitializeForNewAsync().ConfigureAwait(true);

        if (_dialogs.ShowDialog(dialog) == true)
        {
            await ReloadAsync().ConfigureAwait(true);

            if (dialog.SavedVehicleId is { } id)
            {
                await _navigation.OpenVehicleAsync(id).ConfigureAwait(true);
            }
        }
    }

    [RelayCommand]
    private async Task EditAsync()
    {
        if (SelectedVehicle is null)
        {
            return;
        }

        _currentUser.DemandPermission(Permissions.VehicleEdit);

        var dialog = _services.GetRequiredService<VehicleEditViewModel>();
        await dialog.InitializeForEditAsync(SelectedVehicle.Id).ConfigureAwait(true);

        if (_dialogs.ShowDialog(dialog) == true)
        {
            await ReloadAsync().ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private async Task ChangeStatusAsync()
    {
        if (SelectedVehicle is null)
        {
            return;
        }

        var dialog = _services.GetRequiredService<StatusChangeViewModel>();
        await dialog.InitializeAsync(SelectedVehicle.Id, SelectedVehicle.DisplayName).ConfigureAwait(true);

        if (_dialogs.ShowDialog(dialog) == true)
        {
            await ReloadAsync().ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private async Task AddMileageAsync()
    {
        if (SelectedVehicle is null)
        {
            return;
        }

        var dialog = _services.GetRequiredService<MileageEditViewModel>();
        await dialog.InitializeAsync(SelectedVehicle.Id, SelectedVehicle.DisplayName, SelectedVehicle.CurrentMileage)
            .ConfigureAwait(true);

        if (_dialogs.ShowDialog(dialog) == true)
        {
            await ReloadAsync().ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private async Task ExportAsync(string? format)
    {
        _currentUser.DemandPermission(Permissions.DataExport);

        var exportFormat = format switch
        {
            "xlsx" => ExportFormat.Xlsx,
            "pdf" => ExportFormat.Pdf,
            _ => ExportFormat.Csv
        };

        var extension = exportFormat.ToString().ToLowerInvariant();
        var target = _dialogs.SaveFile(
            $"{exportFormat} (*.{extension})|*.{extension}",
            $"Fahrzeuge_{DateTime.Now:yyyyMMdd}.{extension}",
            "Fahrzeugliste exportieren");

        if (string.IsNullOrWhiteSpace(target))
        {
            return;
        }

        await RunAsync(async () =>
        {
            await _export.ExportAsync(ExportArea.Vehicles, exportFormat, target).ConfigureAwait(true);
            StatusMessage = $"Die Fahrzeugliste wurde exportiert: {target}";
        }, "Export abgeschlossen").ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task ImportAsync()
    {
        _currentUser.DemandPermission(Permissions.DataImport);

        var dialog = _services.GetRequiredService<ImportWizardViewModel>();
        dialog.Initialize(ExportArea.Vehicles);

        if (_dialogs.ShowDialog(dialog) == true)
        {
            await ReloadAsync().ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private async Task NextPageAsync()
    {
        if (PageNumber >= PageCount)
        {
            return;
        }

        PageNumber++;
        await ReloadAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task PreviousPageAsync()
    {
        if (PageNumber <= 1)
        {
            return;
        }

        PageNumber--;
        await ReloadAsync().ConfigureAwait(true);
    }

    /// <summary>Verzoegert die Suche, damit nicht bei jedem Tastendruck die Datenbank abgefragt wird.</summary>
    private async Task DelayedReloadAsync()
    {
        _searchCancellation?.Cancel();
        _searchCancellation = new CancellationTokenSource();
        var token = _searchCancellation.Token;

        try
        {
            await Task.Delay(250, token).ConfigureAwait(true);
            PageNumber = 1;
            await ReloadAsync(token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // Eine neue Eingabe hat die Suche ersetzt.
        }
    }
}
