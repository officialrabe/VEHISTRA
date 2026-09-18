using Fuhrpark.Client.ViewModels;
using Fuhrpark.Client.ViewModels.Dialogs;
using Fuhrpark.Client.Views;
using Fuhrpark.Client.Views.Dialogs;
using Microsoft.Extensions.DependencyInjection;

namespace Fuhrpark.Client.Services;

/// <summary>Registrierung der Ansichtsmodelle und Fenster.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Ansichtsmodelle werden je DI-Bereich erzeugt. Jede Ansicht arbeitet damit mit einem
    /// eigenen Datenbankkontext.
    /// </summary>
    public static IServiceCollection AddViewModels(this IServiceCollection services)
    {
        services.AddScoped<LoginViewModel>();
        services.AddScoped<ShellViewModel>();
        services.AddScoped<ServerSettingsViewModel>();

        services.AddScoped<DashboardViewModel>();
        services.AddScoped<VehicleListViewModel>();
        services.AddScoped<VehicleDetailViewModel>();
        services.AddScoped<DriverViewModel>();
        services.AddScoped<InspectionViewModel>();
        services.AddScoped<MaintenanceViewModel>();
        services.AddScoped<DamageViewModel>();
        services.AddScoped<AccidentViewModel>();
        services.AddScoped<WorkshopViewModel>();
        services.AddScoped<LicensePlateViewModel>();
        services.AddScoped<RetiredVehiclesViewModel>();
        services.AddScoped<DocumentsViewModel>();
        services.AddScoped<ReportsViewModel>();
        services.AddScoped<UsersViewModel>();
        services.AddScoped<RolesViewModel>();
        services.AddScoped<AuditViewModel>();
        services.AddScoped<BackupViewModel>();
        services.AddScoped<UpdatesViewModel>();
        services.AddScoped<SettingsViewModel>();
        services.AddScoped<SupportViewModel>();

        // Dialoge
        services.AddTransient<VehicleEditViewModel>();
        services.AddTransient<DriverEditViewModel>();
        services.AddTransient<DamageEditViewModel>();
        services.AddTransient<AccidentEditViewModel>();
        services.AddTransient<WorkshopOrderEditViewModel>();
        services.AddTransient<InspectionEditViewModel>();
        services.AddTransient<MaintenanceEntryEditViewModel>();
        services.AddTransient<MaintenanceRuleEditViewModel>();
        services.AddTransient<LicensePlateEditViewModel>();
        services.AddTransient<ReservationEditViewModel>();
        services.AddTransient<RetirementEditViewModel>();
        services.AddTransient<RegistrationEditViewModel>();
        services.AddTransient<MileageEditViewModel>();
        services.AddTransient<DriverAssignmentViewModel>();
        services.AddTransient<StatusChangeViewModel>();
        services.AddTransient<UserEditViewModel>();
        services.AddTransient<RoleEditViewModel>();
        services.AddTransient<ChangePasswordViewModel>();
        services.AddTransient<InsuranceEditViewModel>();
        services.AddTransient<VehicleKeyEditViewModel>();
        services.AddTransient<DocumentUploadViewModel>();
        services.AddTransient<ImportWizardViewModel>();

        return services;
    }

    public static IServiceCollection AddWindows(this IServiceCollection services)
    {
        services.AddTransient<LoginWindow>();
        services.AddTransient<MainWindow>();
        services.AddTransient<ServerSettingsWindow>();
        services.AddTransient<ServerUnavailableWindow>();
        services.AddTransient<DialogHostWindow>();

        return services;
    }
}
