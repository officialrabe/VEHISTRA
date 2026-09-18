using System.IO;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fuhrpark.Application.Abstractions;
using Fuhrpark.Application.Dtos;
using Fuhrpark.Client.Services;
using Fuhrpark.Domain.Entities;
using Fuhrpark.Domain.Enums;

namespace Fuhrpark.Client.ViewModels.Dialogs;

/// <summary>Melden und Bearbeiten eines Schadens.</summary>
public sealed partial class DamageEditViewModel : DialogViewModelBase
{
    private readonly IDamageService _damages;
    private readonly IVehicleService _vehicles;
    private readonly IDriverService _drivers;
    private readonly IWorkshopService _workshop;
    private readonly IDocumentService _documents;
    private readonly IDialogService _dialogs;

    private DamageReport? _entity;

    [ObservableProperty]
    private bool _isNew = true;

    [ObservableProperty]
    private string? _damageNumber;

    [ObservableProperty]
    private VehicleListItem? _selectedVehicle;

    [ObservableProperty]
    private Driver? _selectedDriver;

    [ObservableProperty]
    private DateTime _occurredAt = DateTime.Now;

    [ObservableProperty]
    private int? _mileage;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private DamageArea _area = DamageArea.Unbekannt;

    [ObservableProperty]
    private DamageCategory? _selectedCategory;

    [ObservableProperty]
    private DamagePriority _priority = DamagePriority.Normal;

    [ObservableProperty]
    private bool _isDriveable = true;

    [ObservableProperty]
    private bool _repairRequired = true;

    [ObservableProperty]
    private DamageStatus _status = DamageStatus.Gemeldet;

    [ObservableProperty]
    private Workshop? _selectedWorkshop;

    [ObservableProperty]
    private DateTime? _repairedAt;

    [ObservableProperty]
    private decimal? _costEstimate;

    [ObservableProperty]
    private decimal? _costActual;

    [ObservableProperty]
    private bool _isInsuranceCase;

    [ObservableProperty]
    private string? _insuranceClaimNumber;

    [ObservableProperty]
    private string? _comment;

    public DamageEditViewModel(
        IDamageService damages,
        IVehicleService vehicles,
        IDriverService drivers,
        IWorkshopService workshop,
        IDocumentService documents,
        IDialogService dialogs)
    {
        _damages = damages;
        _vehicles = vehicles;
        _drivers = drivers;
        _workshop = workshop;
        _documents = documents;
        _dialogs = dialogs;
    }

    public override string Title => IsNew ? "Schaden melden" : $"Schaden {DamageNumber} bearbeiten";

    public ObservableCollection<VehicleListItem> Vehicles { get; } = [];

    public ObservableCollection<Driver> Drivers { get; } = [];

    public ObservableCollection<DamageCategory> Categories { get; } = [];

    public ObservableCollection<Workshop> Workshops { get; } = [];

    public ObservableCollection<DocumentListItem> Attachments { get; } = [];

    public IReadOnlyList<DamageArea> Areas { get; } = Enum.GetValues<DamageArea>();

    public IReadOnlyList<DamagePriority> Priorities { get; } = Enum.GetValues<DamagePriority>();

    public IReadOnlyList<DamageStatus> StatusValues { get; } = Enum.GetValues<DamageStatus>();

    public async Task InitializeForNewAsync(int? vehicleId)
    {
        IsNew = true;
        await LoadLookupsAsync().ConfigureAwait(true);

        SelectedVehicle = Vehicles.FirstOrDefault(v => v.Id == vehicleId);
        Mileage = SelectedVehicle?.CurrentMileage;
    }

    public async Task InitializeForEditAsync(int damageId)
    {
        IsNew = false;
        await LoadLookupsAsync().ConfigureAwait(true);

        _entity = await _damages.GetAsync(damageId).ConfigureAwait(true);

        if (_entity is null)
        {
            ErrorMessage = "Der Schaden wurde nicht gefunden.";
            return;
        }

        DamageNumber = _entity.DamageNumber;
        SelectedVehicle = Vehicles.FirstOrDefault(v => v.Id == _entity.VehicleId);
        SelectedDriver = Drivers.FirstOrDefault(d => d.Id == _entity.DriverId);
        OccurredAt = _entity.OccurredAt;
        Mileage = _entity.Mileage;
        Description = _entity.Description;
        Area = _entity.Area;
        SelectedCategory = Categories.FirstOrDefault(c => c.Id == _entity.DamageCategoryId);
        Priority = _entity.Priority;
        IsDriveable = _entity.IsDriveable;
        RepairRequired = _entity.RepairRequired;
        Status = _entity.Status;
        SelectedWorkshop = Workshops.FirstOrDefault(w => w.Id == _entity.WorkshopId);
        RepairedAt = _entity.RepairedAt;
        CostEstimate = _entity.CostEstimate;
        CostActual = _entity.CostActual;
        IsInsuranceCase = _entity.IsInsuranceCase;
        InsuranceClaimNumber = _entity.InsuranceClaimNumber;
        Comment = _entity.Comment;

        Attachments.Clear();
        foreach (var attachment in _entity.Attachments.Where(a => a.Document is not null))
        {
            var document = attachment.Document!;
            Attachments.Add(new DocumentListItem(
                document.Id, document.VehicleId, null, document.Category, document.Title,
                document.OriginalFileName, document.FileSizeBytes, document.DocumentDate,
                document.CreatedAt, document.CreatedByUserName, document.RelativePath));
        }
    }

    partial void OnSelectedVehicleChanged(VehicleListItem? value)
    {
        if (IsNew && value is not null)
        {
            Mileage = value.CurrentMileage;
        }
    }

    [RelayCommand]
    private async Task AddPhotoAsync()
    {
        if (_entity is null || SelectedVehicle is null)
        {
            _dialogs.ShowInformation(
                "Bitte speichern Sie den Schaden zuerst, damit Fotos zugeordnet werden können.",
                "Fotos hinzufügen");
            return;
        }

        var files = _dialogs.OpenFiles(
            "Bilder und Dokumente|*.jpg;*.jpeg;*.png;*.pdf|Alle Dateien (*.*)|*.*",
            "Fotos zum Schaden auswählen");

        if (files.Count == 0)
        {
            return;
        }

        await RunAsync(async () =>
        {
            foreach (var file in files)
            {
                var documentId = await _documents.AddFromFileAsync(file, new VehicleDocument
                {
                    VehicleId = SelectedVehicle.Id,
                    Category = DocumentCategory.Schadensbild,
                    Title = $"Schadensbild {DamageNumber}",
                    DocumentDate = OccurredAt.Date
                }).ConfigureAwait(true);

                await _documents.LinkToDamageAsync(documentId, _entity.Id, Path.GetFileName(file))
                    .ConfigureAwait(true);
            }

            await InitializeForEditAsync(_entity.Id).ConfigureAwait(true);
        }, "Die Dateien wurden hinzugefügt.").ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task OpenAttachmentAsync(DocumentListItem? item)
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

    protected override async Task<bool> SaveAsync()
    {
        if (SelectedVehicle is null)
        {
            ErrorMessage = "Bitte wählen Sie ein Fahrzeug aus.";
            return false;
        }

        return await RunAsync(async () =>
        {
            var damage = _entity ?? new DamageReport();

            damage.VehicleId = SelectedVehicle.Id;
            damage.DriverId = SelectedDriver?.Id;
            damage.OccurredAt = OccurredAt;
            damage.Mileage = Mileage;
            damage.Description = Description?.Trim() ?? string.Empty;
            damage.Area = Area;
            damage.DamageCategoryId = SelectedCategory?.Id;
            damage.Priority = Priority;
            damage.IsDriveable = IsDriveable;
            damage.RepairRequired = RepairRequired;
            damage.Status = Status;
            damage.WorkshopId = SelectedWorkshop?.Id;
            damage.RepairedAt = RepairedAt;
            damage.CostEstimate = CostEstimate;
            damage.CostActual = CostActual;
            damage.IsInsuranceCase = IsInsuranceCase;
            damage.InsuranceClaimNumber = InsuranceClaimNumber?.Trim();
            damage.Comment = Comment;

            if (IsNew)
            {
                await _damages.CreateAsync(damage).ConfigureAwait(true);
            }
            else
            {
                await _damages.UpdateAsync(damage).ConfigureAwait(true);
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

            foreach (var category in await _damages.GetCategoriesAsync().ConfigureAwait(true))
            {
                Categories.Add(category);
            }

            foreach (var workshop in await _workshop.GetWorkshopsAsync().ConfigureAwait(true))
            {
                Workshops.Add(workshop);
            }
        }).ConfigureAwait(true);
    }
}
