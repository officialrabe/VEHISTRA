using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Fuhrpark.Application.Abstractions;
using Fuhrpark.Domain.Entities;
using Fuhrpark.Domain.Enums;

namespace Fuhrpark.Client.ViewModels.Dialogs;

/// <summary>Anlegen und Bearbeiten der Fahrzeugstammdaten.</summary>
public sealed partial class VehicleEditViewModel : DialogViewModelBase
{
    private readonly IVehicleService _vehicles;
    private readonly IDriverService _drivers;

    private Vehicle? _entity;

    [ObservableProperty]
    private bool _isNew = true;

    [ObservableProperty]
    private string _internalNumber = string.Empty;

    [ObservableProperty]
    private string? _licensePlate;

    [ObservableProperty]
    private string? _vin;

    [ObservableProperty]
    private string _manufacturer = string.Empty;

    [ObservableProperty]
    private string _model = string.Empty;

    [ObservableProperty]
    private string? _variant;

    [ObservableProperty]
    private int? _buildYear;

    [ObservableProperty]
    private DateTime? _firstRegistration;

    [ObservableProperty]
    private FuelType _fuelType = FuelType.Diesel;

    [ObservableProperty]
    private TransmissionType _transmission = TransmissionType.Schaltgetriebe;

    [ObservableProperty]
    private int? _powerKw;

    [ObservableProperty]
    private string? _color;

    [ObservableProperty]
    private int? _seats;

    [ObservableProperty]
    private int _currentMileage;

    [ObservableProperty]
    private string? _hsn;

    [ObservableProperty]
    private string? _tsn;

    [ObservableProperty]
    private DateTime? _purchaseDate;

    [ObservableProperty]
    private decimal? _purchasePrice;

    [ObservableProperty]
    private bool _isLeased;

    [ObservableProperty]
    private string? _leasingCompany;

    [ObservableProperty]
    private string? _leasingContractNumber;

    [ObservableProperty]
    private DateTime? _leasingStart;

    [ObservableProperty]
    private DateTime? _leasingEnd;

    [ObservableProperty]
    private string? _comment;

    [ObservableProperty]
    private VehicleStatus? _selectedStatus;

    [ObservableProperty]
    private Driver? _selectedDriver;

    public VehicleEditViewModel(IVehicleService vehicles, IDriverService drivers)
    {
        _vehicles = vehicles;
        _drivers = drivers;
    }

    public override string Title => IsNew ? "Neues Fahrzeug anlegen" : "Fahrzeug bearbeiten";

    public int? SavedVehicleId { get; private set; }

    public ObservableCollection<VehicleStatus> Statuses { get; } = [];

    public ObservableCollection<VehicleCategory> Categories { get; } = [];

    public ObservableCollection<Driver> Drivers { get; } = [];

    public ObservableCollection<CategorySelection> CategorySelections { get; } = [];

    public IReadOnlyList<FuelType> FuelTypes { get; } = Enum.GetValues<FuelType>();

    public IReadOnlyList<TransmissionType> TransmissionTypes { get; } = Enum.GetValues<TransmissionType>();

    public async Task InitializeForNewAsync()
    {
        IsNew = true;
        await LoadLookupsAsync().ConfigureAwait(true);

        SelectedStatus = Statuses.FirstOrDefault(s => s.Kind == VehicleStatusKind.Aktiv) ?? Statuses.FirstOrDefault();
        FirstRegistration = null;
    }

    public async Task InitializeForEditAsync(int vehicleId)
    {
        IsNew = false;
        await LoadLookupsAsync().ConfigureAwait(true);

        _entity = await _vehicles.GetAsync(vehicleId).ConfigureAwait(true);

        if (_entity is null)
        {
            ErrorMessage = "Das Fahrzeug wurde nicht gefunden.";
            return;
        }

        InternalNumber = _entity.InternalNumber;
        LicensePlate = _entity.LicensePlate;
        Vin = _entity.Vin;
        Manufacturer = _entity.Manufacturer;
        Model = _entity.Model;
        Variant = _entity.Variant;
        BuildYear = _entity.BuildYear;
        FirstRegistration = _entity.FirstRegistration;
        FuelType = _entity.FuelType;
        Transmission = _entity.Transmission;
        PowerKw = _entity.PowerKw;
        Color = _entity.Color;
        Seats = _entity.Seats;
        CurrentMileage = _entity.CurrentMileage;
        Hsn = _entity.Hsn;
        Tsn = _entity.Tsn;
        PurchaseDate = _entity.PurchaseDate;
        PurchasePrice = _entity.PurchasePrice;
        IsLeased = _entity.IsLeased;
        LeasingCompany = _entity.LeasingCompany;
        LeasingContractNumber = _entity.LeasingContractNumber;
        LeasingStart = _entity.LeasingStart;
        LeasingEnd = _entity.LeasingEnd;
        Comment = _entity.Comment;

        SelectedStatus = Statuses.FirstOrDefault(s => s.Id == _entity.VehicleStatusId);
        SelectedDriver = Drivers.FirstOrDefault(d => d.Id == _entity.CurrentDriverId);

        var assigned = await _vehicles.GetCategoryIdsAsync(vehicleId).ConfigureAwait(true);

        foreach (var selection in CategorySelections)
        {
            selection.IsSelected = assigned.Contains(selection.Category.Id);
        }
    }

    protected override async Task<bool> SaveAsync()
    {
        return await RunAsync(async () =>
        {
            var vehicle = _entity ?? new Vehicle();

            vehicle.InternalNumber = InternalNumber?.Trim() ?? string.Empty;
            vehicle.LicensePlate = LicensePlate;
            vehicle.Vin = Vin?.Trim();
            vehicle.Manufacturer = Manufacturer?.Trim() ?? string.Empty;
            vehicle.Model = Model?.Trim() ?? string.Empty;
            vehicle.Variant = Variant?.Trim();
            vehicle.BuildYear = BuildYear;
            vehicle.FirstRegistration = FirstRegistration;
            vehicle.FuelType = FuelType;
            vehicle.Transmission = Transmission;
            vehicle.PowerKw = PowerKw;
            vehicle.Color = Color?.Trim();
            vehicle.Seats = Seats;
            vehicle.Hsn = Hsn?.Trim();
            vehicle.Tsn = Tsn?.Trim();
            vehicle.PurchaseDate = PurchaseDate;
            vehicle.PurchasePrice = PurchasePrice;
            vehicle.IsLeased = IsLeased;
            vehicle.LeasingCompany = LeasingCompany?.Trim();
            vehicle.LeasingContractNumber = LeasingContractNumber?.Trim();
            vehicle.LeasingStart = LeasingStart;
            vehicle.LeasingEnd = LeasingEnd;
            vehicle.Comment = Comment;
            vehicle.VehicleStatusId = SelectedStatus?.Id ?? vehicle.VehicleStatusId;

            var categoryIds = CategorySelections
                .Where(c => c.IsSelected)
                .Select(c => c.Category.Id)
                .ToList();

            if (IsNew)
            {
                vehicle.CurrentMileage = CurrentMileage;
                vehicle.CurrentDriverId = SelectedDriver?.Id;

                SavedVehicleId = await _vehicles.CreateAsync(vehicle, categoryIds).ConfigureAwait(true);
            }
            else
            {
                await _vehicles.UpdateAsync(vehicle, categoryIds).ConfigureAwait(true);
                SavedVehicleId = vehicle.Id;
            }
        }).ConfigureAwait(true);
    }

    private async Task LoadLookupsAsync()
    {
        if (Statuses.Count > 0)
        {
            return;
        }

        await RunAsync(async () =>
        {
            foreach (var status in await _vehicles.GetStatusesAsync().ConfigureAwait(true))
            {
                Statuses.Add(status);
            }

            foreach (var category in await _vehicles.GetCategoriesAsync().ConfigureAwait(true))
            {
                Categories.Add(category);
                CategorySelections.Add(new CategorySelection(category));
            }

            foreach (var driver in await _drivers.GetDriversAsync().ConfigureAwait(true))
            {
                Drivers.Add(driver);
            }
        }).ConfigureAwait(true);
    }
}

/// <summary>Auswahl eines Einsatzbereichs im Fahrzeugdialog.</summary>
public sealed partial class CategorySelection : ObservableObject
{
    public CategorySelection(VehicleCategory category)
    {
        Category = category;
    }

    public VehicleCategory Category { get; }

    public string Name => Category.Name;

    [ObservableProperty]
    private bool _isSelected;
}
