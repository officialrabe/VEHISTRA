using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vehistra.Application.Abstractions;
using Vehistra.Application.Dtos;
using Vehistra.Client.Services;
using Vehistra.Domain.Entities;
using Vehistra.Domain.Enums;

namespace Vehistra.Client.ViewModels.Dialogs;

/// <summary>Erfassen und Bearbeiten eines Unfalls inklusive Beteiligter und Zeugen.</summary>
public sealed partial class AccidentEditViewModel : DialogViewModelBase
{
    private readonly IAccidentService _accidents;
    private readonly IVehicleService _vehicles;
    private readonly IDriverService _drivers;
    private readonly IReportGenerator _reports;
    private readonly IDialogService _dialogs;

    private AccidentReport? _entity;

    [ObservableProperty]
    private bool _isNew = true;

    [ObservableProperty]
    private string? _accidentNumber;

    [ObservableProperty]
    private VehicleListItem? _selectedVehicle;

    [ObservableProperty]
    private Driver? _selectedDriver;

    [ObservableProperty]
    private string? _driverPhone;

    [ObservableProperty]
    private DateTime _occurredAt = DateTime.Now;

    [ObservableProperty]
    private string? _location;

    [ObservableProperty]
    private string? _street;

    [ObservableProperty]
    private string? _postalCode;

    [ObservableProperty]
    private string? _city;

    [ObservableProperty]
    private int? _mileage;

    [ObservableProperty]
    private AccidentType _type = AccidentType.UnfallMitFremdbeteiligung;

    [ObservableProperty]
    private string? _typeOther;

    [ObservableProperty]
    private string? _courseOfEvents;

    [ObservableProperty]
    private bool _thirdPartyInvolved = true;

    [ObservableProperty]
    private bool _personalInjury;

    [ObservableProperty]
    private bool _policeInvolved;

    [ObservableProperty]
    private string? _policeStation;

    [ObservableProperty]
    private string? _policeFileNumber;

    [ObservableProperty]
    private bool _vehicleDriveable = true;

    [ObservableProperty]
    private string? _ownInsuranceClaimNumber;

    [ObservableProperty]
    private string? _comment;

    // Beteiligter Dritter
    [ObservableProperty]
    private string? _participantLicensePlate;

    [ObservableProperty]
    private string? _participantLastName;

    [ObservableProperty]
    private string? _participantFirstName;

    [ObservableProperty]
    private string? _participantPhone;

    [ObservableProperty]
    private string? _participantStreet;

    [ObservableProperty]
    private string? _participantPostalCode;

    [ObservableProperty]
    private string? _participantCity;

    [ObservableProperty]
    private string? _participantInsurance;

    [ObservableProperty]
    private string? _participantInsuranceNumber;

    // Zeuge
    [ObservableProperty]
    private string? _witnessName;

    [ObservableProperty]
    private string? _witnessPhone;

    [ObservableProperty]
    private string? _damageDescription;

    [ObservableProperty]
    private bool _createDamage = true;

    public AccidentEditViewModel(
        IAccidentService accidents,
        IVehicleService vehicles,
        IDriverService drivers,
        IReportGenerator reports,
        IDialogService dialogs)
    {
        _accidents = accidents;
        _vehicles = vehicles;
        _drivers = drivers;
        _reports = reports;
        _dialogs = dialogs;
    }

    public override string Title => IsNew ? "Unfall erfassen" : $"Unfall {AccidentNumber} bearbeiten";

    public ObservableCollection<VehicleListItem> Vehicles { get; } = [];

    public ObservableCollection<Driver> Drivers { get; } = [];

    public ObservableCollection<AccidentParticipant> Participants { get; } = [];

    public ObservableCollection<AccidentWitness> Witnesses { get; } = [];

    public IReadOnlyList<AccidentType> Types { get; } = Enum.GetValues<AccidentType>();

    public async Task InitializeForNewAsync(int? vehicleId)
    {
        IsNew = true;
        await LoadLookupsAsync().ConfigureAwait(true);

        SelectedVehicle = Vehicles.FirstOrDefault(v => v.Id == vehicleId);
        Mileage = SelectedVehicle?.CurrentMileage;
    }

    public async Task InitializeForEditAsync(int accidentId)
    {
        IsNew = false;
        await LoadLookupsAsync().ConfigureAwait(true);

        _entity = await _accidents.GetAsync(accidentId).ConfigureAwait(true);

        if (_entity is null)
        {
            ErrorMessage = "Der Unfall wurde nicht gefunden.";
            return;
        }

        AccidentNumber = _entity.AccidentNumber;
        SelectedVehicle = Vehicles.FirstOrDefault(v => v.Id == _entity.VehicleId);
        SelectedDriver = Drivers.FirstOrDefault(d => d.Id == _entity.DriverId);
        DriverPhone = _entity.DriverPhone;
        OccurredAt = _entity.OccurredAt;
        Location = _entity.Location;
        Street = _entity.Street;
        PostalCode = _entity.PostalCode;
        City = _entity.City;
        Mileage = _entity.Mileage;
        Type = _entity.Type;
        TypeOther = _entity.TypeOther;
        CourseOfEvents = _entity.CourseOfEvents;
        ThirdPartyInvolved = _entity.ThirdPartyInvolved;
        PersonalInjury = _entity.PersonalInjury;
        PoliceInvolved = _entity.PoliceInvolved;
        PoliceStation = _entity.PoliceStation;
        PoliceFileNumber = _entity.PoliceFileNumber;
        VehicleDriveable = _entity.VehicleDriveable;
        OwnInsuranceClaimNumber = _entity.OwnInsuranceClaimNumber;
        Comment = _entity.Comment;
        CreateDamage = false;

        Participants.Clear();
        foreach (var participant in _entity.Participants)
        {
            Participants.Add(participant);
        }

        Witnesses.Clear();
        foreach (var witness in _entity.Witnesses)
        {
            Witnesses.Add(witness);
        }
    }

    partial void OnSelectedVehicleChanged(VehicleListItem? value)
    {
        if (IsNew && value is not null)
        {
            Mileage = value.CurrentMileage;
        }
    }

    partial void OnSelectedDriverChanged(Driver? value)
    {
        if (value is not null && string.IsNullOrWhiteSpace(DriverPhone))
        {
            DriverPhone = value.Phone ?? value.Mobile;
        }
    }

    [RelayCommand]
    private async Task PrintReportAsync()
    {
        if (_entity is null)
        {
            _dialogs.ShowInformation(
                "Bitte speichern Sie den Unfall zuerst, damit der Bericht mit allen Angaben erzeugt werden kann.",
                "Unfallbericht");
            return;
        }

        await RunAsync(async () =>
            await _reports.CreateAccidentReportForAccidentAsync(_entity.Id).ConfigureAwait(true))
            .ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task RemoveParticipantAsync(AccidentParticipant? participant)
    {
        if (participant is null || _entity is null)
        {
            return;
        }

        await RunAsync(async () =>
        {
            await _accidents.RemoveParticipantAsync(participant.Id).ConfigureAwait(true);
            Participants.Remove(participant);
        }).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task RemoveWitnessAsync(AccidentWitness? witness)
    {
        if (witness is null || _entity is null)
        {
            return;
        }

        await RunAsync(async () =>
        {
            await _accidents.RemoveWitnessAsync(witness.Id).ConfigureAwait(true);
            Witnesses.Remove(witness);
        }).ConfigureAwait(true);
    }

    protected override async Task<bool> SaveAsync()
    {
        if (SelectedVehicle is null)
        {
            ErrorMessage = "Bitte wählen Sie ein Fahrzeug aus.";
            return false;
        }

        return await RunAsync(async () =>
        {
            var accident = _entity ?? new AccidentReport();

            accident.VehicleId = SelectedVehicle.Id;
            accident.DriverId = SelectedDriver?.Id;
            accident.DriverPhone = DriverPhone?.Trim();
            accident.OccurredAt = OccurredAt;
            accident.Location = Location?.Trim();
            accident.Street = Street?.Trim();
            accident.PostalCode = PostalCode?.Trim();
            accident.City = City?.Trim();
            accident.Mileage = Mileage;
            accident.Type = Type;
            accident.TypeOther = TypeOther?.Trim();
            accident.CourseOfEvents = CourseOfEvents;
            accident.ThirdPartyInvolved = ThirdPartyInvolved;
            accident.PersonalInjury = PersonalInjury;
            accident.PoliceInvolved = PoliceInvolved;
            accident.PoliceStation = PoliceStation?.Trim();
            accident.PoliceFileNumber = PoliceFileNumber?.Trim();
            accident.VehicleDriveable = VehicleDriveable;
            accident.OwnInsuranceClaimNumber = OwnInsuranceClaimNumber?.Trim();
            accident.Comment = Comment;

            int accidentId;

            if (IsNew)
            {
                accidentId = await _accidents.CreateAsync(accident).ConfigureAwait(true);
            }
            else
            {
                await _accidents.UpdateAsync(accident).ConfigureAwait(true);
                accidentId = accident.Id;
            }

            // Beteiligten Dritten uebernehmen, sofern Angaben gemacht wurden.
            if (!string.IsNullOrWhiteSpace(ParticipantLastName) || !string.IsNullOrWhiteSpace(ParticipantLicensePlate))
            {
                await _accidents.AddParticipantAsync(new AccidentParticipant
                {
                    AccidentReportId = accidentId,
                    LicensePlate = ParticipantLicensePlate?.Trim(),
                    LastName = ParticipantLastName?.Trim(),
                    FirstName = ParticipantFirstName?.Trim(),
                    Phone = ParticipantPhone?.Trim(),
                    Street = ParticipantStreet?.Trim(),
                    PostalCode = ParticipantPostalCode?.Trim(),
                    City = ParticipantCity?.Trim(),
                    InsuranceCompany = ParticipantInsurance?.Trim(),
                    InsuranceNumber = ParticipantInsuranceNumber?.Trim()
                }).ConfigureAwait(true);

                ParticipantLicensePlate = null;
                ParticipantLastName = null;
                ParticipantFirstName = null;
                ParticipantPhone = null;
                ParticipantStreet = null;
                ParticipantPostalCode = null;
                ParticipantCity = null;
                ParticipantInsurance = null;
                ParticipantInsuranceNumber = null;
            }

            if (!string.IsNullOrWhiteSpace(WitnessName))
            {
                var parts = WitnessName.Split(' ', 2);

                await _accidents.AddWitnessAsync(new AccidentWitness
                {
                    AccidentReportId = accidentId,
                    FirstName = parts.Length > 1 ? parts[0] : null,
                    LastName = parts.Length > 1 ? parts[1] : parts[0],
                    Phone = WitnessPhone?.Trim()
                }).ConfigureAwait(true);

                WitnessName = null;
                WitnessPhone = null;
            }

            if (CreateDamage && !string.IsNullOrWhiteSpace(DamageDescription))
            {
                await _accidents.CreateDamageFromAccidentAsync(accidentId, DamageDescription).ConfigureAwait(true);
            }
        }).ConfigureAwait(true);
    }

    private async Task LoadLookupsAsync()
    {
        if (Vehicles.Count > 0)
        {
            return;
        }

        await RunAsync(async () =>
        {
            var vehicles = await _vehicles.GetListAsync(new VehicleFilter { IsRetired = false, PageSize = 500 })
                .ConfigureAwait(true);

            foreach (var vehicle in vehicles.Items)
            {
                Vehicles.Add(vehicle);
            }

            foreach (var driver in await _drivers.GetDriversAsync().ConfigureAwait(true))
            {
                Drivers.Add(driver);
            }
        }).ConfigureAwait(true);
    }
}
