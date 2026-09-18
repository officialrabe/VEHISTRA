using CommunityToolkit.Mvvm.ComponentModel;
using Vehistra.Client.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Vehistra.Client.Services;

/// <summary>Wechselt zwischen den Arbeitsbereichen des Hauptfensters.</summary>
public interface INavigationService
{
    ViewModelBase? Current { get; }

    event EventHandler<ViewModelBase?>? CurrentChanged;

    Task NavigateToAsync<TViewModel>() where TViewModel : ViewModelBase;

    Task NavigateToAsync(Type viewModelType);

    /// <summary>Oeffnet die Fahrzeugakte eines bestimmten Fahrzeugs.</summary>
    Task OpenVehicleAsync(int vehicleId, string? tabKey = null);

    /// <summary>Springt zu einem Suchergebnis der globalen Suche.</summary>
    Task OpenSearchResultAsync(string entityType, int entityId, int? vehicleId);
}

/// <summary>
/// Jede Ansicht erhaelt einen eigenen DI-Bereich und damit einen frischen Datenbankkontext.
/// Der Bereich der vorherigen Ansicht wird freigegeben, sodass keine veralteten Daten
/// im Speicher verbleiben.
/// </summary>
public sealed class NavigationService : ObservableObject, INavigationService, IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NavigationService> _logger;

    private IServiceScope? _currentScope;
    private ViewModelBase? _current;

    public NavigationService(IServiceScopeFactory scopeFactory, ILogger<NavigationService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public ViewModelBase? Current
    {
        get => _current;
        private set
        {
            if (SetProperty(ref _current, value))
            {
                CurrentChanged?.Invoke(this, value);
            }
        }
    }

    public event EventHandler<ViewModelBase?>? CurrentChanged;

    public Task NavigateToAsync<TViewModel>() where TViewModel : ViewModelBase =>
        NavigateToAsync(typeof(TViewModel));

    public async Task NavigateToAsync(Type viewModelType)
    {
        var viewModel = CreateViewModel(viewModelType);

        if (viewModel is null)
        {
            return;
        }

        Current = viewModel;
        await viewModel.LoadAsync().ConfigureAwait(true);
    }

    public async Task OpenVehicleAsync(int vehicleId, string? tabKey = null)
    {
        if (CreateViewModel(typeof(VehicleDetailViewModel)) is not VehicleDetailViewModel viewModel)
        {
            return;
        }

        Current = viewModel;
        await viewModel.LoadVehicleAsync(vehicleId, tabKey).ConfigureAwait(true);
    }

    public async Task OpenSearchResultAsync(string entityType, int entityId, int? vehicleId)
    {
        switch (entityType)
        {
            case "Fahrzeug":
                await OpenVehicleAsync(entityId).ConfigureAwait(true);
                break;

            case "Fahrer":
                await NavigateToAsync<DriverViewModel>().ConfigureAwait(true);
                break;

            case "Schaden":
                await NavigateToAsync<DamageViewModel>().ConfigureAwait(true);
                break;

            case "Unfall":
                await NavigateToAsync<AccidentViewModel>().ConfigureAwait(true);
                break;

            case "Werkstattvorgang":
                await NavigateToAsync<WorkshopViewModel>().ConfigureAwait(true);
                break;

            case "Kennzeichen":
                await NavigateToAsync<LicensePlateViewModel>().ConfigureAwait(true);
                break;

            default:
                if (vehicleId is { } id)
                {
                    await OpenVehicleAsync(id).ConfigureAwait(true);
                }

                break;
        }
    }

    public void Dispose()
    {
        _currentScope?.Dispose();
        _currentScope = null;
    }

    private ViewModelBase? CreateViewModel(Type viewModelType)
    {
        var scope = _scopeFactory.CreateScope();

        if (scope.ServiceProvider.GetService(viewModelType) is not ViewModelBase viewModel)
        {
            _logger.LogWarning("Für {ViewModel} ist keine Ansicht registriert.", viewModelType.Name);
            scope.Dispose();
            return null;
        }

        // Der bisherige Bereich wird erst freigegeben, wenn die neue Ansicht steht.
        var previous = _currentScope;
        _currentScope = scope;
        previous?.Dispose();

        return viewModel;
    }
}
