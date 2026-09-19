using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vehistra.Application.Abstractions;
using Vehistra.Application.Services;
using Vehistra.Client.Services;
using Vehistra.Client.ViewModels.Dialogs;
using Vehistra.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace Vehistra.Client.ViewModels;

/// <summary>
/// Systemeinstellungen: Unternehmensangaben, Warnfristen, Pfade, Darstellung,
/// Stammdaten (Kategorien, Werkstätten) und Sicherheit.
/// </summary>
public sealed partial class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsService _settings;
    private readonly IVehicleService _vehicles;
    private readonly IWorkshopService _workshop;
    private readonly IDamageService _damages;
    private readonly IConnectionSettingsStore _connectionStore;
    private readonly IDialogService _dialogs;
    private readonly IServiceProvider _services;

    [ObservableProperty]
    private string _companyName = string.Empty;

    [ObservableProperty]
    private string? _companyStreet;

    [ObservableProperty]
    private string? _companyPostalCode;

    [ObservableProperty]
    private string? _companyCity;

    [ObservableProperty]
    private string? _companyPhone;

    [ObservableProperty]
    private string? _companyEmail;

    [ObservableProperty]
    private string? _companyLogoPath;

    [ObservableProperty]
    private int _inspectionCriticalDays = 14;

    [ObservableProperty]
    private int _inspectionWarningDays = 30;

    [ObservableProperty]
    private int _maintenanceWarnDays = 30;

    [ObservableProperty]
    private int _maintenanceWarnKilometers = 1000;

    [ObservableProperty]
    private string _plateReservationWarnDays = "30;14;7;3;1";

    [ObservableProperty]
    private int _documentMaxFileSizeMb = 25;

    [ObservableProperty]
    private string? _documentsPath;

    [ObservableProperty]
    private string? _backupPath;

    [ObservableProperty]
    private string? _updatePath;

    [ObservableProperty]
    private string _dateFormat = "dd.MM.yyyy";

    [ObservableProperty]
    private int _maxFailedLogins = 5;

    [ObservableProperty]
    private int _lockoutMinutes = 15;

    [ObservableProperty]
    private int _passwordMinimumLength = 8;

    [ObservableProperty]
    private bool _checkUpdatesOnStart = true;

    [ObservableProperty]
    private string? _workshopReportNotice;

    [ObservableProperty]
    private string? _accidentReportNotice;

    [ObservableProperty]
    private Workshop? _selectedWorkshop;

    [ObservableProperty]
    private VehicleCategory? _selectedCategory;

    [ObservableProperty]
    private DamageCategory? _selectedDamageCategory;

    [ObservableProperty]
    private VehicleStatus? _selectedStatus;

    public SettingsViewModel(
        ISettingsService settings,
        IVehicleService vehicles,
        IWorkshopService workshop,
        IDamageService damages,
        IConnectionSettingsStore connectionStore,
        IDialogService dialogs,
        IServiceProvider services)
    {
        _settings = settings;
        _vehicles = vehicles;
        _workshop = workshop;
        _damages = damages;
        _connectionStore = connectionStore;
        _dialogs = dialogs;
        _services = services;
    }

    public override string Title => "Einstellungen";

    public override string? Subtitle =>
        "Unternehmensangaben, Warnfristen, Pfade und Stammdaten der Fuhrparkverwaltung";

    public ObservableCollection<VehicleCategory> Categories { get; } = [];

    public ObservableCollection<DamageCategory> DamageCategories { get; } = [];

    public ObservableCollection<VehicleStatus> Statuses { get; } = [];

    public ObservableCollection<Workshop> Workshops { get; } = [];

    public override async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        await RunAsync(async () =>
        {
            var company = await _settings.GetCompanyProfileAsync(cancellationToken).ConfigureAwait(true);

            CompanyName = company.Name;
            CompanyStreet = company.Street;
            CompanyPostalCode = company.PostalCode;
            CompanyCity = company.City;
            CompanyPhone = company.Phone;
            CompanyEmail = company.Email;
            CompanyLogoPath = company.LogoPath;

            InspectionCriticalDays = await _settings
                .GetIntAsync(SettingsKeys.InspectionWarnCriticalDays, 14, cancellationToken).ConfigureAwait(true);
            InspectionWarningDays = await _settings
                .GetIntAsync(SettingsKeys.InspectionWarnWarningDays, 30, cancellationToken).ConfigureAwait(true);
            MaintenanceWarnDays = await _settings
                .GetIntAsync(SettingsKeys.MaintenanceWarnDays, 30, cancellationToken).ConfigureAwait(true);
            MaintenanceWarnKilometers = await _settings
                .GetIntAsync(SettingsKeys.MaintenanceWarnKilometers, 1000, cancellationToken).ConfigureAwait(true);
            DocumentMaxFileSizeMb = await _settings
                .GetIntAsync(SettingsKeys.DocumentMaxFileSizeMb, 25, cancellationToken)
                .ConfigureAwait(true);

            PlateReservationWarnDays = await _settings
                .GetOrDefaultAsync(SettingsKeys.PlateReservationWarnDays, "30;14;7;3;1", cancellationToken)
                .ConfigureAwait(true);

            var connection = _connectionStore.Load();
            DocumentsPath = connection?.DocumentsPath
                ?? await _settings.GetAsync(SettingsKeys.DocumentsPath, cancellationToken).ConfigureAwait(true);
            BackupPath = connection?.BackupPath
                ?? await _settings.GetAsync(SettingsKeys.BackupPath, cancellationToken).ConfigureAwait(true);
            UpdatePath = connection?.UpdatePath
                ?? await _settings.GetAsync(SettingsKeys.UpdatePath, cancellationToken).ConfigureAwait(true);

            DateFormat = await _settings
                .GetOrDefaultAsync(SettingsKeys.DateFormat, "dd.MM.yyyy", cancellationToken).ConfigureAwait(true);
            MaxFailedLogins = await _settings
                .GetIntAsync(SettingsKeys.LoginMaxFailedAttempts, 5, cancellationToken).ConfigureAwait(true);
            LockoutMinutes = await _settings
                .GetIntAsync(SettingsKeys.LoginLockoutMinutes, 15, cancellationToken).ConfigureAwait(true);
            PasswordMinimumLength = await _settings
                .GetIntAsync(SettingsKeys.PasswordMinimumLength, 8, cancellationToken).ConfigureAwait(true);
            CheckUpdatesOnStart = await _settings
                .GetBoolAsync(SettingsKeys.CheckForUpdatesOnStart, true, cancellationToken).ConfigureAwait(true);

            WorkshopReportNotice = await _settings
                .GetAsync(SettingsKeys.WorkshopReportNotice, cancellationToken).ConfigureAwait(true);
            AccidentReportNotice = await _settings
                .GetAsync(SettingsKeys.AccidentReportNotice, cancellationToken).ConfigureAwait(true);

            Categories.Clear();
            foreach (var category in await _vehicles.GetCategoriesAsync(true, cancellationToken).ConfigureAwait(true))
            {
                Categories.Add(category);
            }

            DamageCategories.Clear();
            foreach (var category in await _damages.GetCategoriesAsync(true, cancellationToken).ConfigureAwait(true))
            {
                DamageCategories.Add(category);
            }

            Statuses.Clear();
            foreach (var status in await _vehicles.GetStatusesAsync(true, cancellationToken).ConfigureAwait(true))
            {
                Statuses.Add(status);
            }

            Workshops.Clear();
            foreach (var workshop in await _workshop.GetWorkshopsAsync(true, cancellationToken).ConfigureAwait(true))
            {
                Workshops.Add(workshop);
            }
        }).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        await RunAsync(async () =>
        {
            await _settings.SetAsync(SettingsKeys.CompanyName, CompanyName).ConfigureAwait(true);
            await _settings.SetAsync(SettingsKeys.CompanyStreet, CompanyStreet).ConfigureAwait(true);
            await _settings.SetAsync(SettingsKeys.CompanyPostalCode, CompanyPostalCode).ConfigureAwait(true);
            await _settings.SetAsync(SettingsKeys.CompanyCity, CompanyCity).ConfigureAwait(true);
            await _settings.SetAsync(SettingsKeys.CompanyPhone, CompanyPhone).ConfigureAwait(true);
            await _settings.SetAsync(SettingsKeys.CompanyEmail, CompanyEmail).ConfigureAwait(true);
            await _settings.SetAsync(SettingsKeys.CompanyLogoPath, CompanyLogoPath).ConfigureAwait(true);

            await _settings.SetAsync(SettingsKeys.InspectionWarnCriticalDays, InspectionCriticalDays.ToString())
                .ConfigureAwait(true);
            await _settings.SetAsync(SettingsKeys.InspectionWarnWarningDays, InspectionWarningDays.ToString())
                .ConfigureAwait(true);
            await _settings.SetAsync(SettingsKeys.MaintenanceWarnDays, MaintenanceWarnDays.ToString())
                .ConfigureAwait(true);
            await _settings.SetAsync(SettingsKeys.MaintenanceWarnKilometers, MaintenanceWarnKilometers.ToString())
                .ConfigureAwait(true);
            // Mehr als die harte Grenze der Ablage zu erlauben, waere eine
            // Zusage, die das Programm nicht halten kann.
            var grenze = Math.Clamp(DocumentMaxFileSizeMb, 1, 50);

            if (grenze != DocumentMaxFileSizeMb)
            {
                DocumentMaxFileSizeMb = grenze;
                StatusMessage = "Die Dateigröße wurde auf den zulässigen Bereich (1 bis 50 MB) gesetzt.";
            }

            await _settings.SetAsync(SettingsKeys.DocumentMaxFileSizeMb, grenze.ToString())
                .ConfigureAwait(true);

            await _settings.SetAsync(SettingsKeys.PlateReservationWarnDays, PlateReservationWarnDays)
                .ConfigureAwait(true);

            await _settings.SetAsync(SettingsKeys.DocumentsPath, DocumentsPath).ConfigureAwait(true);
            await _settings.SetAsync(SettingsKeys.BackupPath, BackupPath).ConfigureAwait(true);
            await _settings.SetAsync(SettingsKeys.UpdatePath, UpdatePath).ConfigureAwait(true);

            await _settings.SetAsync(SettingsKeys.DateFormat, DateFormat).ConfigureAwait(true);
            await _settings.SetAsync(SettingsKeys.LoginMaxFailedAttempts, MaxFailedLogins.ToString())
                .ConfigureAwait(true);
            await _settings.SetAsync(SettingsKeys.LoginLockoutMinutes, LockoutMinutes.ToString()).ConfigureAwait(true);
            await _settings.SetAsync(SettingsKeys.PasswordMinimumLength, PasswordMinimumLength.ToString())
                .ConfigureAwait(true);
            await _settings.SetAsync(SettingsKeys.CheckForUpdatesOnStart, CheckUpdatesOnStart.ToString())
                .ConfigureAwait(true);

            await _settings.SetAsync(SettingsKeys.WorkshopReportNotice, WorkshopReportNotice).ConfigureAwait(true);
            await _settings.SetAsync(SettingsKeys.AccidentReportNotice, AccidentReportNotice).ConfigureAwait(true);

            _settings.InvalidateCache();

            // Die Dokumentenablage der Serververbindung mitpflegen, damit Client und
            // Einstellungen nicht auseinanderlaufen.
            var connection = _connectionStore.Load();
            if (connection is not null)
            {
                connection.DocumentsPath = DocumentsPath;
                connection.BackupPath = BackupPath;
                connection.UpdatePath = UpdatePath;
                _connectionStore.Save(connection);
            }
        }, "Die Einstellungen wurden gespeichert.").ConfigureAwait(true);
    }

    [RelayCommand]
    private void BrowseLogo()
    {
        var path = _dialogs.OpenFile(
            "Bilddateien (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg|Alle Dateien (*.*)|*.*",
            "Firmenlogo auswählen");

        if (!string.IsNullOrWhiteSpace(path))
        {
            CompanyLogoPath = path;
        }
    }

    [RelayCommand]
    private void BrowseDocuments() => DocumentsPath = _dialogs.SelectFolder("Dokumentenablage auswählen") ?? DocumentsPath;

    [RelayCommand]
    private void BrowseUpdates() => UpdatePath = _dialogs.SelectFolder("Updateablage auswählen") ?? UpdatePath;

    [RelayCommand]
    private async Task CreateCategoryAsync()
    {
        var name = _dialogs.Prompt(
            "Name des Einsatzbereichs, zum Beispiel „Winterdienst“ oder „Werkstattwagen“.",
            "Neuer Einsatzbereich");

        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        await RunAsync(async () =>
        {
            await _vehicles.CreateCategoryAsync(name).ConfigureAwait(true);
            await LoadAsync().ConfigureAwait(true);
        }, $"Der Einsatzbereich „{name.Trim()}“ wurde angelegt.").ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task RenameCategoryAsync()
    {
        if (SelectedCategory is null)
        {
            return;
        }

        var category = SelectedCategory;
        var name = _dialogs.Prompt("Neuer Name des Einsatzbereichs", "Umbenennen", category.Name);

        if (string.IsNullOrWhiteSpace(name) || name.Trim() == category.Name)
        {
            return;
        }

        await RunAsync(async () =>
        {
            category.Name = name;
            await _vehicles.UpdateCategoryAsync(category).ConfigureAwait(true);
            await LoadAsync().ConfigureAwait(true);
        }, "Der Einsatzbereich wurde umbenannt.").ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task ToggleCategoryAsync()
    {
        if (SelectedCategory is null)
        {
            return;
        }

        var category = SelectedCategory;
        var einschalten = !category.IsActive;

        await RunAsync(async () =>
        {
            category.IsActive = einschalten;
            await _vehicles.UpdateCategoryAsync(category).ConfigureAwait(true);
            await LoadAsync().ConfigureAwait(true);
        }, einschalten
            ? $"Der Einsatzbereich „{category.Name}“ steht wieder zur Auswahl."
            : $"Der Einsatzbereich „{category.Name}“ ist stillgelegt und erscheint nicht mehr zur Auswahl. " +
              "Bereits zugeordnete Fahrzeuge behalten ihn.").ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task DeleteCategoryAsync()
    {
        if (SelectedCategory is null)
        {
            return;
        }

        var category = SelectedCategory;

        if (!_dialogs.Confirm(
                $"Soll der Einsatzbereich „{category.Name}“ endgültig gelöscht werden?" +
                Environment.NewLine + Environment.NewLine +
                "Ist er noch einem Fahrzeug oder einer Wartungsregel zugeordnet, bleibt er erhalten. " +
                "Zum Ausblenden genügt „Stilllegen“.",
                "Einsatzbereich löschen"))
        {
            return;
        }

        await RunAsync(async () =>
        {
            await _vehicles.DeleteCategoryAsync(category.Id).ConfigureAwait(true);
            await LoadAsync().ConfigureAwait(true);
        }, $"Der Einsatzbereich „{category.Name}“ wurde gelöscht.").ConfigureAwait(true);
    }

    // ----- Schadenskategorien -------------------------------------------------

    [RelayCommand]
    private async Task CreateDamageCategoryAsync()
    {
        var name = _dialogs.Prompt(
            "Name der Schadenskategorie, zum Beispiel „Hagelschaden“ oder „Vandalismus“.",
            "Neue Schadenskategorie");

        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        await RunAsync(async () =>
        {
            await _damages.CreateCategoryAsync(name).ConfigureAwait(true);
            await LoadAsync().ConfigureAwait(true);
        }, $"Die Schadenskategorie „{name.Trim()}“ wurde angelegt.").ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task RenameDamageCategoryAsync()
    {
        if (SelectedDamageCategory is null)
        {
            return;
        }

        var category = SelectedDamageCategory;

        if (category.IsSystemCategory)
        {
            // Der Dienst weist das ohnehin ab; hier erklaert es sich ohne Fehlermeldung.
            _dialogs.ShowInformation(
                $"Die mitgelieferte Kategorie „{category.Name}“ kann nicht umbenannt werden, " +
                "weil das Programm sie über ihren Namen findet." + Environment.NewLine + Environment.NewLine +
                "Sie können sie stilllegen und eine eigene Kategorie anlegen.",
                "Mitgelieferte Kategorie");
            return;
        }

        var name = _dialogs.Prompt("Neuer Name der Schadenskategorie", "Umbenennen", category.Name);

        if (string.IsNullOrWhiteSpace(name) || name.Trim() == category.Name)
        {
            return;
        }

        await RunAsync(async () =>
        {
            category.Name = name;
            await _damages.UpdateCategoryAsync(category).ConfigureAwait(true);
            await LoadAsync().ConfigureAwait(true);
        }, "Die Schadenskategorie wurde umbenannt.").ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task ToggleDamageCategoryAsync()
    {
        if (SelectedDamageCategory is null)
        {
            return;
        }

        var category = SelectedDamageCategory;
        var einschalten = !category.IsActive;

        await RunAsync(async () =>
        {
            category.IsActive = einschalten;
            await _damages.UpdateCategoryAsync(category).ConfigureAwait(true);
            await LoadAsync().ConfigureAwait(true);
        }, einschalten
            ? $"Die Kategorie „{category.Name}“ steht wieder zur Auswahl."
            : $"Die Kategorie „{category.Name}“ ist stillgelegt. Bereits erfasste Schäden behalten sie.")
            .ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task DeleteDamageCategoryAsync()
    {
        if (SelectedDamageCategory is null)
        {
            return;
        }

        var category = SelectedDamageCategory;

        if (!_dialogs.Confirm(
                $"Soll die Schadenskategorie „{category.Name}“ endgültig gelöscht werden?" +
                Environment.NewLine + Environment.NewLine +
                "Ist sie noch einer Schadensmeldung zugeordnet, bleibt sie erhalten. " +
                "Zum Ausblenden genügt „Stilllegen“.",
                "Schadenskategorie löschen"))
        {
            return;
        }

        await RunAsync(async () =>
        {
            await _damages.DeleteCategoryAsync(category.Id).ConfigureAwait(true);
            await LoadAsync().ConfigureAwait(true);
        }, $"Die Schadenskategorie „{category.Name}“ wurde gelöscht.").ConfigureAwait(true);
    }

    // ----- Fahrzeugstatus -----------------------------------------------------

    [RelayCommand]
    private async Task CreateStatusAsync()
    {
        var name = _dialogs.Prompt(
            "Name des Status, zum Beispiel „Verleih“ oder „Saisonpause“." + Environment.NewLine +
            "Ob Fahrzeuge in diesem Status als einsatzbereit oder verfügbar zählen, " +
            "legen Sie anschließend über die Schaltflächen fest.",
            "Neuer Fahrzeugstatus");

        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        await RunAsync(async () =>
        {
            await _vehicles.CreateStatusAsync(name).ConfigureAwait(true);
            await LoadAsync().ConfigureAwait(true);
        }, $"Der Status „{name.Trim()}“ wurde angelegt.").ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task RenameStatusAsync()
    {
        if (SelectedStatus is null)
        {
            return;
        }

        var status = SelectedStatus;
        var name = _dialogs.Prompt("Neuer Name des Status", "Umbenennen", status.Name);

        if (string.IsNullOrWhiteSpace(name) || name.Trim() == status.Name)
        {
            return;
        }

        await RunAsync(async () =>
        {
            status.Name = name;
            await _vehicles.UpdateStatusAsync(status).ConfigureAwait(true);
            await LoadAsync().ConfigureAwait(true);
        }, "Der Status wurde umbenannt. Die zugehörigen Abläufe bleiben unverändert.").ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task ToggleStatusAsync()
    {
        if (SelectedStatus is null)
        {
            return;
        }

        var status = SelectedStatus;
        var einschalten = !status.IsActive;

        await RunAsync(async () =>
        {
            status.IsActive = einschalten;
            await _vehicles.UpdateStatusAsync(status).ConfigureAwait(true);
            await LoadAsync().ConfigureAwait(true);
        }, einschalten
            ? $"Der Status „{status.Name}“ steht wieder zur Auswahl."
            : $"Der Status „{status.Name}“ ist stillgelegt. Fahrzeuge mit diesem Status behalten ihn.")
            .ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task ToggleStatusOperationalAsync()
    {
        if (SelectedStatus is null)
        {
            return;
        }

        var status = SelectedStatus;
        status.CountsAsOperational = !status.CountsAsOperational;

        await RunAsync(async () =>
        {
            await _vehicles.UpdateStatusAsync(status).ConfigureAwait(true);
            await LoadAsync().ConfigureAwait(true);
        }, status.CountsAsOperational
            ? $"Fahrzeuge im Status „{status.Name}“ zählen jetzt als einsatzbereit."
            : $"Fahrzeuge im Status „{status.Name}“ zählen nicht mehr als einsatzbereit.")
            .ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task ToggleStatusAvailableAsync()
    {
        if (SelectedStatus is null)
        {
            return;
        }

        var status = SelectedStatus;
        status.CountsAsAvailable = !status.CountsAsAvailable;

        await RunAsync(async () =>
        {
            await _vehicles.UpdateStatusAsync(status).ConfigureAwait(true);
            await LoadAsync().ConfigureAwait(true);
        }, status.CountsAsAvailable
            ? $"Fahrzeuge im Status „{status.Name}“ zählen jetzt als verfügbar."
            : $"Fahrzeuge im Status „{status.Name}“ zählen nicht mehr als verfügbar.")
            .ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task DeleteStatusAsync()
    {
        if (SelectedStatus is null)
        {
            return;
        }

        var status = SelectedStatus;

        if (!_dialogs.Confirm(
                $"Soll der Fahrzeugstatus „{status.Name}“ endgültig gelöscht werden?" +
                Environment.NewLine + Environment.NewLine +
                "Mitgelieferte Status und solche, die noch an Fahrzeugen oder in der Statushistorie " +
                "vorkommen, bleiben erhalten. Zum Ausblenden genügt „Stilllegen“.",
                "Fahrzeugstatus löschen"))
        {
            return;
        }

        await RunAsync(async () =>
        {
            await _vehicles.DeleteStatusAsync(status.Id).ConfigureAwait(true);
            await LoadAsync().ConfigureAwait(true);
        }, $"Der Status „{status.Name}“ wurde gelöscht.").ConfigureAwait(true);
    }

    // ----- Werkstätten --------------------------------------------------------

    [RelayCommand]
    private async Task CreateWorkshopAsync()
    {
        var name = _dialogs.Prompt("Name der Werkstatt", "Neue Werkstatt");

        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        await RunAsync(async () =>
        {
            await _workshop.CreateWorkshopAsync(new Workshop { Name = name, IsActive = true }).ConfigureAwait(true);
            await LoadAsync().ConfigureAwait(true);
        }, "Die Werkstatt wurde angelegt.").ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task ToggleWorkshopAsync()
    {
        if (SelectedWorkshop is null)
        {
            return;
        }

        var workshop = SelectedWorkshop;
        workshop.IsActive = !workshop.IsActive;

        await RunAsync(async () =>
        {
            await _workshop.UpdateWorkshopAsync(workshop).ConfigureAwait(true);
            await LoadAsync().ConfigureAwait(true);
        }).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync().ConfigureAwait(true);
}
