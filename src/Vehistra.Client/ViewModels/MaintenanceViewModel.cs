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

/// <summary>Wartungsuebersicht: faellige Services und Verwaltung der Wartungsregeln.</summary>
public sealed partial class MaintenanceViewModel : ViewModelBase
{
    private readonly IMaintenanceService _maintenance;
    private readonly IExportService _export;
    private readonly INavigationService _navigation;
    private readonly ICurrentUserService _currentUser;
    private readonly IDialogService _dialogs;
    private readonly IServiceProvider _services;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RecordMaintenanceCommand))]
    private MaintenanceDueItem? _selectedDueItem;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditRuleCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteRuleCommand))]
    private MaintenanceRule? _selectedRule;

    [ObservableProperty]
    private bool _onlyCritical;

    public MaintenanceViewModel(
        IMaintenanceService maintenance,
        IExportService export,
        INavigationService navigation,
        ICurrentUserService currentUser,
        IDialogService dialogs,
        IServiceProvider services)
    {
        _maintenance = maintenance;
        _export = export;
        _navigation = navigation;
        _currentUser = currentUser;
        _dialogs = dialogs;
        _services = services;
    }

    public override string Title => "Wartung";

    public override string? Subtitle => $"{DueItems.Count} fällige oder bald fällige Wartungen · {Rules.Count} Regeln";

    public ObservableCollection<MaintenanceDueItem> DueItems { get; } = [];

    public ObservableCollection<MaintenanceRule> Rules { get; } = [];

    public bool CanManage => _currentUser.HasPermission(Permissions.MaintenanceManage);

    public bool CanExport => _currentUser.HasPermission(Permissions.DataExport);

    public override async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        await RunAsync(async () =>
        {
            var due = await _maintenance.GetDueItemsAsync(cancellationToken: cancellationToken).ConfigureAwait(true);

            DueItems.Clear();
            foreach (var item in due.Where(d => !OnlyCritical || d.Level == WarningLevel.Kritisch))
            {
                DueItems.Add(item);
            }

            Rules.Clear();
            foreach (var rule in await _maintenance.GetRulesAsync(cancellationToken: cancellationToken)
                         .ConfigureAwait(true))
            {
                Rules.Add(rule);
            }

            OnPropertyChanged(nameof(Subtitle));
        }).ConfigureAwait(true);
    }

    partial void OnOnlyCriticalChanged(bool value) => _ = LoadAsync();

    [RelayCommand]
    private async Task OpenVehicleAsync(MaintenanceDueItem? item)
    {
        var target = item ?? SelectedDueItem;

        if (target is not null)
        {
            await _navigation.OpenVehicleAsync(target.VehicleId, "maintenance").ConfigureAwait(true);
        }
    }

    /// <summary>Ohne Auswahl bleibt die Schaltflaeche abgeblendet statt wirkungslos.</summary>
    private bool HatFaelligkeit => SelectedDueItem is not null;

    [RelayCommand(CanExecute = nameof(HatFaelligkeit))]
    private async Task RecordMaintenanceAsync()
    {
        if (SelectedDueItem is null)
        {
            return;
        }

        var dialog = _services.GetRequiredService<MaintenanceEntryEditViewModel>();
        await dialog.InitializeAsync(SelectedDueItem.VehicleId, SelectedDueItem.MaintenanceRuleId)
            .ConfigureAwait(true);

        if (_dialogs.ShowDialog(dialog) == true)
        {
            await LoadAsync().ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private async Task CreateRuleAsync()
    {
        var dialog = _services.GetRequiredService<MaintenanceRuleEditViewModel>();
        await dialog.InitializeForNewAsync().ConfigureAwait(true);

        if (_dialogs.ShowDialog(dialog) == true)
        {
            await LoadAsync().ConfigureAwait(true);
        }
    }

    /// <summary>Ohne Auswahl bleibt die Schaltflaeche abgeblendet statt wirkungslos.</summary>
    private bool HatRegel => SelectedRule is not null;

    [RelayCommand(CanExecute = nameof(HatRegel))]
    private async Task EditRuleAsync()
    {
        if (SelectedRule is null)
        {
            return;
        }

        var dialog = _services.GetRequiredService<MaintenanceRuleEditViewModel>();
        await dialog.InitializeForEditAsync(SelectedRule.Id).ConfigureAwait(true);

        if (_dialogs.ShowDialog(dialog) == true)
        {
            await LoadAsync().ConfigureAwait(true);
        }
    }

    [RelayCommand(CanExecute = nameof(HatRegel))]
    private async Task DeleteRuleAsync()
    {
        if (SelectedRule is null)
        {
            return;
        }

        if (!_dialogs.Confirm(
                $"Soll die Wartungsregel „{SelectedRule.Name}“ entfernt werden?" + Environment.NewLine +
                "Bereits erfasste Wartungen bleiben erhalten.",
                "Wartungsregel entfernen"))
        {
            return;
        }

        await RunAsync(async () =>
        {
            await _maintenance.DeleteRuleAsync(SelectedRule.Id).ConfigureAwait(true);
            await LoadAsync().ConfigureAwait(true);
        }).ConfigureAwait(true);
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
            $"Wartung_{DateTime.Now:yyyyMMdd}.{extension}", "Wartungsliste exportieren");

        if (string.IsNullOrWhiteSpace(target))
        {
            return;
        }

        await RunAsync(
            async () => await _export.ExportAsync(ExportArea.Maintenance, exportFormat, target).ConfigureAwait(true),
            $"Die Wartungsliste wurde exportiert: {target}").ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync().ConfigureAwait(true);
}
