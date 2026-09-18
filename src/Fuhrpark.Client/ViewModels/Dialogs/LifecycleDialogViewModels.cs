using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Fuhrpark.Application.Abstractions;
using Fuhrpark.Application.Dtos;
using Fuhrpark.Domain.Entities;
using Fuhrpark.Domain.Enums;

namespace Fuhrpark.Client.ViewModels.Dialogs;

/// <summary>An- und Abmeldung eines Fahrzeugs.</summary>
public sealed partial class RegistrationEditViewModel : DialogViewModelBase
{
    private readonly IVehicleLifecycleService _lifecycle;
    private readonly ILicensePlateService _plates;
    private readonly IVehicleService _vehicles;

    [ObservableProperty]
    private int _vehicleId;

    [ObservableProperty]
    private string _vehicleDisplay = string.Empty;

    [ObservableProperty]
    private bool _isRegistration = true;

    [ObservableProperty]
    private DateTime _date = DateTime.Today;

    [ObservableProperty]
    private LicensePlateListItem? _selectedPlate;

    [ObservableProperty]
    private string? _registrationOffice;

    [ObservableProperty]
    private string? _reason;

    [ObservableProperty]
    private bool _releaseLicensePlate = true;

    [ObservableProperty]
    private string? _comment;

    public RegistrationEditViewModel(
        IVehicleLifecycleService lifecycle,
        ILicensePlateService plates,
        IVehicleService vehicles)
    {
        _lifecycle = lifecycle;
        _plates = plates;
        _vehicles = vehicles;
    }

    public override string Title => IsRegistration ? "Fahrzeug anmelden" : "Fahrzeug abmelden";

    public ObservableCollection<LicensePlateListItem> AvailablePlates { get; } = [];

    public async Task InitializeAsync(int vehicleId, bool isRegistration)
    {
        VehicleId = vehicleId;
        IsRegistration = isRegistration;
        Date = DateTime.Today;

        await RunAsync(async () =>
        {
            var vehicle = await _vehicles.GetAsync(vehicleId).ConfigureAwait(true);
            VehicleDisplay = vehicle?.DisplayName ?? string.Empty;

            if (isRegistration)
            {
                AvailablePlates.Clear();

                var plates = await _plates.GetListAsync(LicensePlateStatus.Verfuegbar).ConfigureAwait(true);
                foreach (var plate in plates)
                {
                    AvailablePlates.Add(plate);
                }

                var reserved = await _plates.GetListAsync(LicensePlateStatus.Reserviert).ConfigureAwait(true);
                foreach (var plate in reserved)
                {
                    AvailablePlates.Add(plate);
                }
            }
        }).ConfigureAwait(true);
    }

    protected override async Task<bool> SaveAsync()
    {
        return await RunAsync(async () =>
        {
            if (IsRegistration)
            {
                await _lifecycle.RegisterAsync(VehicleId, Date, SelectedPlate?.Id, RegistrationOffice?.Trim(), Comment)
                    .ConfigureAwait(true);
            }
            else
            {
                await _lifecycle.DeregisterAsync(VehicleId, Date, Reason?.Trim(), ReleaseLicensePlate, Comment)
                    .ConfigureAwait(true);
            }
        }).ConfigureAwait(true);
    }
}

/// <summary>Ausmusterung eines Fahrzeugs. Alle Historien bleiben erhalten.</summary>
public sealed partial class RetirementEditViewModel : DialogViewModelBase
{
    private readonly IVehicleLifecycleService _lifecycle;
    private readonly IVehicleService _vehicles;

    [ObservableProperty]
    private int _vehicleId;

    [ObservableProperty]
    private string _vehicleDisplay = string.Empty;

    [ObservableProperty]
    private DateTime _retiredAt = DateTime.Today;

    [ObservableProperty]
    private RetirementReason _reason = RetirementReason.Verkauft;

    [ObservableProperty]
    private string? _reasonText;

    [ObservableProperty]
    private bool _isDeregistered = true;

    [ObservableProperty]
    private DateTime? _deregisteredAt = DateTime.Today;

    [ObservableProperty]
    private bool _licensePlateRemoved = true;

    [ObservableProperty]
    private int? _lastMileage;

    [ObservableProperty]
    private bool _isSold;

    [ObservableProperty]
    private decimal? _salePrice;

    [ObservableProperty]
    private string? _buyer;

    [ObservableProperty]
    private bool _isScrapped;

    [ObservableProperty]
    private bool _isTotalLoss;

    [ObservableProperty]
    private bool _isLeasingReturn;

    [ObservableProperty]
    private bool _isSparePartDonor;

    [ObservableProperty]
    private string? _comment;

    public RetirementEditViewModel(IVehicleLifecycleService lifecycle, IVehicleService vehicles)
    {
        _lifecycle = lifecycle;
        _vehicles = vehicles;
    }

    public override string Title => "Fahrzeug ausmustern";

    public IReadOnlyList<RetirementReason> Reasons { get; } = Enum.GetValues<RetirementReason>();

    public async Task InitializeAsync(int vehicleId)
    {
        VehicleId = vehicleId;

        await RunAsync(async () =>
        {
            var vehicle = await _vehicles.GetAsync(vehicleId).ConfigureAwait(true);
            VehicleDisplay = vehicle?.DisplayName ?? string.Empty;
            LastMileage = vehicle?.CurrentMileage;
        }).ConfigureAwait(true);
    }

    protected override async Task<bool> SaveAsync()
    {
        return await RunAsync(async () =>
            await _lifecycle.RetireAsync(new VehicleRetirement
            {
                VehicleId = VehicleId,
                RetiredAt = RetiredAt,
                Reason = Reason,
                ReasonText = ReasonText?.Trim(),
                IsDeregistered = IsDeregistered,
                DeregisteredAt = IsDeregistered ? DeregisteredAt : null,
                LicensePlateRemoved = LicensePlateRemoved,
                LastMileage = LastMileage,
                IsSold = IsSold,
                SalePrice = SalePrice,
                Buyer = Buyer?.Trim(),
                IsScrapped = IsScrapped,
                IsTotalLoss = IsTotalLoss,
                IsLeasingReturn = IsLeasingReturn,
                IsSparePartDonor = IsSparePartDonor,
                Comment = Comment
            }).ConfigureAwait(true)).ConfigureAwait(true);
    }
}

/// <summary>Versicherungsvertrag eines Fahrzeugs.</summary>
public sealed partial class InsuranceEditViewModel : DialogViewModelBase
{
    private readonly IInsuranceService _insurances;

    private VehicleInsurance? _entity;

    [ObservableProperty]
    private bool _isNew = true;

    [ObservableProperty]
    private int _vehicleId;

    [ObservableProperty]
    private string _company = string.Empty;

    [ObservableProperty]
    private string? _policyNumber;

    [ObservableProperty]
    private string? _contractNumber;

    [ObservableProperty]
    private InsuranceKind _kind = InsuranceKind.Haftpflicht;

    [ObservableProperty]
    private DateTime? _validFrom = DateTime.Today;

    [ObservableProperty]
    private DateTime? _validTo;

    [ObservableProperty]
    private string? _contactPerson;

    [ObservableProperty]
    private string? _phone;

    [ObservableProperty]
    private string? _email;

    [ObservableProperty]
    private decimal? _annualPremium;

    [ObservableProperty]
    private decimal? _deductibleComprehensive;

    [ObservableProperty]
    private decimal? _deductiblePartial;

    [ObservableProperty]
    private bool _isActive = true;

    [ObservableProperty]
    private string? _comment;

    public InsuranceEditViewModel(IInsuranceService insurances)
    {
        _insurances = insurances;
    }

    public override string Title => IsNew ? "Versicherung hinzufügen" : "Versicherung bearbeiten";

    public IReadOnlyList<InsuranceKind> Kinds { get; } = Enum.GetValues<InsuranceKind>();

    public Task InitializeForNewAsync(int vehicleId)
    {
        IsNew = true;
        VehicleId = vehicleId;

        return Task.CompletedTask;
    }

    public async Task InitializeForEditAsync(int insuranceId)
    {
        IsNew = false;

        var insurances = await _insurances.GetForVehicleAsync(VehicleId).ConfigureAwait(true);
        _entity = insurances.FirstOrDefault(i => i.Id == insuranceId);

        if (_entity is null)
        {
            ErrorMessage = "Die Versicherung wurde nicht gefunden.";
            return;
        }

        VehicleId = _entity.VehicleId;
        Company = _entity.Company;
        PolicyNumber = _entity.PolicyNumber;
        ContractNumber = _entity.ContractNumber;
        Kind = _entity.Kind;
        ValidFrom = _entity.ValidFrom;
        ValidTo = _entity.ValidTo;
        ContactPerson = _entity.ContactPerson;
        Phone = _entity.Phone;
        Email = _entity.Email;
        AnnualPremium = _entity.AnnualPremium;
        DeductibleComprehensive = _entity.DeductibleComprehensive;
        DeductiblePartial = _entity.DeductiblePartial;
        IsActive = _entity.IsActive;
        Comment = _entity.Comment;
    }

    protected override async Task<bool> SaveAsync()
    {
        return await RunAsync(async () =>
        {
            var insurance = _entity ?? new VehicleInsurance { VehicleId = VehicleId };

            insurance.Company = Company?.Trim() ?? string.Empty;
            insurance.PolicyNumber = PolicyNumber?.Trim();
            insurance.ContractNumber = ContractNumber?.Trim();
            insurance.Kind = Kind;
            insurance.ValidFrom = ValidFrom;
            insurance.ValidTo = ValidTo;
            insurance.ContactPerson = ContactPerson?.Trim();
            insurance.Phone = Phone?.Trim();
            insurance.Email = Email?.Trim();
            insurance.AnnualPremium = AnnualPremium;
            insurance.DeductibleComprehensive = DeductibleComprehensive;
            insurance.DeductiblePartial = DeductiblePartial;
            insurance.IsActive = IsActive;
            insurance.Comment = Comment;

            if (IsNew)
            {
                await _insurances.CreateAsync(insurance).ConfigureAwait(true);
            }
            else
            {
                await _insurances.UpdateAsync(insurance).ConfigureAwait(true);
            }
        }).ConfigureAwait(true);
    }
}

/// <summary>Fahrzeugschluessel anlegen, ausgeben und zurueckerhalten.</summary>
public sealed partial class VehicleKeyEditViewModel : DialogViewModelBase
{
    private readonly IVehicleKeyService _keys;
    private readonly IDriverService _drivers;

    private VehicleKey? _entity;

    [ObservableProperty]
    private bool _isNew = true;

    [ObservableProperty]
    private int _vehicleId;

    [ObservableProperty]
    private string _keyNumber = string.Empty;

    [ObservableProperty]
    private int _count = 1;

    [ObservableProperty]
    private string? _storageLocation;

    [ObservableProperty]
    private Driver? _issuedToDriver;

    [ObservableProperty]
    private string? _issuedToName;

    [ObservableProperty]
    private DateTime? _issuedAt;

    [ObservableProperty]
    private DateTime? _returnedAt;

    [ObservableProperty]
    private string? _comment;

    public VehicleKeyEditViewModel(IVehicleKeyService keys, IDriverService drivers)
    {
        _keys = keys;
        _drivers = drivers;
    }

    public override string Title => IsNew ? "Schlüssel hinzufügen" : "Schlüssel bearbeiten";

    public ObservableCollection<Driver> Drivers { get; } = [];

    public async Task InitializeForNewAsync(int vehicleId)
    {
        IsNew = true;
        VehicleId = vehicleId;

        await LoadDriversAsync().ConfigureAwait(true);
    }

    public async Task InitializeForEditAsync(int keyId)
    {
        IsNew = false;
        await LoadDriversAsync().ConfigureAwait(true);

        var keys = await _keys.GetForVehicleAsync(VehicleId).ConfigureAwait(true);
        _entity = keys.FirstOrDefault(k => k.Id == keyId);

        if (_entity is null)
        {
            ErrorMessage = "Der Schlüssel wurde nicht gefunden.";
            return;
        }

        VehicleId = _entity.VehicleId;
        KeyNumber = _entity.KeyNumber;
        Count = _entity.Count;
        StorageLocation = _entity.StorageLocation;
        IssuedToDriver = Drivers.FirstOrDefault(d => d.Id == _entity.IssuedToDriverId);
        IssuedToName = _entity.IssuedToName;
        IssuedAt = _entity.IssuedAt;
        ReturnedAt = _entity.ReturnedAt;
        Comment = _entity.Comment;
    }

    protected override async Task<bool> SaveAsync()
    {
        return await RunAsync(async () =>
        {
            if (IsNew)
            {
                await _keys.CreateAsync(new VehicleKey
                {
                    VehicleId = VehicleId,
                    KeyNumber = KeyNumber?.Trim() ?? string.Empty,
                    Count = Count,
                    StorageLocation = StorageLocation?.Trim(),
                    Comment = Comment
                }).ConfigureAwait(true);

                return;
            }

            var key = _entity!;
            key.KeyNumber = KeyNumber?.Trim() ?? string.Empty;
            key.Count = Count;
            key.StorageLocation = StorageLocation?.Trim();
            key.Comment = Comment;

            await _keys.UpdateAsync(key).ConfigureAwait(true);

            // Ausgabe bzw. Rueckgabe getrennt behandeln, damit die Historie stimmt.
            if (IssuedAt is { } issuedAt && !key.IsIssued)
            {
                await _keys.IssueAsync(key.Id, IssuedToDriver?.Id, IssuedToName?.Trim(), issuedAt)
                    .ConfigureAwait(true);
            }
            else if (ReturnedAt is { } returnedAt && key.IsIssued)
            {
                await _keys.ReturnAsync(key.Id, returnedAt).ConfigureAwait(true);
            }
        }).ConfigureAwait(true);
    }

    private async Task LoadDriversAsync()
    {
        if (Drivers.Count > 0)
        {
            return;
        }

        foreach (var driver in await _drivers.GetDriversAsync().ConfigureAwait(true))
        {
            Drivers.Add(driver);
        }
    }
}
