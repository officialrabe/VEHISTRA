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
    /// Migrationen sind anbieterspezifisch und liegen deshalb in eigenen
    /// Projekten. Die Namen werden bewusst als Zeichenkette verwendet, damit
    /// die Infrastruktur nicht von ihnen abhaengt.
    /// </summary>
    public const string SqlServerMigrationsAssembly = "Vehistra.Migrations.SqlServer";

    public const string SqliteMigrationsAssembly = "Vehistra.Migrations.Sqlite";

    /// <summary>
    /// Registriert die Infrastruktur. Welche Datenbank verwendet wird, entscheiden
    /// die gespeicherten Verbindungseinstellungen: Microsoft SQL Server im
    /// Mehrplatzbetrieb, SQLite als Datei beim Solo-Platz.
    /// </summary>
    public static IServiceCollection AddVehistraInfrastructure(
        this IServiceCollection services,
        Func<IServiceProvider, ServerConnectionSettings> settingsFactory,
        string? applicationVersion = null,
        int commandTimeoutSeconds = 60)
    {
        services.AddSingleton(new ApplicationVersionProvider(applicationVersion));
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IConnectionSettingsStore, ConnectionSettingsStore>();
        services.AddSingleton<FileSystemDocumentStorage>();
        services.AddSingleton<IDocumentStorage>(sp => sp.GetRequiredService<FileSystemDocumentStorage>());

        services.AddDbContext<VehistraDbContext>((sp, options) =>
        {
            var settings = settingsFactory(sp);
            var connectionString = sp.GetRequiredService<IConnectionSettingsStore>()
                .BuildConnectionString(settings);

            if (settings.Provider == DatabaseProvider.Sqlite)
            {
                options.UseSqlite(connectionString, sqlite =>
                {
                    sqlite.CommandTimeout(commandTimeoutSeconds);
                    sqlite.MigrationsAssembly(SqliteMigrationsAssembly);
                    sqlite.MigrationsHistoryTable("__EFMigrationsHistory");
                });
            }
            else
            {
                options.UseSqlServer(connectionString, sql =>
                {
                    sql.CommandTimeout(commandTimeoutSeconds);
                    sql.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(5),
                        errorNumbersToAdd: null);
                    sql.MigrationsAssembly(SqlServerMigrationsAssembly);
                    sql.MigrationsHistoryTable("__EFMigrationsHistory");
                });
            }

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
        services.AddScoped<AuditSaveChangesInterceptor>(sp => new AuditSaveChangesInterceptor(
            sp.GetRequiredService<ICurrentUserService>(),
            sp.GetRequiredService<IClock>(),
            sp.GetRequiredService<ApplicationVersionProvider>().Version));

        services.AddScoped<ConcurrencyTokenInterceptor>();

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
