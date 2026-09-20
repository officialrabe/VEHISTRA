using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vehistra.Application.Abstractions;
using Vehistra.Application.Dtos;
using Vehistra.Client.Services;
using Vehistra.Domain.Entities;
using Vehistra.Domain.Enums;
using Vehistra.Domain.Security;

namespace Vehistra.Client.ViewModels;

/// <summary>
/// Ausgemusterte Fahrzeuge. Fahrzeuge werden niemals geloescht - alle Historien bleiben erhalten.
/// </summary>
public sealed partial class RetiredVehiclesViewModel : ViewModelBase
{
    private readonly IVehicleLifecycleService _lifecycle;
    private readonly IExportService _export;
    private readonly INavigationService _navigation;
    private readonly ICurrentUserService _currentUser;
    private readonly IDialogService _dialogs;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UndoRetirementCommand))]
    private Vehicle? _selectedVehicle;

    [ObservableProperty]
    private RetirementReason? _reasonFilter;

    public RetiredVehiclesViewModel(
        IVehicleLifecycleService lifecycle,
        IExportService export,
        INavigationService navigation,
        ICurrentUserService currentUser,
        IDialogService dialogs)
    {
        _lifecycle = lifecycle;
        _export = export;
        _navigation = navigation;
        _currentUser = currentUser;
        _dialogs = dialogs;
    }

    public override string Title => "Ausgemusterte Fahrzeuge";

    public override string? Subtitle =>
        $"{Vehicles.Count} Fahrzeuge · alle Daten und Historien bleiben dauerhaft erhalten";

    public ObservableCollection<Vehicle> Vehicles { get; } = [];

    public IReadOnlyList<RetirementReason> ReasonValues { get; } = Enum.GetValues<RetirementReason>();

    public bool CanRetire => _currentUser.HasPermission(Permissions.VehicleRetire);

    public bool CanExport => _currentUser.HasPermission(Permissions.DataExport);

    public override async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        await RunAsync(async () =>
        {
            var vehicles = await _lifecycle.GetRetiredVehiclesAsync(ReasonFilter, cancellationToken)
                .ConfigureAwait(true);

            Vehicles.Clear();
            foreach (var vehicle in vehicles)
            {
                Vehicles.Add(vehicle);
            }

            OnPropertyChanged(nameof(Subtitle));
        }).ConfigureAwait(true);
    }

    partial void OnReasonFilterChanged(RetirementReason? value) => _ = LoadAsync();

    [RelayCommand]
    private async Task OpenVehicleAsync()
    {
        if (SelectedVehicle is not null)
        {
            await _navigation.OpenVehicleAsync(SelectedVehicle.Id).ConfigureAwait(true);
        }
    }

    /// <summary>Ohne Auswahl bleibt die Schaltflaeche abgeblendet statt wirkungslos.</summary>
    private bool HatAuswahl => SelectedVehicle is not null;

    [RelayCommand(CanExecute = nameof(HatAuswahl))]
    private async Task UndoRetirementAsync()
    {
        if (SelectedVehicle is null)
        {
            return;
        }

        var reason = _dialogs.Prompt(
            "Bitte geben Sie den Grund für die Rücknahme der Ausmusterung an.",
            "Ausmusterung zurücknehmen");

        if (string.IsNullOrWhiteSpace(reason))
        {
            return;
        }

        await RunAsync(async () =>
        {
            await _lifecycle.UndoRetirementAsync(SelectedVehicle.Id, reason).ConfigureAwait(true);
            await LoadAsync().ConfigureAwait(true);
        }, "Die Ausmusterung wurde zurückgenommen.").ConfigureAwait(true);
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
            $"Ausgemusterte-Fahrzeuge_{DateTime.Now:yyyyMMdd}.{extension}", "Liste exportieren");

        if (string.IsNullOrWhiteSpace(target))
        {
            return;
        }

        await RunAsync(
            async () => await _export.ExportAsync(ExportArea.RetiredVehicles, exportFormat, target).ConfigureAwait(true),
            $"Die Liste wurde exportiert: {target}").ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync().ConfigureAwait(true);
}
