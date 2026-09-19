using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vehistra.Application.Abstractions;
using Vehistra.Application.Dtos;
using Vehistra.Client.Services;

namespace Vehistra.Client.ViewModels;

/// <summary>Dashboard mit allen Kennzahlen des Fuhrparks.</summary>
public sealed partial class DashboardViewModel : ViewModelBase
{
    private readonly IDashboardService _dashboard;
    private readonly INavigationService _navigation;

    [ObservableProperty]
    private DashboardData? _data;

    [ObservableProperty]
    private DateTime _generatedAt;

    public DashboardViewModel(IDashboardService dashboard, INavigationService navigation)
    {
        _dashboard = dashboard;
        _navigation = navigation;
    }

    public override string Title => "Dashboard";

    public override string? Subtitle => Data is null
        ? "Kennzahlen werden geladen ..."
        : $"Stand {GeneratedAt:dd.MM.yyyy HH:mm} · {Data.Vehicles.Total} Fahrzeuge im Bestand";

    public ObservableCollection<AttentionItem> AttentionItems { get; } = [];

    public override async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        await RunAsync(async () =>
        {
            var data = await _dashboard.GetAsync(cancellationToken).ConfigureAwait(true);

            Data = data;
            GeneratedAt = data.GeneratedAt;

            AttentionItems.Clear();
            foreach (var item in data.AttentionItems)
            {
                AttentionItems.Add(item);
            }

            OnPropertyChanged(nameof(Subtitle));
        }).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync().ConfigureAwait(true);

    [RelayCommand]
    private async Task OpenAttentionItemAsync(AttentionItem? item)
    {
        if (item?.VehicleId is { } vehicleId)
        {
            await _navigation.OpenVehicleAsync(vehicleId).ConfigureAwait(true);
        }
        else if (item?.Category == Domain.Enums.NotificationCategory.Kennzeichenreservierung)
        {
            await _navigation.NavigateToAsync<LicensePlateViewModel>().ConfigureAwait(true);
        }
    }

    /// <summary>
    /// Oeffnet die Liste hinter einer Kennzahl - gefiltert auf genau das, was
    /// die Zahl zeigt. Ohne das muesste der Anwender die Zahl lesen und den
    /// Filter danach von Hand nachbauen.
    /// </summary>
    [RelayCommand]
    private async Task OpenPresetAsync(string? preset)
    {
        if (!Enum.TryParse<ListPreset>(preset, out var wert))
        {
            return;
        }

        var ziel = wert switch
        {
            ListPreset.TuevAbgelaufen or ListPreset.TuevIn14Tagen
                or ListPreset.TuevIn30Tagen or ListPreset.TuevIn60Tagen => typeof(InspectionViewModel),

            ListPreset.OffeneSchaeden or ListPreset.KritischeSchaeden => typeof(DamageViewModel),

            ListPreset.WerkstattOffen or ListPreset.WerkstattHeute
                or ListPreset.WerkstattUeberfaellig => typeof(WorkshopViewModel),

            ListPreset.KennzeichenVerfuegbar or ListPreset.KennzeichenReserviert
                or ListPreset.ReservierungLaeuftAus
                or ListPreset.ReservierungAbgelaufen => typeof(LicensePlateViewModel),

            ListPreset.WartungFaellig => typeof(MaintenanceViewModel),

            _ => typeof(VehicleListViewModel)
        };

        await _navigation.NavigateToAsync(ziel, wert).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task OpenVehiclesAsync() => await _navigation.NavigateToAsync<VehicleListViewModel>().ConfigureAwait(true);

    [RelayCommand]
    private async Task OpenInspectionsAsync() => await _navigation.NavigateToAsync<InspectionViewModel>().ConfigureAwait(true);

    [RelayCommand]
    private async Task OpenDamagesAsync() => await _navigation.NavigateToAsync<DamageViewModel>().ConfigureAwait(true);

    [RelayCommand]
    private async Task OpenWorkshopAsync() => await _navigation.NavigateToAsync<WorkshopViewModel>().ConfigureAwait(true);

    [RelayCommand]
    private async Task OpenPlatesAsync() => await _navigation.NavigateToAsync<LicensePlateViewModel>().ConfigureAwait(true);

    [RelayCommand]
    private async Task OpenMaintenanceAsync() => await _navigation.NavigateToAsync<MaintenanceViewModel>().ConfigureAwait(true);
}
