using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vehistra.Application.Abstractions;
using Vehistra.Application.Dtos;
using Vehistra.Application.Services;
using Vehistra.Client.Services;
using Vehistra.Domain.Security;
using Microsoft.Extensions.Logging;

namespace Vehistra.Client.ViewModels;

/// <summary>Ansichtsmodell des Hauptfensters: Navigation, globale Suche und Benachrichtigungen.</summary>
public sealed partial class ShellViewModel : ViewModelBase
{
    private readonly INavigationService _navigation;
    private readonly ICurrentUserService _currentUser;
    private readonly ISettingsService _settings;
    private readonly ISearchService _search;
    private readonly INotificationService _notifications;
    private readonly IUpdateService _updates;
    private readonly IDialogService _dialogs;
    private readonly AppShellState _state;
    private readonly ILogger<ShellViewModel> _logger;

    private CancellationTokenSource? _searchCancellation;

    [ObservableProperty]
    private NavigationItem? _selectedItem;

    [ObservableProperty]
    private ViewModelBase? _currentView;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _isSearchOpen;

    [ObservableProperty]
    private string _companyName = "Vehistra";

    [ObservableProperty]
    private string _userDisplay = string.Empty;

    [ObservableProperty]
    private string _roleDisplay = string.Empty;

    [ObservableProperty]
    private int _unreadNotifications;

    [ObservableProperty]
    private string? _updateBanner;

    public ShellViewModel(
        INavigationService navigation,
        ICurrentUserService currentUser,
        ISettingsService settings,
        ISearchService search,
        INotificationService notifications,
        IUpdateService updates,
        IDialogService dialogs,
        AppShellState state,
        ILogger<ShellViewModel> logger)
    {
        _navigation = navigation;
        _currentUser = currentUser;
        _settings = settings;
        _search = search;
        _notifications = notifications;
        _updates = updates;
        _dialogs = dialogs;
        _state = state;
        _logger = logger;

        _navigation.CurrentChanged += (_, viewModel) => CurrentView = viewModel;

        BuildNavigation();
    }

    public ObservableCollection<NavigationItem> NavigationItems { get; } = [];

    public ObservableCollection<SearchResultItem> SearchResults { get; } = [];

    public ObservableCollection<NotificationListItem> Notifications { get; } = [];

    public string Version => typeof(App).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";

    public event EventHandler? LogoutRequested;

    public override async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var user = _currentUser.User;

        if (user is not null)
        {
            UserDisplay = user.DisplayName;
            RoleDisplay = string.Join(", ", user.Roles.Select(EnumDisplay));
        }

        await RunAsync(async () =>
        {
            var company = await _settings.GetCompanyProfileAsync(cancellationToken).ConfigureAwait(true);
            CompanyName = string.IsNullOrWhiteSpace(company.Name) ? "Vehistra" : company.Name;
            _state.CompanyName = CompanyName;

            await _notifications.RefreshDueNotificationsAsync(cancellationToken).ConfigureAwait(true);
            await RefreshNotificationsAsync(cancellationToken).ConfigureAwait(true);

            if (await _settings.GetBoolAsync(SettingsKeys.CheckForUpdatesOnStart, true, cancellationToken)
                    .ConfigureAwait(true))
            {
                await CheckForUpdateAsync(cancellationToken).ConfigureAwait(true);
            }
        }).ConfigureAwait(true);

        // Startansicht
        var first = NavigationItems.FirstOrDefault(i => i.IsEnabled && !i.IsSection);
        if (first is not null)
        {
            await SelectAsync(first).ConfigureAwait(true);
        }
    }

    [RelayCommand]
    public async Task SelectAsync(NavigationItem? item)
    {
        if (item is null || item.IsSection || !item.IsEnabled)
        {
            return;
        }

        foreach (var navigationItem in NavigationItems)
        {
            navigationItem.IsSelected = ReferenceEquals(navigationItem, item);
        }

        SelectedItem = item;
        IsSearchOpen = false;

        await _navigation.NavigateToAsync(item.ViewModelType!).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        _searchCancellation?.Cancel();
        _searchCancellation = new CancellationTokenSource();
        var token = _searchCancellation.Token;

        if (string.IsNullOrWhiteSpace(SearchText) || SearchText.Trim().Length < 2)
        {
            SearchResults.Clear();
            IsSearchOpen = false;
            return;
        }

        try
        {
            // Kurze Verzoegerung, damit nicht bei jedem Tastendruck abgefragt wird.
            await Task.Delay(220, token).ConfigureAwait(true);

            var results = await _search.SearchAsync(SearchText, 8, token).ConfigureAwait(true);

            SearchResults.Clear();
            foreach (var result in results)
            {
                SearchResults.Add(result);
            }

            IsSearchOpen = SearchResults.Count > 0;
        }
        catch (OperationCanceledException)
        {
            // Eine neue Eingabe hat die Suche ersetzt.
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Die globale Suche ist fehlgeschlagen.");
        }
    }

    [RelayCommand]
    private async Task OpenSearchResultAsync(SearchResultItem? item)
    {
        if (item is null)
        {
            return;
        }

        IsSearchOpen = false;
        SearchText = string.Empty;
        SearchResults.Clear();

        await _navigation.OpenSearchResultAsync(item.EntityType, item.EntityId, item.VehicleId).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task MarkNotificationReadAsync(NotificationListItem? item)
    {
        if (item is null)
        {
            return;
        }

        await RunAsync(async () =>
        {
            await _notifications.MarkAsReadAsync(item.Id).ConfigureAwait(true);
            await RefreshNotificationsAsync().ConfigureAwait(true);

            if (item.VehicleId is { } vehicleId)
            {
                await _navigation.OpenVehicleAsync(vehicleId).ConfigureAwait(true);
            }
        }).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task MarkAllNotificationsReadAsync()
    {
        await RunAsync(async () =>
        {
            await _notifications.MarkAllAsReadAsync().ConfigureAwait(true);
            await RefreshNotificationsAsync().ConfigureAwait(true);
        }).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (CurrentView is not null)
        {
            await CurrentView.LoadAsync().ConfigureAwait(true);
        }

        await RefreshNotificationsAsync().ConfigureAwait(true);
    }

    /// <summary>Blendet die Fehlerleiste der aktuellen Ansicht aus.</summary>
    [RelayCommand]
    private void DismissError()
    {
        if (CurrentView is not null)
        {
            CurrentView.ErrorMessage = null;
        }
    }

    [RelayCommand]
    private void Logout() => LogoutRequested?.Invoke(this, EventArgs.Empty);

    private async Task RefreshNotificationsAsync(CancellationToken cancellationToken = default)
    {
        var items = await _notifications.GetForCurrentUserAsync(false, 50, cancellationToken).ConfigureAwait(true);

        Notifications.Clear();
        foreach (var item in items)
        {
            Notifications.Add(item);
        }

        UnreadNotifications = await _notifications.GetUnreadCountAsync(cancellationToken).ConfigureAwait(true);
        _state.UnreadNotifications = UnreadNotifications;
    }

    private async Task CheckForUpdateAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await _updates.CheckForUpdateAsync(cancellationToken: cancellationToken).ConfigureAwait(true);

            if (!result.IsUpdateAvailable)
            {
                return;
            }

            _state.AvailableUpdateVersion = result.AvailableVersion;
            _state.IsUpdateMandatory = result.IsMandatory;

            UpdateBanner = result.IsMandatory
                ? $"Pflichtupdate auf Version {result.AvailableVersion} verfügbar – bitte über „Updates“ installieren."
                : $"Update auf Version {result.AvailableVersion} verfügbar.";

            if (result.IsMandatory)
            {
                _dialogs.ShowWarning(
                    "Diese Version muss aktualisiert werden, bevor die Anwendung weiter verwendet werden kann." +
                    Environment.NewLine + Environment.NewLine +
                    $"Installiert: {result.InstalledVersion}{Environment.NewLine}" +
                    $"Verfügbar: {result.AvailableVersion}",
                    "Pflichtupdate verfügbar");
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Die Updatepruefung ist fehlgeschlagen.");
        }
    }

    /// <summary>Baut die Seitenleiste auf und blendet Bereiche ohne Berechtigung aus.</summary>
    private void BuildNavigation()
    {
        void Add(string title, string icon, Type viewModelType, string? permission = null)
        {
            var allowed = permission is null || _currentUser.HasPermission(permission);

            if (!allowed)
            {
                return;
            }

            NavigationItems.Add(new NavigationItem(title, icon, viewModelType) { IsEnabled = true });
        }

        void AddSection(string title)
        {
            NavigationItems.Add(NavigationItem.Section(title));
        }

        Add("Dashboard", "", typeof(DashboardViewModel), Permissions.VehicleView);
        Add("Fahrzeuge", "", typeof(VehicleListViewModel), Permissions.VehicleView);
        Add("Fahrer", "", typeof(DriverViewModel), Permissions.DriverView);
        Add("TÜV & Fristen", "", typeof(InspectionViewModel), Permissions.InspectionView);
        Add("Wartung", "", typeof(MaintenanceViewModel), Permissions.MaintenanceView);
        Add("Schäden", "", typeof(DamageViewModel), Permissions.DamageView);
        Add("Unfälle", "", typeof(AccidentViewModel), Permissions.AccidentView);
        Add("Werkstatt", "", typeof(WorkshopViewModel), Permissions.WorkshopView);
        Add("Kennzeichen", "", typeof(LicensePlateViewModel), Permissions.LicensePlateView);
        Add("Ausgemusterte Fahrzeuge", "", typeof(RetiredVehiclesViewModel), Permissions.VehicleView);
        Add("Dokumente", "", typeof(DocumentsViewModel), Permissions.DocumentView);
        Add("Berichte & Formulare", "", typeof(ReportsViewModel), Permissions.ReportsPrint);

        var administrationItems = new List<NavigationItem>();

        void AddAdministration(string title, string icon, Type viewModelType, string permission)
        {
            if (_currentUser.HasPermission(permission))
            {
                administrationItems.Add(new NavigationItem(title, icon, viewModelType) { IsEnabled = true });
            }
        }

        AddAdministration("Benutzer", "", typeof(UsersViewModel), Permissions.UsersManage);
        AddAdministration("Rollen & Rechte", "", typeof(RolesViewModel), Permissions.RolesManage);
        AddAdministration("Audit-Log", "", typeof(AuditViewModel), Permissions.AuditView);
        AddAdministration("Backups", "", typeof(BackupViewModel), Permissions.BackupManage);
        AddAdministration("Updates", "", typeof(UpdatesViewModel), Permissions.UpdatesManage);
        AddAdministration("Einstellungen", "", typeof(SettingsViewModel), Permissions.SettingsManage);

        if (administrationItems.Count > 0)
        {
            AddSection("ADMINISTRATION");

            foreach (var item in administrationItems)
            {
                NavigationItems.Add(item);
            }
        }

        AddSection(string.Empty);
        NavigationItems.Add(new NavigationItem("Hilfe & Support", "", typeof(SupportViewModel))
        {
            IsEnabled = true
        });
    }

    private static string EnumDisplay(string value) =>
        value.Length <= 1 ? value : char.ToUpperInvariant(value[0]) + value[1..].ToLowerInvariant();
}

/// <summary>Eintrag der Seitenleiste.</summary>
public sealed partial class NavigationItem : ObservableObject
{
    public NavigationItem(string title, string icon, Type? viewModelType)
    {
        Title = title;
        Icon = icon;
        ViewModelType = viewModelType;
    }

    public string Title { get; }

    public string Icon { get; }

    public Type? ViewModelType { get; }

    public bool IsSection { get; private init; }

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isEnabled = true;

    public static NavigationItem Section(string title) =>
        new(title, string.Empty, null) { IsSection = true, IsEnabled = false };
}
