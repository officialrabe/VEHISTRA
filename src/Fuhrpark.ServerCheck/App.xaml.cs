using System.Runtime.Versioning;
using System.Windows;
using Fuhrpark.Application;
using Fuhrpark.Application.Abstractions;
using Fuhrpark.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

namespace Fuhrpark.ServerCheck;

/// <summary>
/// FuhrparkServerCheck.exe - prueft die Serverinstallation und erklaert jedes Problem
/// in einfacher Sprache. Entwickelt von LSP Virtual Services.
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
                Path.Combine(ApplicationPaths.Logs, "servercheck-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        DispatcherUnhandledException += OnDispatcherUnhandledException;

        var version = typeof(App).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";

        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Logging.AddSerilog(Log.Logger);

        builder.Services.AddFuhrparkApplication();
        builder.Services.AddFuhrparkInfrastructure(provider =>
        {
            var store = provider.GetRequiredService<IConnectionSettingsStore>();
            var settings = store.Load();

            // Ohne Konfiguration wird ein Platzhalter verwendet; die Pruefung meldet das verstaendlich.
            return settings is null
                ? $"Server={Environment.MachineName}\\SQLEXPRESS;Database=FuhrparkDB;" +
                  "Trusted_Connection=True;TrustServerCertificate=True"
                : store.BuildConnectionString(settings);
        }, version);

        builder.Services.AddSingleton<ServerCheckRunner>();
        builder.Services.AddSingleton<ServerCheckViewModel>();

        _host = builder.Build();

        var window = new MainWindow(_host.Services.GetRequiredService<ServerCheckViewModel>());
        MainWindow = window;
        window.Show();
    }

    private void OnDispatcherUnhandledException(
        object sender,
        System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unbehandelter Fehler in der Serverprüfung.");

        MessageBox.Show(
            "Bei der Prüfung ist ein unerwarteter Fehler aufgetreten:" +
            Environment.NewLine + Environment.NewLine + e.Exception.Message +
            Environment.NewLine + Environment.NewLine +
            "Weitere Angaben stehen im Protokoll unter" + Environment.NewLine + ApplicationPaths.Logs,
            "Fuhrparkmanagement – Serverprüfung",
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
