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

/// <summary>
/// Fahrerakte: alles zu einem Fahrer auf einer Seite. Bisher zeigte die
/// Fahrerliste nur die Zuordnungen; wer wissen wollte, welche Schaeden oder
/// Unfaelle auf einen Fahrer gemeldet sind, musste in drei Listen suchen.
/// </summary>
public sealed partial class DriverDetailViewModel : ViewModelBase
{
    private readonly IDriverService _drivers;
    private readonly IDamageService _damages;
    private readonly IAccidentService _accidents;
    private readonly IWorkshopService _workshop;
    private readonly INavigationService _navigation;
    private readonly ICurrentUserService _currentUser;
    private readonly IDialogService _dialogs;
    private readonly IServiceProvider _services;

    [ObservableProperty]
    private int _driverId;

    [ObservableProperty]
    private Driver? _driver;

    [ObservableProperty]
    private int _selectedTabIndex;

    public DriverDetailViewModel(
        IDriverService drivers,
        IDamageService damages,
        IAccidentService accidents,
        IWorkshopService workshop,
        INavigationService navigation,
        ICurrentUserService currentUser,
        IDialogService dialogs,
        IServiceProvider services)
    {
        _drivers = drivers;
        _damages = damages;
        _accidents = accidents;
        _workshop = workshop;
        _navigation = navigation;
        _currentUser = currentUser;
        _dialogs = dialogs;
        _services = services;
    }

    public override string Title => Driver is null ? "Fahrerakte" : $"Fahrer {Driver.FullName}";

    public override string? Subtitle => Driver is null
        ? null
        : string.Join(" · ", Beschreibungsteile());

    public ObservableCollection<VehicleDriverAssignment> Assignments { get; } = [];

    public ObservableCollection<DamageListItem> Damages { get; } = [];

    public ObservableCollection<AccidentListItem> Accidents { get; } = [];

    public ObservableCollection<WorkshopOrderListItem> WorkshopOrders { get; } = [];

    public bool CanEdit => _currentUser.HasPermission(Permissions.DriverEdit);

    /// <summary>Die laufende Zuordnung, also die ohne Enddatum.</summary>
    public VehicleDriverAssignment? CurrentAssignment =>
        Assignments.FirstOrDefault(a => a.ValidTo is null);

    public string CurrentVehicleText => CurrentAssignment?.Vehicle is { } fahrzeug
        ? $"{fahrzeug.LicensePlate ?? fahrzeug.InternalNumber} · {fahrzeug.Manufacturer} {fahrzeug.Model}"
        : "Derzeit kein Fahrzeug fest zugeordnet";

    public string AssignmentsTabHeader => MitZahl("FAHRZEUGE", Assignments.Count);

    public string DamagesTabHeader => MitZahl("SCHÄDEN", Damages.Count);

    public string AccidentsTabHeader => MitZahl("UNFÄLLE", Accidents.Count);

    public string WorkshopTabHeader => MitZahl("WERKSTATT", WorkshopOrders.Count);

    public int OpenDamageCount => Damages.Count(d => d.Status != Domain.Enums.DamageStatus.Geschlossen);

    internal static string MitZahl(string text, int anzahl) => anzahl == 0 ? text : $"{text} ({anzahl})";

    public async Task LoadDriverAsync(int driverId, CancellationToken cancellationToken = default)
    {
        DriverId = driverId;
        await LoadAsync(cancellationToken).ConfigureAwait(true);
    }

    public override async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (DriverId <= 0)
        {
            return;
        }

        await RunAsync(async () =>
        {
            Driver = await _drivers.GetAsync(DriverId, cancellationToken).ConfigureAwait(true);

            Ersetze(Assignments, await _drivers.GetAssignmentsForDriverAsync(DriverId, cancellationToken)
                .ConfigureAwait(true));

            // Jeder Bereich nur, wenn die Berechtigung dafuer da ist - die
            // Fahrerakte ist kein Weg um die Rechte herum.
            if (_currentUser.HasPermission(Permissions.DamageView))
            {
                Ersetze(Damages, await _damages.GetListAsync(
                    new DamageFilter { DriverId = DriverId, OnlyOpen = false }, cancellationToken)
                    .ConfigureAwait(true));
            }

            if (_currentUser.HasPermission(Permissions.AccidentView))
            {
                Ersetze(Accidents, await _accidents.GetForDriverAsync(DriverId, cancellationToken)
                    .ConfigureAwait(true));
            }

            if (_currentUser.HasPermission(Permissions.WorkshopView))
            {
                Ersetze(WorkshopOrders, await _workshop.GetOrdersAsync(
                    new WorkshopFilter { DriverId = DriverId, OnlyOpen = false }, cancellationToken)
                    .ConfigureAwait(true));
            }

            foreach (var name in new[]
                     {
                         nameof(Title), nameof(Subtitle), nameof(CurrentAssignment), nameof(CurrentVehicleText),
                         nameof(AssignmentsTabHeader), nameof(DamagesTabHeader), nameof(AccidentsTabHeader),
                         nameof(WorkshopTabHeader), nameof(OpenDamageCount)
                     })
            {
                OnPropertyChanged(name);
            }
        }).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task EditAsync()
    {
        if (Driver is null)
        {
            return;
        }

        var dialog = _services.GetRequiredService<DriverEditViewModel>();
        await dialog.InitializeForEditAsync(Driver.Id).ConfigureAwait(true);

        if (_dialogs.ShowDialog(dialog) == true)
        {
            await LoadAsync().ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private async Task OpenVehicleAsync(int? vehicleId)
    {
        if (vehicleId is { } id and > 0)
        {
            await _navigation.OpenVehicleAsync(id).ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private async Task OpenAssignmentVehicleAsync(VehicleDriverAssignment? assignment)
    {
        if (assignment is not null)
        {
            await _navigation.OpenVehicleAsync(assignment.VehicleId).ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private async Task BackToListAsync() =>
        await _navigation.NavigateToAsync<DriverViewModel>().ConfigureAwait(true);

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync().ConfigureAwait(true);

    private IEnumerable<string> Beschreibungsteile()
    {
        if (!string.IsNullOrWhiteSpace(Driver!.PersonnelNumber))
        {
            yield return $"Personalnummer {Driver.PersonnelNumber}";
        }

        yield return Driver.IsActive ? "aktiv" : "stillgelegt";
        yield return CurrentVehicleText;

        if (OpenDamageCount > 0)
        {
            yield return OpenDamageCount == 1 ? "1 offener Schaden" : $"{OpenDamageCount} offene Schäden";
        }
    }

    private static void Ersetze<T>(ObservableCollection<T> ziel, IEnumerable<T> eintraege)
    {
        ziel.Clear();
        foreach (var eintrag in eintraege)
        {
            ziel.Add(eintrag);
        }
    }
}
