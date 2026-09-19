using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vehistra.Application.Abstractions;
using Vehistra.Application.Dtos;
using Vehistra.Client.Services;
using Vehistra.Client.ViewModels.Dialogs;
using Vehistra.Domain.Enums;
using Vehistra.Domain.Security;
using Microsoft.Extensions.DependencyInjection;

namespace Vehistra.Client.ViewModels;

/// <summary>Uebersicht der TUEV-Fristen ueber den gesamten Fuhrpark.</summary>
public sealed partial class InspectionViewModel : ViewModelBase, IAcceptsPreset
{
    private readonly IInspectionService _inspections;
    private readonly IExportService _export;
    private readonly INavigationService _navigation;
    private readonly ICurrentUserService _currentUser;
    private readonly IDialogService _dialogs;
    private readonly IServiceProvider _services;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddInspectionCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenVehicleCommand))]
    private InspectionListItem? _selectedItem;

    [ObservableProperty]
    private int? _withinDays = 60;

    [ObservableProperty]
    private bool _includeRetired;

    [ObservableProperty]
    private int _expiredCount;

    [ObservableProperty]
    private int _dueSoonCount;

    public InspectionViewModel(
        IInspectionService inspections,
        IExportService export,
        INavigationService navigation,
        ICurrentUserService currentUser,
        IDialogService dialogs,
        IServiceProvider services)
    {
        _inspections = inspections;
        _export = export;
        _navigation = navigation;
        _currentUser = currentUser;
        _dialogs = dialogs;
        _services = services;
    }

    public override string Title => "TÜV & Fristen";

    public override string? Subtitle =>
        $"{ExpiredCount} abgelaufen · {DueSoonCount} in den nächsten 30 Tagen fällig";

    public ObservableCollection<InspectionListItem> Items { get; } = [];

    public IReadOnlyList<FilterOption> RangeOptions { get; } =
    [
        new("alle Fahrzeuge", null),
        new("nur abgelaufene", 0),
        new("innerhalb 7 Tage", 7),
        new("innerhalb 14 Tage", 14),
        new("innerhalb 30 Tage", 30),
        new("innerhalb 60 Tage", 60),
        new("innerhalb 90 Tage", 90)
    ];

    public bool CanManage => _currentUser.HasPermission(Permissions.InspectionManage);

    public bool CanExport => _currentUser.HasPermission(Permissions.DataExport);

    public override async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        await RunAsync(async () =>
        {
            var items = await _inspections.GetOverviewAsync(WithinDays, IncludeRetired, cancellationToken)
                .ConfigureAwait(true);

            Items.Clear();
            foreach (var item in items)
            {
                Items.Add(item);
            }

            ExpiredCount = items.Count(i => i.Level == WarningLevel.Kritisch);
            DueSoonCount = items.Count(i => i.DaysRemaining is >= 0 and <= 30);

            OnPropertyChanged(nameof(Subtitle));
        }).ConfigureAwait(true);
    }

    partial void OnWithinDaysChanged(int? value) => _ = LoadAsync();

    partial void OnIncludeRetiredChanged(bool value) => _ = LoadAsync();

    [RelayCommand(CanExecute = nameof(HatAuswahl))]
    private async Task OpenVehicleAsync(InspectionListItem? item)
    {
        var target = item ?? SelectedItem;

        if (target is not null)
        {
            await _navigation.OpenVehicleAsync(target.VehicleId, "inspection").ConfigureAwait(true);
        }
    }

    /// <summary>Ohne Auswahl bleibt die Schaltflaeche abgeblendet statt wirkungslos.</summary>
    private bool HatAuswahl => SelectedItem is not null;

    [RelayCommand(CanExecute = nameof(HatAuswahl))]
    private async Task AddInspectionAsync()
    {
        if (SelectedItem is null)
        {
            return;
        }

        var dialog = _services.GetRequiredService<InspectionEditViewModel>();
        await dialog.InitializeForNewAsync(SelectedItem.VehicleId).ConfigureAwait(true);

        if (_dialogs.ShowDialog(dialog) == true)
        {
            await LoadAsync().ConfigureAwait(true);
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
            $"TUEV-Liste_{DateTime.Now:yyyyMMdd}.{extension}", "TÜV-Liste exportieren");

        if (string.IsNullOrWhiteSpace(target))
        {
            return;
        }

        await RunAsync(
            async () => await _export.ExportAsync(ExportArea.Inspections, exportFormat, target).ConfigureAwait(true),
            $"Die TÜV-Liste wurde exportiert: {target}").ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync().ConfigureAwait(true);

    /// <inheritdoc />
    public void ApplyPreset(ListPreset preset)
    {
        // "Abgelaufen" heisst: Faelligkeit heute oder frueher.
        WithinDays = preset switch
        {
            ListPreset.TuevAbgelaufen => 0,
            ListPreset.TuevIn14Tagen => 14,
            ListPreset.TuevIn30Tagen => 30,
            ListPreset.TuevIn60Tagen => 60,
            _ => WithinDays
        };
    }
}

/// <summary>Auswahlmoeglichkeit eines Zeitraumfilters.</summary>
public sealed record FilterOption(string Display, int? Days);
