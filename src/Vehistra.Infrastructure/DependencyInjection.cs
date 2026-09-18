using Vehistra.Application.Abstractions;
using Vehistra.Infrastructure.Diagnostics;
using Vehistra.Infrastructure.ImportExport;
using Vehistra.Infrastructure.Persistence;
using Vehistra.Infrastructure.Persistence.Interceptors;
using Vehistra.Infrastructure.Persistence.Seeding;
using Vehistra.Infrastructure.Security;
using Vehistra.Infrastructure.Services;
using Vehistra.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Vehistra.Infrastructure;

/// <summary>Registriert Datenzugriff und technische Dienste im DI-Container.</summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registriert die Infrastruktur fuer den Betrieb gegen Microsoft SQL Server.
    /// </summary>
    public static IServiceCollection AddVehistraInfrastructure(
        this IServiceCollection services,
        Func<IServiceProvider, string> connectionStringFactory,
        string? applicationVersion = null,
        int commandTimeoutSeconds = 60)
    {
        services.AddSingleton(new ApplicationVersionProvider(applicationVersion));
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IConnectionSettingsStore, ConnectionSettingsStore>();
        services.AddSingleton<FileSystemDocumentStorage>();
        services.AddSingleton<IDocumentStorage>(sp => sp.GetRequiredService<FileSystemDocumentStorage>());

        services.AddScoped<AuditSaveChangesInterceptor>(sp => new AuditSaveChangesInterceptor(
            sp.GetRequiredService<ICurrentUserService>(),
            sp.GetRequiredService<IClock>(),
            sp.GetRequiredService<ApplicationVersionProvider>().Version));

        services.AddScoped<ConcurrencyTokenInterceptor>();

        services.AddDbContext<VehistraDbContext>((sp, options) =>
        {
            options.UseSqlServer(connectionStringFactory(sp), sql =>
            {
                sql.CommandTimeout(commandTimeoutSeconds);
                sql.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorNumbersToAdd: null);
                sql.MigrationsHistoryTable("__EFMigrationsHistory");
            });

            options.AddInterceptors(
                sp.GetRequiredService<AuditSaveChangesInterceptor>(),
                sp.GetRequiredService<ConcurrencyTokenInterceptor>());
        });

        services.AddScoped<IVehistraDbContext>(sp => sp.GetRequiredService<VehistraDbContext>());

        return services.AddVehistraInfrastructureServices();
    }

    /// <summary>
    /// Registriert die technischen Dienste ohne Datenbankanbieter.
    /// Wird von den Integrationstests genutzt, die einen eigenen Anbieter konfigurieren.
    /// </summary>
    public static IServiceCollection AddVehistraInfrastructureServices(this IServiceCollection services)
    {
        services.AddScoped<IAuditWriter, AuditWriter>();
        services.AddScoped<MigrationLockManager>();
        services.AddScoped<IBackupService, BackupService>();
        services.AddScoped<IDatabaseAdministrationService, DatabaseAdministrationService>();
        services.AddScoped<IUpdateService, UpdateService>();
        services.AddScoped<IDiagnosticsService, DiagnosticsService>();
        services.AddScoped<IImportService, ImportService>();
        services.AddScoped<IExportService, ExportService>();
        services.AddScoped<DatabaseSeeder>();
        services.AddScoped<DevelopmentDataSeeder>();

        return services;
    }
}
