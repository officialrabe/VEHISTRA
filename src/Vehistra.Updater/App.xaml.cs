using System.IO;
using System.Windows;
using Vehistra.Application;
using Vehistra.Application.Abstractions;
using Vehistra.Infrastructure;
using Vehistra.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

namespace Vehistra.Updater;

/// <summary>
/// Vehistra.Updater.exe - fuehrt Programm- und Datenbankupdates aus,
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

        builder.Services.AddVehistraApplication();
        builder.Services.AddVehistraInfrastructure(provider =>
        {
            var store = provider.GetRequiredService<IConnectionSettingsStore>();

            // Ohne gespeicherte Verbindung wird der Solo-Platz angenommen: eine
            // Datenbankdatei unter ProgramData, die keine Installation braucht.
            return store.Load() ?? new ServerConnectionSettings
            {
                Provider = DatabaseProvider.Sqlite,
                DatabaseFile = ConnectionSettingsStore.DefaultDatabaseFile
            };
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
