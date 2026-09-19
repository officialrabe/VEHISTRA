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

/// <summary>Kennzeichenverwaltung mit Reservierungen und Historie.</summary>
public sealed partial class LicensePlateViewModel : ViewModelBase, IAcceptsPreset
{
    private readonly ILicensePlateService _plates;
    private readonly IExportService _export;
    private readonly INavigationService _navigation;
    private readonly ICurrentUserService _currentUser;
    private readonly IDialogService _dialogs;
    private readonly IServiceProvider _services;

    [ObservableProperty]
    private LicensePlateListItem? _selectedPlate;

    [ObservableProperty]
    private LicensePlateStatus? _statusFilter;

    [ObservableProperty]
    private string _searchText = string.Empty;

    public LicensePlateViewModel(
        ILicensePlateService plates,
        IExportService export,
        INavigationService navigation,
        ICurrentUserService currentUser,
        IDialogService dialogs,
        IServiceProvider services)
    {
        _plates = plates;
        _export = export;
        _navigation = navigation;
        _currentUser = currentUser;
        _dialogs = dialogs;
        _services = services;
    }

    public override string Title => "Kennzeichen";

    public override string? Subtitle =>
        $"{Plates.Count} Kennzeichen · {Plates.Count(p => p.Status == LicensePlateStatus.Verfuegbar)} verfügbar · " +
        $"{Reservations.Count} laufende Reservierungen";

    public ObservableCollection<LicensePlateListItem> Plates { get; } = [];

    public ObservableCollection<LicensePlateReservation> Reservations { get; } = [];

    public ObservableCollection<LicensePlateAssignment> History { get; } = [];

    public IReadOnlyList<LicensePlateStatus> StatusValues { get; } = Enum.GetValues<LicensePlateStatus>();

    public bool CanManage => _currentUser.HasPermission(Permissions.LicensePlateManage);

    public bool CanExport => _currentUser.HasPermission(Permissions.DataExport);

    public bool CanImport => _currentUser.HasPermission(Permissions.DataImport);

    public override async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        await RunAsync(async () =>
        {
            var items = await _plates.GetListAsync(StatusFilter, SearchText, cancellationToken).ConfigureAwait(true);

            Plates.Clear();
            foreach (var item in items)
            {
                Plates.Add(item);
            }

            Reservations.Clear();
            foreach (var reservation in await _plates.GetReservationsAsync(true, cancellationToken).ConfigureAwait(true))
            {
                Reservations.Add(reservation);
            }

            OnPropertyChanged(nameof(Subtitle));
        }).ConfigureAwait(true);
    }

    partial void OnStatusFilterChanged(LicensePlateStatus? value) => _ = LoadAsync();

    partial void OnSearchTextChanged(string value) => _ = LoadAsync();

    partial void OnSelectedPlateChanged(LicensePlateListItem? value) => _ = LoadHistoryAsync(value);

    private async Task LoadHistoryAsync(LicensePlateListItem? plate)
    {
        History.Clear();

        if (plate is null)
        {
            return;
        }

        await RunAsync(async () =>
        {
            foreach (var entry in await _plates.GetHistoryAsync(plate.Id).ConfigureAwait(true))
            {
                History.Add(entry);
            }
        }).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task CreateAsync()
    {
        var dialog = _services.GetRequiredService<LicensePlateEditViewModel>();
        dialog.InitializeForNew();

        if (_dialogs.ShowDialog(dialog) == true)
        {
            await LoadAsync().ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private async Task EditAsync()
    {
        if (SelectedPlate is null)
        {
            return;
        }

        var dialog = _services.GetRequiredService<LicensePlateEditViewModel>();
        await dialog.InitializeForEditAsync(SelectedPlate.Id).ConfigureAwait(true);

        if (_dialogs.ShowDialog(dialog) == true)
        {
            await LoadAsync().ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private async Task ReserveAsync()
    {
        if (SelectedPlate is null)
        {
            return;
        }

        var dialog = _services.GetRequiredService<ReservationEditViewModel>();
        await dialog.InitializeAsync(SelectedPlate.Id, SelectedPlate.Plate).ConfigureAwait(true);

        if (_dialogs.ShowDialog(dialog) == true)
        {
            await LoadAsync().ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private async Task ReleaseReservationAsync(LicensePlateReservation? reservation)
    {
        if (reservation is null)
        {
            return;
        }

        if (!_dialogs.Confirm(
                $"Soll die Reservierung für „{reservation.LicensePlate?.Plate}“ aufgehoben werden?",
                "Reservierung aufheben"))
        {
            return;
        }

        await RunAsync(async () =>
        {
            await _plates.ReleaseReservationAsync(reservation.Id).ConfigureAwait(true);
            await LoadAsync().ConfigureAwait(true);
        }, "Die Reservierung wurde aufgehoben.").ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task ReleaseFromVehicleAsync()
    {
        if (SelectedPlate?.CurrentVehicleId is null)
        {
            return;
        }

        var reason = _dialogs.Prompt(
            "Bitte geben Sie den Grund für das Lösen der Zuordnung an.",
            "Kennzeichen lösen", "Abmeldung");

        if (string.IsNullOrWhiteSpace(reason))
        {
            return;
        }

        await RunAsync(async () =>
        {
            await _plates.ReleaseFromVehicleAsync(SelectedPlate.Id, DateTime.Now, reason).ConfigureAwait(true);
            await LoadAsync().ConfigureAwait(true);
        }, "Das Kennzeichen wurde vom Fahrzeug gelöst.").ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task OpenVehicleAsync()
    {
        if (SelectedPlate?.CurrentVehicleId is { } vehicleId)
        {
            await _navigation.OpenVehicleAsync(vehicleId, "plates").ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private async Task ImportAsync()
    {
        var dialog = _services.GetRequiredService<ImportWizardViewModel>();
        dialog.Initialize(ExportArea.LicensePlates);

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
            $"Kennzeichen_{DateTime.Now:yyyyMMdd}.{extension}", "Kennzeichenliste exportieren");

        if (string.IsNullOrWhiteSpace(target))
        {
            return;
        }

        await RunAsync(
            async () => await _export.ExportAsync(ExportArea.LicensePlates, exportFormat, target).ConfigureAwait(true),
            $"Die Kennzeichenliste wurde exportiert: {target}").ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync().ConfigureAwait(true);

    /// <inheritdoc />
    public void ApplyPreset(ListPreset preset)
    {
        StatusFilter = preset switch
        {
            ListPreset.KennzeichenVerfuegbar => LicensePlateStatus.Verfuegbar,
            ListPreset.KennzeichenReserviert or ListPreset.ReservierungLaeuftAus
                or ListPreset.ReservierungAbgelaufen => LicensePlateStatus.Reserviert,
            _ => StatusFilter
        };
    }
}
