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

/// <summary>Fahrerverwaltung mit aktueller und historischer Fahrzeugzuordnung.</summary>
public sealed partial class DriverViewModel : ViewModelBase
{
    private readonly IDriverService _drivers;
    private readonly IExportService _export;
    private readonly INavigationService _navigation;
    private readonly ICurrentUserService _currentUser;
    private readonly IDialogService _dialogs;
    private readonly IServiceProvider _services;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditCommand))]
    [NotifyCanExecuteChangedFor(nameof(ToggleActiveCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenFileCommand))]
    private Driver? _selectedDriver;

    [ObservableProperty]
    private bool _includeInactive;

    [ObservableProperty]
    private string _searchText = string.Empty;

    public DriverViewModel(
        IDriverService drivers,
        IExportService export,
        INavigationService navigation,
        ICurrentUserService currentUser,
        IDialogService dialogs,
        IServiceProvider services)
    {
        _drivers = drivers;
        _export = export;
        _navigation = navigation;
        _currentUser = currentUser;
        _dialogs = dialogs;
        _services = services;
    }

    public override string Title => "Fahrer";

    public override string? Subtitle => $"{Drivers.Count} Fahrer · Auswahl zeigt aktuelle und frühere Fahrzeuge";

    public ObservableCollection<Driver> Drivers { get; } = [];

    public ObservableCollection<VehicleDriverAssignment> Assignments { get; } = [];

    public bool CanEdit => _currentUser.HasPermission(Permissions.DriverEdit);

    public bool CanExport => _currentUser.HasPermission(Permissions.DataExport);

    public bool CanImport => _currentUser.HasPermission(Permissions.DataImport);

    public override async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        await RunAsync(async () =>
        {
            var all = await _drivers.GetDriversAsync(IncludeInactive, cancellationToken).ConfigureAwait(true);

            var filtered = string.IsNullOrWhiteSpace(SearchText)
                ? all
                : all.Where(d =>
                        d.FullName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                        (d.PersonnelNumber?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false))
                    .ToList();

            Drivers.Clear();
            foreach (var driver in filtered)
            {
                Drivers.Add(driver);
            }

            OnPropertyChanged(nameof(Subtitle));
        }).ConfigureAwait(true);
    }

    partial void OnIncludeInactiveChanged(bool value) => _ = LoadAsync();

    partial void OnSearchTextChanged(string value) => _ = LoadAsync();

    partial void OnSelectedDriverChanged(Driver? value) => _ = LoadAssignmentsAsync(value);

    private async Task LoadAssignmentsAsync(Driver? driver)
    {
        Assignments.Clear();

        if (driver is null)
        {
            return;
        }

        await RunAsync(async () =>
        {
            var assignments = await _drivers.GetAssignmentsForDriverAsync(driver.Id).ConfigureAwait(true);

            foreach (var assignment in assignments)
            {
                Assignments.Add(assignment);
            }
        }).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task CreateAsync()
    {
        var dialog = _services.GetRequiredService<DriverEditViewModel>();
        dialog.InitializeForNew();

        if (_dialogs.ShowDialog(dialog) == true)
        {
            await LoadAsync().ConfigureAwait(true);
        }
    }

    /// <summary>Oeffnet die Fahrerakte - alles zu einem Fahrer auf einer Seite.</summary>
    [RelayCommand(CanExecute = nameof(HatAuswahl))]
    private async Task OpenFileAsync()
    {
        if (SelectedDriver is not null)
        {
            await _navigation.OpenDriverAsync(SelectedDriver.Id).ConfigureAwait(true);
        }
    }

    /// <summary>Ohne Auswahl bleibt die Schaltflaeche abgeblendet statt wirkungslos.</summary>
    private bool HatAuswahl => SelectedDriver is not null;

    [RelayCommand(CanExecute = nameof(HatAuswahl))]
    private async Task EditAsync()
    {
        if (SelectedDriver is null)
        {
            return;
        }

        var dialog = _services.GetRequiredService<DriverEditViewModel>();
        await dialog.InitializeForEditAsync(SelectedDriver.Id).ConfigureAwait(true);

        if (_dialogs.ShowDialog(dialog) == true)
        {
            await LoadAsync().ConfigureAwait(true);
        }
    }

    [RelayCommand(CanExecute = nameof(HatAuswahl))]
    private async Task ToggleActiveAsync()
    {
        if (SelectedDriver is null)
        {
            return;
        }

        var driver = SelectedDriver;
        var activate = !driver.IsActive;

        if (!_dialogs.Confirm(
                activate
                    ? $"Soll der Fahrer „{driver.DisplayName}“ wieder aktiviert werden?"
                    : $"Soll der Fahrer „{driver.DisplayName}“ deaktiviert werden?",
                "Fahrer"))
        {
            return;
        }

        await RunAsync(async () =>
        {
            await _drivers.SetActiveAsync(driver.Id, activate).ConfigureAwait(true);
            await LoadAsync().ConfigureAwait(true);
        }).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task OpenVehicleAsync(VehicleDriverAssignment? assignment)
    {
        if (assignment is not null)
        {
            await _navigation.OpenVehicleAsync(assignment.VehicleId).ConfigureAwait(true);
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
            $"Fahrer_{DateTime.Now:yyyyMMdd}.{extension}", "Fahrerliste exportieren");

        if (string.IsNullOrWhiteSpace(target))
        {
            return;
        }

        await RunAsync(
            async () => await _export.ExportAsync(ExportArea.Drivers, exportFormat, target).ConfigureAwait(true),
            $"Die Fahrerliste wurde exportiert: {target}").ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task ImportAsync()
    {
        var dialog = _services.GetRequiredService<ImportWizardViewModel>();
        dialog.Initialize(ExportArea.Drivers);

        if (_dialogs.ShowDialog(dialog) == true)
        {
            await LoadAsync().ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync().ConfigureAwait(true);
}
