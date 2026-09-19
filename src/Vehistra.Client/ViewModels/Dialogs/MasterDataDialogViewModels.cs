using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Vehistra.Application.Abstractions;
using Vehistra.Application.Dtos;
using Vehistra.Domain.Entities;
using Vehistra.Domain.Enums;

namespace Vehistra.Client.ViewModels.Dialogs;

/// <summary>Eintragen einer Hauptuntersuchung.</summary>
public sealed partial class InspectionEditViewModel : DialogViewModelBase
{
    private readonly IInspectionService _inspections;
    private readonly IVehicleService _vehicles;

    private VehicleInspection? _entity;

    [ObservableProperty]
    private bool _isNew = true;

    [ObservableProperty]
    private int _vehicleId;

    [ObservableProperty]
    private string _vehicleDisplay = string.Empty;

    [ObservableProperty]
    private InspectionType _type = InspectionType.HauptUndAbgasuntersuchung;

    [ObservableProperty]
    private DateTime _inspectionDate = DateTime.Today;

    [ObservableProperty]
    private DateTime _nextDueDate = DateTime.Today.AddYears(2);

    [ObservableProperty]
    private DateTime? _nextEmissionDueDate;

    [ObservableProperty]
    private InspectionResult _result = InspectionResult.Bestanden;

    [ObservableProperty]
    private string? _testCenter;

    [ObservableProperty]
    private string? _inspector;

    [ObservableProperty]
    private int? _mileage;

    [ObservableProperty]
    private decimal? _cost;

    [ObservableProperty]
    private string? _defects;

    [ObservableProperty]
    private string? _comment;

    public InspectionEditViewModel(IInspectionService inspections, IVehicleService vehicles)
    {
        _inspections = inspections;
        _vehicles = vehicles;
    }

    public override string Title => IsNew ? "Prüfung eintragen" : "Prüfung bearbeiten";

    public IReadOnlyList<InspectionType> Types { get; } = Enum.GetValues<InspectionType>();

    public IReadOnlyList<InspectionResult> Results { get; } = Enum.GetValues<InspectionResult>();

    public async Task InitializeForNewAsync(int vehicleId)
    {
        IsNew = true;
        VehicleId = vehicleId;

        var vehicle = await _vehicles.GetAsync(vehicleId).ConfigureAwait(true);
        VehicleDisplay = vehicle?.DisplayName ?? string.Empty;
        Mileage = vehicle?.CurrentMileage;

        InspectionDate = DateTime.Today;
        NextDueDate = DateTime.Today.AddYears(2);
    }

    public async Task InitializeForEditAsync(int inspectionId)
    {
        IsNew = false;

        var inspections = await _inspections.GetForVehicleAsync(VehicleId).ConfigureAwait(true);
        _entity = inspections.FirstOrDefault(i => i.Id == inspectionId);

        if (_entity is null)
        {
            ErrorMessage = "Die Prüfung wurde nicht gefunden.";
            return;
        }

        Type = _entity.Type;
        InspectionDate = _entity.InspectionDate;
        NextDueDate = _entity.NextDueDate;
        NextEmissionDueDate = _entity.NextEmissionDueDate;
        Result = _entity.Result;
        TestCenter = _entity.TestCenter;
        Inspector = _entity.Inspector;
        Mileage = _entity.Mileage;
        Cost = _entity.Cost;
        Defects = _entity.Defects;
        Comment = _entity.Comment;
    }

    partial void OnInspectionDateChanged(DateTime value)
    {
        if (IsNew)
        {
            NextDueDate = value.AddYears(2);
        }
    }

    protected override async Task<bool> SaveAsync()
    {
        return await RunAsync(async () =>
        {
            var inspection = _entity ?? new VehicleInspection { VehicleId = VehicleId };

            inspection.Type = Type;
            inspection.InspectionDate = InspectionDate;
            inspection.NextDueDate = NextDueDate;
            inspection.NextEmissionDueDate = NextEmissionDueDate;
            inspection.Result = Result;
            inspection.TestCenter = TestCenter?.Trim();
            inspection.Inspector = Inspector?.Trim();
            inspection.Mileage = Mileage;
            inspection.Cost = Cost;
            inspection.Defects = Defects;
            inspection.Comment = Comment;

            if (IsNew)
            {
                await _inspections.AddAsync(inspection).ConfigureAwait(true);
            }
            else
            {
                await _inspections.UpdateAsync(inspection).ConfigureAwait(true);
            }
        }).ConfigureAwait(true);
    }
}

/// <summary>Eintragen einer durchgefuehrten Wartung.</summary>
public sealed partial class MaintenanceEntryEditViewModel : DialogViewModelBase
{
    private readonly IMaintenanceService _maintenance;
    private readonly IVehicleService _vehicles;
    private readonly IWorkshopService _workshop;

    [ObservableProperty]
    private int _vehicleId;

    [ObservableProperty]
    private string _vehicleDisplay = string.Empty;

    [ObservableProperty]
    private MaintenanceRule? _selectedRule;

    /// <summary>Freie Bezeichnung, falls keine Wartungsregel gewaehlt wird.</summary>
    [ObservableProperty]
    private string? _customTitle;

    [ObservableProperty]
    private DateTime _performedAt = DateTime.Today;

    [ObservableProperty]
    private int? _mileage;

    [ObservableProperty]
    private DateTime? _nextDueDate;

    [ObservableProperty]
    private int? _nextDueMileage;

    [ObservableProperty]
    private Workshop? _selectedWorkshop;

    [ObservableProperty]
    private string? _performedBy;

    [ObservableProperty]
    private decimal? _cost;

    [ObservableProperty]
    private string? _comment;

    public MaintenanceEntryEditViewModel(
        IMaintenanceService maintenance,
        IVehicleService vehicles,
        IWorkshopService workshop)
    {
        _maintenance = maintenance;
        _vehicles = vehicles;
        _workshop = workshop;
    }

    public override string Title => "Wartung eintragen";

    public ObservableCollection<MaintenanceRule> Rules { get; } = [];

    public ObservableCollection<Workshop> Workshops { get; } = [];

    public async Task InitializeAsync(int vehicleId, int? ruleId = null)
    {
        VehicleId = vehicleId;

        await RunAsync(async () =>
        {
            var vehicle = await _vehicles.GetAsync(vehicleId).ConfigureAwait(true);
            VehicleDisplay = vehicle?.DisplayName ?? string.Empty;
            Mileage = vehicle?.CurrentMileage;

            Rules.Clear();
            foreach (var rule in await _maintenance.GetRulesAsync(vehicleId).ConfigureAwait(true))
            {
                Rules.Add(rule);
            }

            Workshops.Clear();
            foreach (var workshop in await _workshop.GetWorkshopsAsync().ConfigureAwait(true))
            {
                Workshops.Add(workshop);
            }

            SelectedRule = Rules.FirstOrDefault(r => r.Id == ruleId) ?? Rules.FirstOrDefault();
        }).ConfigureAwait(true);
    }

    protected override async Task<bool> SaveAsync()
    {
        return await RunAsync(async () =>
            await _maintenance.AddEntryAsync(new MaintenanceEntry
            {
                VehicleId = VehicleId,
                MaintenanceRuleId = SelectedRule?.Id,
                Title = CustomTitle?.Trim(),
                PerformedAt = PerformedAt,
                Mileage = Mileage,
                NextDueDate = NextDueDate,
                NextDueMileage = NextDueMileage,
                WorkshopId = SelectedWorkshop?.Id,
                PerformedBy = PerformedBy?.Trim(),
                Cost = Cost,
                Comment = Comment
            }).ConfigureAwait(true)).ConfigureAwait(true);
    }
}

/// <summary>Anlegen und Bearbeiten einer Wartungsregel.</summary>
public sealed partial class MaintenanceRuleEditViewModel : DialogViewModelBase
{
    private readonly IMaintenanceService _maintenance;
    private readonly IVehicleService _vehicles;

    private MaintenanceRule? _entity;

    [ObservableProperty]
    private bool _isNew = true;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string? _description;

    [ObservableProperty]
    private MaintenanceIntervalType _intervalType = MaintenanceIntervalType.DatumOderKilometer;

    [ObservableProperty]
    private int? _intervalMonths;

    [ObservableProperty]
    private int? _intervalKilometers;

    [ObservableProperty]
    private int _warnDaysBefore = 30;

    [ObservableProperty]
    private int _warnKilometersBefore = 1000;

    [ObservableProperty]
    private VehicleCategory? _selectedCategory;

    [ObservableProperty]
    private bool _isActive = true;

    public MaintenanceRuleEditViewModel(IMaintenanceService maintenance, IVehicleService vehicles)
    {
        _maintenance = maintenance;
        _vehicles = vehicles;
    }

    public override string Title => IsNew ? "Neue Wartungsregel" : "Wartungsregel bearbeiten";

    public ObservableCollection<VehicleCategory> Categories { get; } = [];

    public IReadOnlyList<MaintenanceIntervalType> IntervalTypes { get; } = Enum.GetValues<MaintenanceIntervalType>();

    public async Task InitializeForNewAsync()
    {
        IsNew = true;
        await LoadCategoriesAsync().ConfigureAwait(true);
    }

    public async Task InitializeForEditAsync(int ruleId)
    {
        IsNew = false;
        await LoadCategoriesAsync().ConfigureAwait(true);

        var rules = await _maintenance.GetRulesAsync().ConfigureAwait(true);
        _entity = rules.FirstOrDefault(r => r.Id == ruleId);

        if (_entity is null)
        {
            ErrorMessage = "Die Wartungsregel wurde nicht gefunden.";
            return;
        }

        Name = _entity.Name;
        Description = _entity.Description;
        IntervalType = _entity.IntervalType;
        IntervalMonths = _entity.IntervalMonths;
        IntervalKilometers = _entity.IntervalKilometers;
        WarnDaysBefore = _entity.WarnDaysBefore;
        WarnKilometersBefore = _entity.WarnKilometersBefore;
        SelectedCategory = Categories.FirstOrDefault(c => c.Id == _entity.VehicleCategoryId);
        IsActive = _entity.IsActive;
    }

    protected override async Task<bool> SaveAsync()
    {
        return await RunAsync(async () =>
        {
            var rule = _entity ?? new MaintenanceRule();

            rule.Name = Name?.Trim() ?? string.Empty;
            rule.Description = Description;
            rule.IntervalType = IntervalType;
            rule.IntervalMonths = IntervalMonths;
            rule.IntervalKilometers = IntervalKilometers;
            rule.WarnDaysBefore = WarnDaysBefore;
            rule.WarnKilometersBefore = WarnKilometersBefore;
            rule.VehicleCategoryId = SelectedCategory?.Id;
            rule.IsActive = IsActive;

            if (IsNew)
            {
                await _maintenance.CreateRuleAsync(rule).ConfigureAwait(true);
            }
            else
            {
                await _maintenance.UpdateRuleAsync(rule).ConfigureAwait(true);
            }
        }).ConfigureAwait(true);
    }

    private async Task LoadCategoriesAsync()
    {
        if (Categories.Count > 0)
        {
            return;
        }

        foreach (var category in await _vehicles.GetCategoriesAsync().ConfigureAwait(true))
        {
            Categories.Add(category);
        }
    }
}

/// <summary>Anlegen und Bearbeiten eines Kennzeichens.</summary>
public sealed partial class LicensePlateEditViewModel : DialogViewModelBase
{
    private readonly ILicensePlateService _plates;

    private LicensePlate? _entity;

    [ObservableProperty]
    private bool _isNew = true;

    [ObservableProperty]
    private string _plate = string.Empty;

    [ObservableProperty]
    private LicensePlateStatus _status = LicensePlateStatus.Verfuegbar;

    [ObservableProperty]
    private string? _registrationOffice;

    [ObservableProperty]
    private bool _isSeasonPlate;

    [ObservableProperty]
    private string? _seasonFrom;

    [ObservableProperty]
    private string? _seasonTo;

    [ObservableProperty]
    private string? _comment;

    public LicensePlateEditViewModel(ILicensePlateService plates)
    {
        _plates = plates;
    }

    public override string Title => IsNew ? "Neues Kennzeichen" : "Kennzeichen bearbeiten";

    public IReadOnlyList<LicensePlateStatus> StatusValues { get; } = Enum.GetValues<LicensePlateStatus>();

    public void InitializeForNew()
    {
        IsNew = true;
        Status = LicensePlateStatus.Verfuegbar;
    }

    public async Task InitializeForEditAsync(int plateId)
    {
        IsNew = false;
        _entity = await _plates.GetAsync(plateId).ConfigureAwait(true);

        if (_entity is null)
        {
            ErrorMessage = "Das Kennzeichen wurde nicht gefunden.";
            return;
        }

        Plate = _entity.Plate;
        Status = _entity.Status;
        RegistrationOffice = _entity.RegistrationOffice;
        IsSeasonPlate = _entity.IsSeasonPlate;
        SeasonFrom = _entity.SeasonFrom;
        SeasonTo = _entity.SeasonTo;
        Comment = _entity.Comment;
    }

    protected override async Task<bool> SaveAsync()
    {
        return await RunAsync(async () =>
        {
            var plate = _entity ?? new LicensePlate();

            plate.Plate = Plate?.Trim() ?? string.Empty;
            plate.Status = Status;
            plate.RegistrationOffice = RegistrationOffice?.Trim();
            plate.IsSeasonPlate = IsSeasonPlate;
            plate.SeasonFrom = SeasonFrom?.Trim();
            plate.SeasonTo = SeasonTo?.Trim();
            plate.Comment = Comment;

            if (IsNew)
            {
                await _plates.CreateAsync(plate).ConfigureAwait(true);
            }
            else
            {
                await _plates.UpdateAsync(plate).ConfigureAwait(true);
            }
        }).ConfigureAwait(true);
    }
}

/// <summary>Reservierung eines Kennzeichens bei der Zulassungsstelle.</summary>
public sealed partial class ReservationEditViewModel : DialogViewModelBase
{
    private readonly ILicensePlateService _plates;

    [ObservableProperty]
    private int _licensePlateId;

    [ObservableProperty]
    private string _plate = string.Empty;

    [ObservableProperty]
    private DateTime _reservedAt = DateTime.Today;

    [ObservableProperty]
    private DateTime _reservedUntil = DateTime.Today.AddDays(90);

    [ObservableProperty]
    private string? _registrationOffice;

    [ObservableProperty]
    private string? _reservationNumber;

    [ObservableProperty]
    private string? _comment;

    public ReservationEditViewModel(ILicensePlateService plates)
    {
        _plates = plates;
    }

    public override string Title => "Kennzeichen reservieren";

    public Task InitializeAsync(int licensePlateId, string plate)
    {
        LicensePlateId = licensePlateId;
        Plate = plate;
        ReservedAt = DateTime.Today;
        ReservedUntil = DateTime.Today.AddDays(90);

        return Task.CompletedTask;
    }

    protected override async Task<bool> SaveAsync()
    {
        return await RunAsync(async () =>
            await _plates.CreateReservationAsync(new LicensePlateReservation
            {
                LicensePlateId = LicensePlateId,
                ReservedAt = ReservedAt,
                ReservedUntil = ReservedUntil,
                RegistrationOffice = RegistrationOffice?.Trim(),
                ReservationNumber = ReservationNumber?.Trim(),
                Comment = Comment
            }).ConfigureAwait(true)).ConfigureAwait(true);
    }
}
