using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Vehistra.Application.Abstractions;
using Vehistra.Domain.Entities;
using Vehistra.Domain.Enums;

namespace Vehistra.Client.ViewModels.Dialogs;

/// <summary>Anlegen und Bearbeiten eines Fahrers.</summary>
public sealed partial class DriverEditViewModel : DialogViewModelBase
{
    private readonly IDriverService _drivers;
    private Driver? _entity;

    [ObservableProperty]
    private bool _isNew = true;

    [ObservableProperty]
    private string? _personnelNumber;

    [ObservableProperty]
    private string? _firstName;

    [ObservableProperty]
    private string _lastName = string.Empty;

    [ObservableProperty]
    private string? _phone;

    [ObservableProperty]
    private string? _mobile;

    [ObservableProperty]
    private string? _email;

    [ObservableProperty]
    private bool _isActive = true;

    [ObservableProperty]
    private string? _comment;

    public DriverEditViewModel(IDriverService drivers)
    {
        _drivers = drivers;
    }

    public override string Title => IsNew ? "Neuen Fahrer anlegen" : "Fahrer bearbeiten";

    public void InitializeForNew()
    {
        IsNew = true;
        IsActive = true;
    }

    public async Task InitializeForEditAsync(int driverId)
    {
        IsNew = false;
        _entity = await _drivers.GetAsync(driverId).ConfigureAwait(true);

        if (_entity is null)
        {
            ErrorMessage = "Der Fahrer wurde nicht gefunden.";
            return;
        }

        PersonnelNumber = _entity.PersonnelNumber;
        FirstName = _entity.FirstName;
        LastName = _entity.LastName;
        Phone = _entity.Phone;
        Mobile = _entity.Mobile;
        Email = _entity.Email;
        IsActive = _entity.IsActive;
        Comment = _entity.Comment;
    }

    protected override async Task<bool> SaveAsync()
    {
        return await RunAsync(async () =>
        {
            var driver = _entity ?? new Driver();

            driver.PersonnelNumber = string.IsNullOrWhiteSpace(PersonnelNumber) ? null : PersonnelNumber.Trim();
            driver.FirstName = FirstName?.Trim() ?? string.Empty;
            driver.LastName = LastName?.Trim() ?? string.Empty;
            driver.Phone = Phone?.Trim();
            driver.Mobile = Mobile?.Trim();
            driver.Email = Email?.Trim();
            driver.IsActive = IsActive;
            driver.Comment = Comment;

            if (IsNew)
            {
                await _drivers.CreateAsync(driver).ConfigureAwait(true);
            }
            else
            {
                await _drivers.UpdateAsync(driver).ConfigureAwait(true);
            }
        }).ConfigureAwait(true);
    }
}

/// <summary>Erfassung eines Kilometerstands.</summary>
public sealed partial class MileageEditViewModel : DialogViewModelBase
{
    private readonly IMileageService _mileage;

    [ObservableProperty]
    private int _vehicleId;

    [ObservableProperty]
    private string _vehicleDisplay = string.Empty;

    [ObservableProperty]
    private int _currentMileage;

    [ObservableProperty]
    private int _newMileage;

    [ObservableProperty]
    private DateTime _recordedAt = DateTime.Now;

    [ObservableProperty]
    private MileageSource _source = MileageSource.ManuelleEingabe;

    [ObservableProperty]
    private bool _isCorrection;

    [ObservableProperty]
    private string? _comment;

    public MileageEditViewModel(IMileageService mileage)
    {
        _mileage = mileage;
    }

    public override string Title => "Kilometerstand erfassen";

    public IReadOnlyList<MileageSource> Sources { get; } = Enum.GetValues<MileageSource>();

    public Task InitializeAsync(int vehicleId, string vehicleDisplay, int currentMileage)
    {
        VehicleId = vehicleId;
        VehicleDisplay = vehicleDisplay;
        CurrentMileage = currentMileage;
        NewMileage = currentMileage;
        RecordedAt = DateTime.Now;

        return Task.CompletedTask;
    }

    protected override async Task<bool> SaveAsync()
    {
        return await RunAsync(async () =>
            await _mileage.AddAsync(VehicleId, NewMileage, RecordedAt, Source, Comment, IsCorrection)
                .ConfigureAwait(true)).ConfigureAwait(true);
    }
}

/// <summary>Statuswechsel eines Fahrzeugs inklusive Begruendung.</summary>
public sealed partial class StatusChangeViewModel : DialogViewModelBase
{
    private readonly IVehicleService _vehicles;

    [ObservableProperty]
    private int _vehicleId;

    [ObservableProperty]
    private string _vehicleDisplay = string.Empty;

    [ObservableProperty]
    private VehicleStatus? _selectedStatus;

    [ObservableProperty]
    private string? _reason;

    [ObservableProperty]
    private string? _comment;

    public StatusChangeViewModel(IVehicleService vehicles)
    {
        _vehicles = vehicles;
    }

    public override string Title => "Fahrzeugstatus ändern";

    public ObservableCollection<VehicleStatus> Statuses { get; } = [];

    public ObservableCollection<VehicleStatusHistory> History { get; } = [];

    public async Task InitializeAsync(int vehicleId, string vehicleDisplay)
    {
        VehicleId = vehicleId;
        VehicleDisplay = vehicleDisplay;

        await RunAsync(async () =>
        {
            Statuses.Clear();
            foreach (var status in await _vehicles.GetStatusesAsync().ConfigureAwait(true))
            {
                Statuses.Add(status);
            }

            var vehicle = await _vehicles.GetAsync(vehicleId).ConfigureAwait(true);
            SelectedStatus = Statuses.FirstOrDefault(s => s.Id == vehicle?.VehicleStatusId);

            History.Clear();
            foreach (var entry in await _vehicles.GetStatusHistoryAsync(vehicleId).ConfigureAwait(true))
            {
                History.Add(entry);
            }
        }).ConfigureAwait(true);
    }

    protected override async Task<bool> SaveAsync()
    {
        if (SelectedStatus is null)
        {
            ErrorMessage = "Bitte wählen Sie einen Status aus.";
            return false;
        }

        return await RunAsync(async () =>
            await _vehicles.ChangeStatusAsync(VehicleId, SelectedStatus.Id, Reason, Comment)
                .ConfigureAwait(true)).ConfigureAwait(true);
    }
}

/// <summary>Zuweisung des festen Fahrers.</summary>
public sealed partial class DriverAssignmentViewModel : DialogViewModelBase
{
    private readonly IDriverService _drivers;

    [ObservableProperty]
    private int _vehicleId;

    [ObservableProperty]
    private Driver? _selectedDriver;

    [ObservableProperty]
    private DateTime _validFrom = DateTime.Now;

    [ObservableProperty]
    private string? _comment;

    [ObservableProperty]
    private bool _removeAssignment;

    public DriverAssignmentViewModel(IDriverService drivers)
    {
        _drivers = drivers;
    }

    public override string Title => "Festen Fahrer zuweisen";

    public ObservableCollection<Driver> Drivers { get; } = [];

    public ObservableCollection<VehicleDriverAssignment> History { get; } = [];

    public async Task InitializeAsync(int vehicleId, int? currentDriverId)
    {
        VehicleId = vehicleId;

        await RunAsync(async () =>
        {
            Drivers.Clear();
            foreach (var driver in await _drivers.GetDriversAsync().ConfigureAwait(true))
            {
                Drivers.Add(driver);
            }

            SelectedDriver = Drivers.FirstOrDefault(d => d.Id == currentDriverId);

            History.Clear();
            foreach (var assignment in await _drivers.GetAssignmentsForVehicleAsync(vehicleId).ConfigureAwait(true))
            {
                History.Add(assignment);
            }
        }).ConfigureAwait(true);
    }

    protected override async Task<bool> SaveAsync()
    {
        return await RunAsync(async () =>
            await _drivers.AssignDriverAsync(
                VehicleId,
                RemoveAssignment ? null : SelectedDriver?.Id,
                ValidFrom,
                Comment).ConfigureAwait(true)).ConfigureAwait(true);
    }
}

/// <summary>Aendern des eigenen Passworts.</summary>
public sealed partial class ChangePasswordViewModel : DialogViewModelBase
{
    private readonly IAuthenticationService _authentication;

    private string _currentPassword = string.Empty;
    private string _newPassword = string.Empty;
    private string _repeatPassword = string.Empty;

    [ObservableProperty]
    private string? _requirements;

    public ChangePasswordViewModel(IAuthenticationService authentication)
    {
        _authentication = authentication;
    }

    public override string Title => "Passwort ändern";

    public void SetCurrentPassword(string password) => _currentPassword = password;

    public void SetNewPassword(string password) => _newPassword = password;

    public void SetRepeatPassword(string password) => _repeatPassword = password;

    protected override async Task<bool> SaveAsync()
    {
        if (_newPassword != _repeatPassword)
        {
            ErrorMessage = "Die beiden neuen Passwörter stimmen nicht überein.";
            return false;
        }

        var errors = await _authentication.ValidatePasswordAsync(_newPassword).ConfigureAwait(true);
        if (errors.Count > 0)
        {
            ErrorMessage = string.Join(Environment.NewLine, errors);
            return false;
        }

        return await RunAsync(async () =>
            await _authentication.ChangePasswordAsync(_currentPassword, _newPassword).ConfigureAwait(true))
            .ConfigureAwait(true);
    }
}
