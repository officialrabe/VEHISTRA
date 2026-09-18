using System.IO;
using System.Windows;
using Fuhrpark.Application;
using Fuhrpark.Application.Abstractions;
using Fuhrpark.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

namespace Fuhrpark.Updater;

/// <summary>
/// FuhrparkManager.Updater.exe - fuehrt Programm- und Datenbankupdates aus,
/// waehrend die Hauptanwendung geschlossen ist.
/// </summary>
public partial class App : System.Windows.Application
{
    private IHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ApplicationPaths.EnsureCreated();

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                Path.Combine(ApplicationPaths.Logs, "updater-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        var options = UpdateOptions.Parse(e.Args);
        Log.Information("Updater gestartet: Zielversion {Version}", options.TargetVersion);

        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Logging.AddSerilog(Log.Logger);

        builder.Services.AddFuhrparkApplication();
        builder.Services.AddFuhrparkInfrastructure(provider =>
        {
            var store = provider.GetRequiredService<IConnectionSettingsStore>();
            var settings = store.Load();

            return settings is null
                ? "Server=.;Database=FuhrparkDB;Trusted_Connection=True;TrustServerCertificate=True"
                : store.BuildConnectionString(settings);
        }, options.TargetVersion);

        builder.Services.AddSingleton<UpdateRunner>();

        _host = builder.Build();

        var window = new MainWindow(
            _host.Services.GetRequiredService<UpdateRunner>(),
            options);

        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();
        Log.CloseAndFlush();

        base.OnExit(e);
    }
}
