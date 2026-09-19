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

/// <summary>Fahrzeugakte mit allen Vorgaengen eines Fahrzeugs.</summary>
public sealed partial class VehicleDetailViewModel : ViewModelBase
{
    private readonly IVehicleService _vehicles;
    private readonly IDriverService _drivers;
    private readonly IMileageService _mileage;
    private readonly IInspectionService _inspections;
    private readonly IMaintenanceService _maintenance;
    private readonly IDamageService _damages;
    private readonly IAccidentService _accidents;
    private readonly IWorkshopService _workshop;
    private readonly ILicensePlateService _plates;
    private readonly IDocumentService _documents;
    private readonly IInsuranceService _insurances;
    private readonly IVehicleKeyService _keys;
    private readonly IVehicleLifecycleService _lifecycle;
    private readonly INavigationService _navigation;
    private readonly ICurrentUserService _currentUser;
    private readonly IDialogService _dialogs;
    private readonly IServiceProvider _services;

    [ObservableProperty]
    private int _vehicleId;

    [ObservableProperty]
    private VehicleHeader? _header;

    [ObservableProperty]
    private Vehicle? _vehicle;

    [ObservableProperty]
    private int _selectedTabIndex;

    [ObservableProperty]
    private VehicleRetirement? _retirement;

    public VehicleDetailViewModel(
        IVehicleService vehicles,
        IDriverService drivers,
        IMileageService mileage,
        IInspectionService inspections,
        IMaintenanceService maintenance,
        IDamageService damages,
        IAccidentService accidents,
        IWorkshopService workshop,
        ILicensePlateService plates,
        IDocumentService documents,
        IInsuranceService insurances,
        IVehicleKeyService keys,
        IVehicleLifecycleService lifecycle,
        INavigationService navigation,
        ICurrentUserService currentUser,
        IDialogService dialogs,
        IServiceProvider services)
    {
        _vehicles = vehicles;
        _drivers = drivers;
        _mileage = mileage;
        _inspections = inspections;
        _maintenance = maintenance;
        _damages = damages;
        _accidents = accidents;
        _workshop = workshop;
        _plates = plates;
        _documents = documents;
        _insurances = insurances;
        _keys = keys;
        _lifecycle = lifecycle;
        _navigation = navigation;
        _currentUser = currentUser;
        _dialogs = dialogs;
        _services = services;
    }

    public override string Title => Header is null ? "Fahrzeugakte" : $"Fahrzeug {Header.DisplayTitle}";

    public override string? Subtitle => Header is null
        ? null
        : $"Interne Nummer {Header.InternalNumber} · Status {Header.StatusName} · " +
          $"{Header.CurrentMileage:N0} km · {Schadenstext(Header.OpenDamageCount)}";

    /// <summary>Einzahl oder Mehrzahl - "1 offene Schäden" liest sich falsch.</summary>
    private static string Schadenstext(int anzahl) => anzahl switch
    {
        0 => "keine offenen Schäden",
        1 => "1 offener Schaden",
        _ => $"{anzahl} offene Schäden"
    };

    public ObservableCollection<VehicleDriverAssignment> DriverAssignments { get; } = [];

    public ObservableCollection<VehicleInspection> Inspections { get; } = [];

    public ObservableCollection<MaintenanceEntry> MaintenanceEntries { get; } = [];

    public ObservableCollection<MaintenanceDueItem> MaintenanceDue { get; } = [];

    public ObservableCollection<DamageListItem> Damages { get; } = [];

    public ObservableCollection<AccidentListItem> Accidents { get; } = [];

    public ObservableCollection<WorkshopOrderListItem> WorkshopOrders { get; } = [];

    public ObservableCollection<DocumentListItem> Documents { get; } = [];

    public ObservableCollection<LicensePlateAssignment> PlateHistory { get; } = [];

    public ObservableCollection<MileageEntry> MileageEntries { get; } = [];

    public ObservableCollection<VehicleInsurance> Insurances { get; } = [];

    public ObservableCollection<VehicleKey> Keys { get; } = [];

    public ObservableCollection<VehicleRegistration> Registrations { get; } = [];

    public ObservableCollection<VehicleTimelineEntry> Timeline { get; } = [];

    public ObservableCollection<VehicleCategory> Categories { get; } = [];

    public bool CanEdit => _currentUser.HasPermission(Permissions.VehicleEdit);

    public bool CanAssignDriver => _currentUser.HasPermission(Permissions.DriverAssign);

    public bool CanManageInspections => _currentUser.HasPermission(Permissions.InspectionManage);

    public bool CanManageMaintenance => _currentUser.HasPermission(Permissions.MaintenanceManage);

    public bool CanCreateDamage => _currentUser.HasPermission(Permissions.DamageCreate);

    public bool CanCreateAccident => _currentUser.HasPermission(Permissions.AccidentCreate);

    public bool CanManageWorkshop => _currentUser.HasPermission(Permissions.WorkshopManage);

    public bool CanManageDocuments => _currentUser.HasPermission(Permissions.DocumentManage);

    public bool CanManagePlates => _currentUser.HasPermission(Permissions.LicensePlateManage);

    public bool CanEditMileage => _currentUser.HasPermission(Permissions.MileageEdit);

    public bool CanManageInsurance => _currentUser.HasPermission(Permissions.InsuranceManage);

    public bool CanManageKeys => _currentUser.HasPermission(Permissions.KeyManage);

    public bool CanRetire => _currentUser.HasPermission(Permissions.VehicleRetire);

    public bool CanManageRegistration => _currentUser.HasPermission(Permissions.VehicleRegistration);

    public bool IsRetired => Header?.IsRetired ?? false;

    // Die Zahl an der Registerkarte spart das Durchklicken: man sieht, wo
    // ueberhaupt etwas hinterlegt ist. Leere Bereiche bleiben ohne Zahl.
    public string DriverTabHeader => MitZahl("FAHRER", DriverAssignments.Count);

    public string InspectionTabHeader => MitZahl("TÜV", Inspections.Count);

    public string MaintenanceTabHeader => MitZahl("WARTUNG", MaintenanceEntries.Count);

    public string DamageTabHeader => MitZahl("SCHÄDEN", Damages.Count);

    public string AccidentTabHeader => MitZahl("UNFÄLLE", Accidents.Count);

    public string WorkshopTabHeader => MitZahl("WERKSTATT", WorkshopOrders.Count);

    public string DocumentTabHeader => MitZahl("DOKUMENTE", Documents.Count);

    public string PlateTabHeader => MitZahl("KENNZEICHEN", PlateHistory.Count);

    public string MileageTabHeader => MitZahl("KILOMETER", MileageEntries.Count);

    public string InsuranceTabHeader => MitZahl("VERSICHERUNG", Insurances.Count);

    public string KeyTabHeader => MitZahl("SCHLÜSSEL", Keys.Count);

    public string TimelineTabHeader => MitZahl("HISTORIE", Timeline.Count);

    private static string MitZahl(string text, int anzahl) => anzahl == 0 ? text : $"{text} ({anzahl})";

    /// <summary>Laedt die Fahrzeugakte und springt optional auf einen bestimmten Reiter.</summary>
    public async Task LoadVehicleAsync(int vehicleId, string? tabKey = null)
    {
        VehicleId = vehicleId;

        if (tabKey is not null)
        {
            SelectedTabIndex = tabKey switch
            {
                "driver" => 1,
                "inspection" => 2,
                "maintenance" => 3,
                "damage" => 4,
                "accident" => 5,
                "workshop" => 6,
                "documents" => 7,
                "plates" => 8,
                "mileage" => 9,
                "insurance" => 10,
                "keys" => 11,
                "history" => 12,
                _ => 0
            };
        }

        await LoadAsync().ConfigureAwait(true);
    }

    public override async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (VehicleId <= 0)
        {
            return;
        }

        await RunAsync(async () =>
        {
            Header = await _vehicles.GetHeaderAsync(VehicleId, cancellationToken).ConfigureAwait(true);
            Vehicle = await _vehicles.GetAsync(VehicleId, cancellationToken).ConfigureAwait(true);
            Retirement = await _lifecycle.GetRetirementAsync(VehicleId, cancellationToken).ConfigureAwait(true);

            Replace(Categories, Vehicle?.CategoryAssignments
                .Where(a => a.Category is not null)
                .Select(a => a.Category!) ?? []);

            Replace(DriverAssignments, await _drivers.GetAssignmentsForVehicleAsync(VehicleId, cancellationToken)
                .ConfigureAwait(true));

            if (_currentUser.HasPermission(Permissions.InspectionView))
            {
                Replace(Inspections, await _inspections.GetForVehicleAsync(VehicleId, cancellationToken)
                    .ConfigureAwait(true));
            }

            if (_currentUser.HasPermission(Permissions.MaintenanceView))
            {
                Replace(MaintenanceEntries, await _maintenance.GetEntriesAsync(VehicleId, cancellationToken)
                    .ConfigureAwait(true));
                Replace(MaintenanceDue, await _maintenance.GetDueItemsAsync(VehicleId, cancellationToken)
                    .ConfigureAwait(true));
            }

            if (_currentUser.HasPermission(Permissions.DamageView))
            {
                Replace(Damages, await _damages.GetListAsync(
                    new DamageFilter { VehicleId = VehicleId, OnlyOpen = false }, cancellationToken)
                    .ConfigureAwait(true));
            }

            if (_currentUser.HasPermission(Permissions.AccidentView))
            {
                Replace(Accidents, await _accidents.GetListAsync(VehicleId, false, cancellationToken)
                    .ConfigureAwait(true));
            }

            if (_currentUser.HasPermission(Permissions.WorkshopView))
            {
                Replace(WorkshopOrders, await _workshop.GetOrdersAsync(
                    new WorkshopFilter { VehicleId = VehicleId, OnlyOpen = false }, cancellationToken)
                    .ConfigureAwait(true));
            }

            if (_currentUser.HasPermission(Permissions.DocumentView))
            {
                Replace(Documents, await _documents.GetListAsync(VehicleId, cancellationToken: cancellationToken)
                    .ConfigureAwait(true));
            }

            if (_currentUser.HasPermission(Permissions.LicensePlateView))
            {
                Replace(PlateHistory, await _plates.GetHistoryForVehicleAsync(VehicleId, cancellationToken)
                    .ConfigureAwait(true));
            }

            Replace(MileageEntries, await _mileage.GetHistoryAsync(VehicleId, 500, cancellationToken)
                .ConfigureAwait(true));

            if (_currentUser.HasPermission(Permissions.InsuranceView))
            {
                Replace(Insurances, await _insurances.GetForVehicleAsync(VehicleId, cancellationToken)
                    .ConfigureAwait(true));
            }

            if (_currentUser.HasPermission(Permissions.KeyView))
            {
                Replace(Keys, await _keys.GetForVehicleAsync(VehicleId, cancellationToken).ConfigureAwait(true));
            }

            Replace(Registrations, await _lifecycle.GetRegistrationsAsync(VehicleId, cancellationToken)
                .ConfigureAwait(true));

            Replace(Timeline, await _vehicles.GetTimelineAsync(VehicleId, cancellationToken).ConfigureAwait(true));

            OnPropertyChanged(nameof(Subtitle));
            OnPropertyChanged(nameof(IsRetired));

            foreach (var name in new[]
                     {
                         nameof(DriverTabHeader), nameof(InspectionTabHeader), nameof(MaintenanceTabHeader),
                         nameof(DamageTabHeader), nameof(AccidentTabHeader), nameof(WorkshopTabHeader),
                         nameof(DocumentTabHeader), nameof(PlateTabHeader), nameof(MileageTabHeader),
                         nameof(InsuranceTabHeader), nameof(KeyTabHeader), nameof(TimelineTabHeader)
                     })
            {
                OnPropertyChanged(name);
            }
        }).ConfigureAwait(true);
    }

    /// <summary>Oeffnet die Fahrerakte zu einer Zuordnung in der Fahrzeugakte.</summary>
    [RelayCommand]
    private async Task OpenDriverFileAsync(VehicleDriverAssignment? assignment)
    {
        if (assignment?.DriverId is { } driverId and > 0)
        {
            await _navigation.OpenDriverAsync(driverId).ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private async Task EditAsync()
    {
        var dialog = _services.GetRequiredService<VehicleEditViewModel>();
        await dialog.InitializeForEditAsync(VehicleId).ConfigureAwait(true);

        if (_dialogs.ShowDialog(dialog) == true)
        {
            await LoadAsync().ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private async Task ChangeStatusAsync() =>
        await ShowDialogAsync<StatusChangeViewModel>(vm => vm.InitializeAsync(VehicleId, Header?.DisplayTitle ?? string.Empty))
            .ConfigureAwait(true);

    [RelayCommand]
    private async Task AssignDriverAsync() =>
        await ShowDialogAsync<DriverAssignmentViewModel>(vm => vm.InitializeAsync(VehicleId, Vehicle?.CurrentDriverId))
            .ConfigureAwait(true);

    [RelayCommand]
    private async Task AddMileageAsync() =>
        await ShowDialogAsync<MileageEditViewModel>(vm => vm.InitializeAsync(
                VehicleId, Header?.DisplayTitle ?? string.Empty, Header?.CurrentMileage ?? 0))
            .ConfigureAwait(true);

    [RelayCommand]
    private async Task AddInspectionAsync() =>
        await ShowDialogAsync<InspectionEditViewModel>(vm => vm.InitializeForNewAsync(VehicleId)).ConfigureAwait(true);

    [RelayCommand]
    private async Task EditInspectionAsync(VehicleInspection? inspection)
    {
        if (inspection is null)
        {
            return;
        }

        await ShowDialogAsync<InspectionEditViewModel>(vm => vm.InitializeForEditAsync(inspection.Id))
            .ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task AddMaintenanceAsync() =>
        await ShowDialogAsync<MaintenanceEntryEditViewModel>(vm => vm.InitializeAsync(VehicleId)).ConfigureAwait(true);

    [RelayCommand]
    private async Task AddDamageAsync() =>
        await ShowDialogAsync<DamageEditViewModel>(vm => vm.InitializeForNewAsync(VehicleId)).ConfigureAwait(true);

    [RelayCommand]
    private async Task OpenDamageAsync(DamageListItem? item)
    {
        if (item is null)
        {
            return;
        }

        await ShowDialogAsync<DamageEditViewModel>(vm => vm.InitializeForEditAsync(item.Id)).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task AddAccidentAsync() =>
        await ShowDialogAsync<AccidentEditViewModel>(vm => vm.InitializeForNewAsync(VehicleId)).ConfigureAwait(true);

    [RelayCommand]
    private async Task OpenAccidentAsync(AccidentListItem? item)
    {
        if (item is null)
        {
            return;
        }

        await ShowDialogAsync<AccidentEditViewModel>(vm => vm.InitializeForEditAsync(item.Id)).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task AddWorkshopOrderAsync() =>
        await ShowDialogAsync<WorkshopOrderEditViewModel>(vm => vm.InitializeForNewAsync(VehicleId))
            .ConfigureAwait(true);

    [RelayCommand]
    private async Task OpenWorkshopOrderAsync(WorkshopOrderListItem? item)
    {
        if (item is null)
        {
            return;
        }

        await ShowDialogAsync<WorkshopOrderEditViewModel>(vm => vm.InitializeForEditAsync(item.Id))
            .ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task AddDocumentAsync() =>
        await ShowDialogAsync<DocumentUploadViewModel>(vm => vm.InitializeAsync(VehicleId)).ConfigureAwait(true);

    [RelayCommand]
    private async Task OpenDocumentAsync(DocumentListItem? item)
    {
        if (item is null)
        {
            return;
        }

        await RunAsync(async () =>
        {
            var path = await _documents.GetFullPathAsync(item.Id).ConfigureAwait(true);
            _dialogs.OpenInShell(path);
        }).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task AddInsuranceAsync() =>
        await ShowDialogAsync<InsuranceEditViewModel>(vm => vm.InitializeForNewAsync(VehicleId)).ConfigureAwait(true);

    [RelayCommand]
    private async Task EditInsuranceAsync(VehicleInsurance? insurance)
    {
        if (insurance is null)
        {
            return;
        }

        await ShowDialogAsync<InsuranceEditViewModel>(vm => vm.InitializeForEditAsync(insurance.Id))
            .ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task AddKeyAsync() =>
        await ShowDialogAsync<VehicleKeyEditViewModel>(vm => vm.InitializeForNewAsync(VehicleId)).ConfigureAwait(true);

    [RelayCommand]
    private async Task EditKeyAsync(VehicleKey? key)
    {
        if (key is null)
        {
            return;
        }

        await ShowDialogAsync<VehicleKeyEditViewModel>(vm => vm.InitializeForEditAsync(key.Id)).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task AssignPlateAsync() =>
        await ShowDialogAsync<RegistrationEditViewModel>(vm => vm.InitializeAsync(VehicleId, isRegistration: true))
            .ConfigureAwait(true);

    [RelayCommand]
    private async Task DeregisterAsync() =>
        await ShowDialogAsync<RegistrationEditViewModel>(vm => vm.InitializeAsync(VehicleId, isRegistration: false))
            .ConfigureAwait(true);

    [RelayCommand]
    private async Task RetireAsync() =>
        await ShowDialogAsync<RetirementEditViewModel>(vm => vm.InitializeAsync(VehicleId)).ConfigureAwait(true);

    [RelayCommand]
    private async Task UndoRetirementAsync()
    {
        var reason = _dialogs.Prompt(
            "Bitte geben Sie den Grund für die Rücknahme der Ausmusterung an.",
            "Ausmusterung zurücknehmen");

        if (string.IsNullOrWhiteSpace(reason))
        {
            return;
        }

        await RunAsync(async () =>
        {
            await _lifecycle.UndoRetirementAsync(VehicleId, reason).ConfigureAwait(true);
            await LoadAsync().ConfigureAwait(true);
        }, "Die Ausmusterung wurde zurückgenommen.").ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync().ConfigureAwait(true);

    private async Task ShowDialogAsync<TViewModel>(Func<TViewModel, Task> initialize)
        where TViewModel : Dialogs.DialogViewModelBase
    {
        try
        {
            var dialog = _services.GetRequiredService<TViewModel>();
            await initialize(dialog).ConfigureAwait(true);

            if (_dialogs.ShowDialog(dialog) == true)
            {
                await LoadAsync().ConfigureAwait(true);
            }
        }
        catch (Exception exception)
        {
            ErrorMessage = Describe(exception);
        }
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();

        foreach (var item in items)
        {
            target.Add(item);
        }
    }
}
