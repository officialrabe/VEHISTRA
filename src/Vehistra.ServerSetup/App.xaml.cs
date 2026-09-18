using System.Runtime.Versioning;
using System.Windows;
using Vehistra.Application;
using Vehistra.Application.Abstractions;
using Vehistra.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

namespace Vehistra.ServerSetup;

/// <summary>
/// VehistraServerSetup.exe - richtet den Fuhrparkserver in zwoelf verstaendlichen Schritten ein.
/// Entwickelt von LSP Virtual Services.
/// </summary>
[SupportedOSPlatform("windows")]
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
                Path.Combine(ApplicationPaths.Logs, "serversetup-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        Log.Information("Server-Setup gestartet auf {Machine} durch {User}",
            Environment.MachineName, Environment.UserName);

        DispatcherUnhandledException += OnDispatcherUnhandledException;

        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Logging.AddSerilog(Log.Logger);

        builder.Services.AddVehistraApplication();

        // Die Verbindung wird waehrend des Assistenten festgelegt. Der Verbindungsstring
        // wird deshalb bei jeder Kontexterzeugung neu aus der gespeicherten Konfiguration gelesen.
        builder.Services.AddVehistraInfrastructure(provider =>
        {
            var store = provider.GetRequiredService<IConnectionSettingsStore>();
            var settings = store.Load();

            return settings is null
                ? $"Server={Environment.MachineName}\\SQLEXPRESS;Database=VehistraDB;" +
                  "Trusted_Connection=True;TrustServerCertificate=True"
                : store.BuildConnectionString(settings);
        }, ThisAssembly.Version);

        builder.Services.AddSingleton<SetupViewModel>();

        _host = builder.Build();

        var window = new MainWindow(_host.Services.GetRequiredService<SetupViewModel>());
        MainWindow = window;
        window.Show();
    }

    private void OnDispatcherUnhandledException(
        object sender,
        System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unbehandelter Fehler im Server-Setup.");

        MessageBox.Show(
            "Bei der Einrichtung ist ein unerwarteter Fehler aufgetreten:" +
            Environment.NewLine + Environment.NewLine + e.Exception.Message +
            Environment.NewLine + Environment.NewLine +
            "Eine ausführliche Beschreibung steht im Protokoll unter" + Environment.NewLine +
            ApplicationPaths.Logs,
            "Vehistra – Server-Einrichtung",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);

        e.Handled = true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();
        Log.CloseAndFlush();

        base.OnExit(e);
    }
}

/// <summary>Versionsangabe des Setupprogramms.</summary>
internal static class ThisAssembly
{
    public static string Version =>
        typeof(App).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";
}
