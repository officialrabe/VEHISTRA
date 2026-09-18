using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vehistra.Application.Abstractions;
using Vehistra.Application.Dtos;
using Vehistra.Client.Services;
using Vehistra.Domain.Entities;
using Vehistra.Domain.Enums;

namespace Vehistra.Client.ViewModels.Dialogs;

/// <summary>Anlegen und Bearbeiten eines Werkstattvorgangs inklusive Rueckmeldung.</summary>
public sealed partial class WorkshopOrderEditViewModel : DialogViewModelBase
{
    private readonly IWorkshopService _workshop;
    private readonly IVehicleService _vehicles;
    private readonly IDriverService _drivers;
    private readonly IDamageService _damages;
    private readonly IReportGenerator _reports;
    private readonly IDialogService _dialogs;

    private WorkshopOrder? _entity;

    [ObservableProperty]
    private bool _isNew = true;

    [ObservableProperty]
    private string? _orderNumber;

    [ObservableProperty]
    private VehicleListItem? _selectedVehicle;

    [ObservableProperty]
    private Driver? _selectedDriver;

    [ObservableProperty]
    private Workshop? _selectedWorkshop;

    [ObservableProperty]
    private DateTime _createdOn = DateTime.Now;

    [ObservableProperty]
    private DateTime? _proposedDate;

    [ObservableProperty]
    private DateTime? _appointmentDate;

    [ObservableProperty]
    private string? _reason;

    [ObservableProperty]
    private string? _workToPerform;

    [ObservableProperty]
    private WorkshopOrderStatus _status = WorkshopOrderStatus.Geplant;

    [ObservableProperty]
    private DateTime? _vehicleHandedOverAt;

    [ObservableProperty]
    private DateTime? _plannedCompletionAt;

    [ObservableProperty]
    private DateTime? _completedAt;

    [ObservableProperty]
    private DateTime? _pickedUpAt;

    [ObservableProperty]
    private int? _mileageAtHandover;

    [ObservableProperty]
    private decimal? _costNet;

    [ObservableProperty]
    private decimal? _costGross;

    [ObservableProperty]
    private string? _invoiceNumber;

    [ObservableProperty]
    private int? _nextServiceMileage;

    [ObservableProperty]
    private string? _comment;

    [ObservableProperty]
    private string? _newTaskDescription;

    public WorkshopOrderEditViewModel(
        IWorkshopService workshop,
        IVehicleService vehicles,
        IDriverService drivers,
        IDamageService damages,
        IReportGenerator reports,
        IDialogService dialogs)
    {
        _workshop = workshop;
        _vehicles = vehicles;
        _drivers = drivers;
        _damages = damages;
        _reports = reports;
        _dialogs = dialogs;
    }

    public override string Title => IsNew ? "Werkstattauftrag anlegen" : $"Werkstattvorgang {OrderNumber}";

    public ObservableCollection<VehicleListItem> Vehicles { get; } = [];

    public ObservableCollection<Driver> Drivers { get; } = [];

    public ObservableCollection<Workshop> Workshops { get; } = [];

    public ObservableCollection<WorkshopTask> Tasks { get; } = [];

    public ObservableCollection<DamageSelection> OpenDamages { get; } = [];

    public IReadOnlyList<WorkshopOrderStatus> StatusValues { get; } = Enum.GetValues<WorkshopOrderStatus>();

    public async Task InitializeForNewAsync(int? vehicleId)
    {
        IsNew = true;
        await LoadLookupsAsync().ConfigureAwait(true);

        SelectedVehicle = Vehicles.FirstOrDefault(v => v.Id == vehicleId);
        CreatedOn = DateTime.Now;

        await LoadOpenDamagesAsync().ConfigureAwait(true);
    }

    public async Task InitializeForEditAsync(int orderId)
    {
        IsNew = false;
        await LoadLookupsAsync().ConfigureAwait(true);

        _entity = await _workshop.GetOrderAsync(orderId).ConfigureAwait(true);

        if (_entity is null)
        {
            ErrorMessage = "Der Werkstattvorgang wurde nicht gefunden.";
            return;
        }

        OrderNumber = _entity.OrderNumber;
        SelectedVehicle = Vehicles.FirstOrDefault(v => v.Id == _entity.VehicleId);
        SelectedDriver = Drivers.FirstOrDefault(d => d.Id == _entity.DriverId);
        SelectedWorkshop = Workshops.FirstOrDefault(w => w.Id == _entity.WorkshopId);
        CreatedOn = _entity.CreatedOn;
        ProposedDate = _entity.ProposedDate;
        AppointmentDate = _entity.AppointmentDate;
        Reason = _entity.Reason;
        WorkToPerform = _entity.WorkToPerform;
        Status = _entity.Status;
        VehicleHandedOverAt = _entity.VehicleHandedOverAt;
        PlannedCompletionAt = _entity.PlannedCompletionAt;
        CompletedAt = _entity.CompletedAt;
        PickedUpAt = _entity.PickedUpAt;
        MileageAtHandover = _entity.MileageAtHandover;
        CostNet = _entity.CostNet;
        CostGross = _entity.CostGross;
        InvoiceNumber = _entity.InvoiceNumber;
        NextServiceMileage = _entity.NextServiceMileage;
        Comment = _entity.Comment;

        Tasks.Clear();
        foreach (var task in _entity.Tasks.OrderBy(t => t.Position))
        {
            Tasks.Add(task);
        }

        await LoadOpenDamagesAsync().ConfigureAwait(true);
    }

    partial void OnSelectedVehicleChanged(VehicleListItem? value)
    {
        if (value is null)
        {
            return;
        }

        MileageAtHandover ??= value.CurrentMileage;
        _ = LoadOpenDamagesAsync();
    }

    [RelayCommand]
    private async Task AddTaskAsync()
    {
        if (string.IsNullOrWhiteSpace(NewTaskDescription))
        {
            return;
        }

        if (_entity is null)
        {
            _dialogs.ShowInformation(
                "Bitte speichern Sie den Vorgang zuerst, danach können Arbeiten ergänzt werden.",
                "Arbeit hinzufügen");
            return;
        }

        await RunAsync(async () =>
        {
            await _workshop.AddTaskAsync(new WorkshopTask
            {
                WorkshopOrderId = _entity.Id,
                Description = NewTaskDescription.Trim()
            }).ConfigureAwait(true);

            NewTaskDescription = null;
            await InitializeForEditAsync(_entity.Id).ConfigureAwait(true);
        }).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task ToggleTaskAsync(WorkshopTask? task)
    {
        if (task is null || _entity is null)
        {
            return;
        }

        await RunAsync(async () =>
        {
            task.IsCompleted = !task.IsCompleted;
            await _workshop.UpdateTaskAsync(task).ConfigureAwait(true);
            await InitializeForEditAsync(_entity.Id).ConfigureAwait(true);
        }).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task RemoveTaskAsync(WorkshopTask? task)
    {
        if (task is null || _entity is null)
        {
            return;
        }

        await RunAsync(async () =>
        {
            await _workshop.RemoveTaskAsync(task.Id).ConfigureAwait(true);
            await InitializeForEditAsync(_entity.Id).ConfigureAwait(true);
        }).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task PrintReportAsync()
    {
        if (_entity is null)
        {
            _dialogs.ShowInformation(
                "Bitte speichern Sie den Vorgang zuerst, damit der Bericht alle Angaben enthält.",
                "Werkstattbericht");
            return;
        }

        await RunAsync(async () =>
            await _reports.CreateWorkshopReportForOrderAsync(_entity.Id).ConfigureAwait(true)).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task RecordFeedbackAsync()
    {
        if (_entity is null)
        {
            return;
        }

        await RunAsync(async () =>
        {
            await _workshop.RecordFeedbackAsync(new WorkshopFeedback
            {
                OrderId = _entity.Id,
                CostNet = CostNet,
                CostGross = CostGross,
                CompletedAt = CompletedAt ?? DateTime.Now,
                NextServiceMileage = NextServiceMileage,
                InvoiceNumber = InvoiceNumber,
                MileageAtHandover = MileageAtHandover,
                CompletedTaskIds = Tasks.Where(t => t.IsCompleted).Select(t => t.Id).ToList()
            }).ConfigureAwait(true);

            await InitializeForEditAsync(_entity.Id).ConfigureAwait(true);
        }, "Die Rückmeldung wurde übernommen.").ConfigureAwait(true);
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
            var order = _entity ?? new WorkshopOrder();

            order.VehicleId = SelectedVehicle.Id;
            order.DriverId = SelectedDriver?.Id;
            order.WorkshopId = SelectedWorkshop?.Id;
            order.CreatedOn = CreatedOn;
            order.ProposedDate = ProposedDate;
            order.AppointmentDate = AppointmentDate;
            order.Reason = Reason?.Trim();
            order.WorkToPerform = WorkToPerform;
            order.Status = Status;
            order.VehicleHandedOverAt = VehicleHandedOverAt;
            order.PlannedCompletionAt = PlannedCompletionAt;
            order.CompletedAt = CompletedAt;
            order.PickedUpAt = PickedUpAt;
            order.MileageAtHandover = MileageAtHandover;
            order.CostNet = CostNet;
            order.CostGross = CostGross;
            order.InvoiceNumber = InvoiceNumber?.Trim();
            order.NextServiceMileage = NextServiceMileage;
            order.Comment = Comment;

            if (IsNew)
            {
                var damageIds = OpenDamages.Where(d => d.IsSelected).Select(d => d.Damage.Id).ToList();
                await _workshop.CreateOrderAsync(order, damageIds).ConfigureAwait(true);
            }
            else
            {
                await _workshop.UpdateOrderAsync(order).ConfigureAwait(true);
            }
        }).ConfigureAwait(true);
    }

    private async Task LoadOpenDamagesAsync()
    {
        OpenDamages.Clear();

        if (SelectedVehicle is null)
        {
            return;
        }

        await RunAsync(async () =>
        {
            var damages = await _damages.GetOpenForVehicleAsync(SelectedVehicle.Id).ConfigureAwait(true);

            foreach (var damage in damages)
            {
                OpenDamages.Add(new DamageSelection(damage));
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

            foreach (var workshop in await _workshop.GetWorkshopsAsync().ConfigureAwait(true))
            {
                Workshops.Add(workshop);
            }
        }).ConfigureAwait(true);
    }
}

/// <summary>Auswahl eines offenen Schadens beim Anlegen eines Werkstattauftrags.</summary>
public sealed partial class DamageSelection : ObservableObject
{
    public DamageSelection(DamageReport damage)
    {
        Damage = damage;
        IsSelected = true;
    }

    public DamageReport Damage { get; }

    public string Display => $"{Damage.DamageNumber}: {Damage.Description}";

    public string PriorityText => Damage.Priority.ToString();

    [ObservableProperty]
    private bool _isSelected;
}
