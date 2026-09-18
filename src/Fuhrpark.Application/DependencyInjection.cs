using Fuhrpark.Application.Abstractions;
using Fuhrpark.Application.Security;
using Fuhrpark.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Fuhrpark.Application;

/// <summary>Registriert alle Anwendungsdienste im DI-Container.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddFuhrparkApplication(this IServiceCollection services)
    {
        services.AddSingleton<ICurrentUserService, CurrentUserService>();
        services.AddSingleton<IClock, SystemClock>();

        services.AddScoped<ISettingsService, SettingsService>();
        services.AddScoped<NumberGenerator>();

        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<IUserService, UserService>();

        services.AddScoped<IVehicleService, VehicleService>();
        services.AddScoped<IDriverService, DriverService>();
        services.AddScoped<IMileageService, MileageService>();
        services.AddScoped<IInspectionService, InspectionService>();
        services.AddScoped<IMaintenanceService, MaintenanceService>();
        services.AddScoped<IDamageService, DamageService>();
        services.AddScoped<IAccidentService, AccidentService>();
        services.AddScoped<IWorkshopService, WorkshopService>();
        services.AddScoped<ILicensePlateService, LicensePlateService>();
        services.AddScoped<IVehicleLifecycleService, VehicleLifecycleService>();
        services.AddScoped<IInsuranceService, InsuranceService>();
        services.AddScoped<IVehicleKeyService, VehicleKeyService>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<ISearchService, SearchService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IAuditService, AuditService>();

        return services;
    }
}
