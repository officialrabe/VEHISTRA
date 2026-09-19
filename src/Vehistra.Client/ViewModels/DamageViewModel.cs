using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vehistra.Application.Abstractions;
using Vehistra.Application.Dtos;
using Vehistra.Client.Services;
using Vehistra.Client.ViewModels.Dialogs;
using Vehistra.Domain.Entities;
using Vehistra.Domain.Enums;
using Vehistra.Domain.Security;
using Microsoft.Extensions.DependencyInjection;

namespace Vehistra.Client.ViewModels;

/// <summary>Schadensuebersicht ueber den gesamten Fuhrpark.</summary>
public sealed partial class DamageViewModel : ViewModelBase
{
    private readonly IDamageService _damages;
    private readonly IExportService _export;
    private readonly INavigationService _navigation;
    private readonly ICurrentUserService _currentUser;
    private readonly IDialogService _dialogs;
    private readonly IServiceProvider _services;

    [ObservableProperty]
    private DamageListItem? _selectedDamage;

    [ObservableProperty]
    private DamageStatus? _statusFilter;

    [ObservableProperty]
    private DamagePriority? _priorityFilter;

    [ObservableProperty]
    private DamageCategory? _categoryFilter;

    [ObservableProperty]
    private bool _onlyOpen = true;

    [ObservableProperty]
    private bool _onlyNotDriveable;

    [ObservableProperty]
    private bool _onlyInsuranceCases;

    [ObservableProperty]
    private string _searchText = string.Empty;

    public DamageViewModel(
        IDamageService damages,
        IExportService export,
        INavigationService navigation,
        ICurrentUserService currentUser,
        IDialogService dialogs,
        IServiceProvider services)
    {
        _damages = damages;
        _export = export;
        _navigation = navigation;
        _currentUser = currentUser;
        _dialogs = dialogs;
        _services = services;
    }

    public override string Title => "Schäden";

    public override string? Subtitle =>
        $"{Damages.Count} Schäden · {Damages.Count(d => d.Priority == DamagePriority.Kritisch)} kritisch";

    public ObservableCollection<DamageListItem> Damages { get; } = [];

    public ObservableCollection<DamageCategory> Categories { get; } = [];

    public IReadOnlyList<DamageStatus> StatusValues { get; } = Enum.GetValues<DamageStatus>();

    public IReadOnlyList<DamagePriority> PriorityValues { get; } = Enum.GetValues<DamagePriority>();

    public bool CanCreate => _currentUser.HasPermission(Permissions.DamageCreate);

    public bool CanEdit => _currentUser.HasPermission(Permissions.DamageEdit);

    public bool CanClose => _currentUser.HasPermission(Permissions.DamageClose);

    public bool CanExport => _currentUser.HasPermission(Permissions.DataExport);

    public override async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        await RunAsync(async () =>
        {
            if (Categories.Count == 0)
            {
                foreach (var category in await _damages.GetCategoriesAsync(false, cancellationToken).ConfigureAwait(true))
                {
                    Categories.Add(category);
                }
            }

            var filter = new DamageFilter
            {
                Status = StatusFilter,
                Priority = PriorityFilter,
                CategoryId = CategoryFilter?.Id,
                OnlyOpen = OnlyOpen,
                OnlyNotDriveable = OnlyNotDriveable,
                OnlyInsuranceCases = OnlyInsuranceCases,
                SearchText = SearchText
            };

            var items = await _damages.GetListAsync(filter, cancellationToken).ConfigureAwait(true);

            Damages.Clear();
            foreach (var item in items)
            {
                Damages.Add(item);
            }

            OnPropertyChanged(nameof(Subtitle));
        }).ConfigureAwait(true);
    }

    partial void OnStatusFilterChanged(DamageStatus? value) => _ = LoadAsync();

    partial void OnPriorityFilterChanged(DamagePriority? value) => _ = LoadAsync();

    partial void OnCategoryFilterChanged(DamageCategory? value) => _ = LoadAsync();

    partial void OnOnlyOpenChanged(bool value) => _ = LoadAsync();

    partial void OnOnlyNotDriveableChanged(bool value) => _ = LoadAsync();

    partial void OnOnlyInsuranceCasesChanged(bool value) => _ = LoadAsync();

    partial void OnSearchTextChanged(string value) => _ = LoadAsync();

    [RelayCommand]
    private async Task CreateAsync()
    {
        var dialog = _services.GetRequiredService<DamageEditViewModel>();
        await dialog.InitializeForNewAsync(null).ConfigureAwait(true);

        if (_dialogs.ShowDialog(dialog) == true)
        {
            await LoadAsync().ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private async Task EditAsync(DamageListItem? item)
    {
        var target = item ?? SelectedDamage;

        if (target is null)
        {
            return;
        }

        var dialog = _services.GetRequiredService<DamageEditViewModel>();
        await dialog.InitializeForEditAsync(target.Id).ConfigureAwait(true);

        if (_dialogs.ShowDialog(dialog) == true)
        {
            await LoadAsync().ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private async Task CloseDamageAsync()
    {
        if (SelectedDamage is null)
        {
            return;
        }

        if (!_dialogs.Confirm(
                $"Soll der Schaden „{SelectedDamage.DamageNumber}“ abgeschlossen werden?",
                "Schaden abschließen"))
        {
            return;
        }

        await RunAsync(async () =>
        {
            await _damages.CloseAsync(SelectedDamage.Id, DateTime.Now, SelectedDamage.CostActual, null)
                .ConfigureAwait(true);
            await LoadAsync().ConfigureAwait(true);
        }, "Der Schaden wurde abgeschlossen.").ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task OpenVehicleAsync()
    {
        if (SelectedDamage is not null)
        {
            await _navigation.OpenVehicleAsync(SelectedDamage.VehicleId, "damage").ConfigureAwait(true);
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
            $"Schaeden_{DateTime.Now:yyyyMMdd}.{extension}", "Schadensliste exportieren");

        if (string.IsNullOrWhiteSpace(target))
        {
            return;
        }

        await RunAsync(
            async () => await _export.ExportAsync(ExportArea.Damages, exportFormat, target).ConfigureAwait(true),
            $"Die Schadensliste wurde exportiert: {target}").ConfigureAwait(true);
    }

    [RelayCommand]
    private void ResetFilter()
    {
        StatusFilter = null;
        PriorityFilter = null;
        CategoryFilter = null;
        OnlyNotDriveable = false;
        OnlyInsuranceCases = false;
        SearchText = string.Empty;
        OnlyOpen = true;
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync().ConfigureAwait(true);
}
