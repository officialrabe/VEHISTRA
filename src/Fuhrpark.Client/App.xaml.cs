using System.IO;
using System.Globalization;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Threading;
using Fuhrpark.Application;
using Fuhrpark.Application.Abstractions;
using Fuhrpark.Client.Services;
using Fuhrpark.Client.ViewModels;
using Fuhrpark.Client.Views;
using Fuhrpark.Infrastructure;
using Fuhrpark.Infrastructure.Persistence;
using Fuhrpark.Infrastructure.Storage;
using Fuhrpark.Reporting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace Fuhrpark.Client;

/// <summary>
/// Einstiegspunkt der Fuhrparksoftware. Baut den DI-Container auf, prueft die
/// Serververbindung und steuert Anmeldung und Hauptfenster.
/// </summary>
public partial class App : System.Windows.Application
{
    private IHost? _host;
    private IServiceScope? _sessionScope;

    /// <summary>
    /// Dienste der laufenden Sitzung. Fenster und Ansichtsmodelle werden aus einem
    /// eigenen DI-Bereich aufgeloest, damit der Datenbankkontext sauber freigegeben wird.
    /// </summary>
    public static IServiceProvider Services =>
        ((App)Current)._sessionScope?.ServiceProvider
        ?? ((App)Current)._host?.Services
        ?? throw new InvalidOperationException("Die Anwendung wurde noch nicht initialisiert.");

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Deutsche Formatierung in der gesamten Oberflaeche.
        var culture = CultureInfo.GetCultureInfo("de-DE");
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;

        FrameworkElement.LanguageProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(culture.IetfLanguageTag)));

        ApplicationPaths.EnsureCreated();
        ConfigureLogging();

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        SplashWindow? splash = null;

        try
        {
            splash = new SplashWindow();
            splash.Show();
            splash.SetStatus("Anwendung wird vorbereitet ...");

            _host = BuildHost();
            await _host.StartAsync().ConfigureAwait(true);
            _sessionScope = _host.Services.CreateScope();

            splash.SetStatus("Verbindung zum Server wird hergestellt ...");

            if (!await EnsureServerConnectionAsync().ConfigureAwait(true))
            {
                splash.Close();
                Shutdown(1);
                return;
            }

            splash.SetStatus("Anmeldung wird vorbereitet ...");
            splash.Close();
            splash = null;

            if (!ShowLogin())
            {
                Shutdown(0);
                return;
            }

            var mainWindow = Services.GetRequiredService<MainWindow>();
            MainWindow = mainWindow;
            mainWindow.Show();
        }
        catch (Exception exception)
        {
            splash?.Close();
            Log.Fatal(exception, "Die Anwendung konnte nicht gestartet werden.");

            MessageBox.Show(
                "Die Anwendung konnte nicht gestartet werden." + Environment.NewLine + Environment.NewLine +
                exception.Message + Environment.NewLine + Environment.NewLine +
                $"Weitere Angaben finden Sie im Logverzeichnis:{Environment.NewLine}{ApplicationPaths.Logs}",
                "Fuhrparkmanagement", MessageBoxButton.OK, MessageBoxImage.Error);

            Shutdown(1);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        _sessionScope?.Dispose();

        if (_host is not null)
        {
            await _host.StopAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            _host.Dispose();
        }

        await Log.CloseAndFlushAsync().ConfigureAwait(false);
        base.OnExit(e);
    }

    private static void ConfigureLogging()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
            .Enrich.WithProperty("Computer", Environment.MachineName)
            .WriteTo.File(
                Path.Combine(ApplicationPaths.Logs, "fuhrpark-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                fileSizeLimitBytes: 20 * 1024 * 1024,
                rollOnFileSizeLimit: true,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        Log.Information("Fuhrparkmanagement wird gestartet ({Version}).",
            typeof(App).Assembly.GetName().Version?.ToString(3) ?? "unbekannt");
    }

    private static IHost BuildHost()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.Logging.ClearProviders();
        builder.Logging.AddSerilog(Log.Logger);

        var version = typeof(App).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";

        builder.Services.AddFuhrparkApplication();
        builder.Services.AddSingleton<IReportService, QuestPdfReportService>();

        builder.Services.AddFuhrparkInfrastructure(provider =>
        {
            var store = provider.GetRequiredService<IConnectionSettingsStore>();
            var settings = store.Load();

            return settings is null
                ? "Server=.;Database=FuhrparkDB;Trusted_Connection=True;TrustServerCertificate=True"
                : store.BuildConnectionString(settings);
        }, version);

        builder.Services.AddSingleton<IDialogService, DialogService>();
        builder.Services.AddSingleton<INavigationService, NavigationService>();
        builder.Services.AddSingleton<IReportLauncher, ReportLauncher>();
        builder.Services.AddScoped<IReportGenerator, ReportGenerator>();
        builder.Services.AddSingleton<AppShellState>();

        builder.Services.AddViewModels();
        builder.Services.AddWindows();

        return builder.Build();
    }

    /// <summary>
    /// Stellt sicher, dass eine gueltige Serververbindung besteht. Bei Problemen wird ein
    /// verstaendlicher Dialog angezeigt - die Anwendung stuerzt niemals einfach ab.
    /// </summary>
    private async Task<bool> EnsureServerConnectionAsync()
    {
        var store = Services.GetRequiredService<IConnectionSettingsStore>();

        while (true)
        {
            var settings = store.Load();

            if (settings is null || string.IsNullOrWhiteSpace(settings.Server))
            {
                var setupWindow = Services.GetRequiredService<ServerSettingsWindow>();
                if (setupWindow.ShowDialog() != true)
                {
                    return false;
                }

                continue;
            }

            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<FuhrparkDbContext>();

            try
            {
                if (await db.Database.CanConnectAsync().ConfigureAwait(true))
                {
                    ConfigureDocumentStorage(settings);

                    var pending = await db.Database.GetPendingMigrationsAsync().ConfigureAwait(true);
                    if (pending.Any())
                    {
                        Log.Warning("Das Datenbankschema ist nicht aktuell: {Count} offene Migration(en).",
                            pending.Count());

                        MessageBox.Show(
                            "Das Datenbankschema der Fuhrparkdatenbank ist älter als diese Programmversion." +
                            Environment.NewLine + Environment.NewLine +
                            "Bitte lassen Sie das Datenbankupdate durch einen Administrator über " +
                            "'Administration → Updates' ausführen. Vorher wird automatisch eine Sicherung erstellt.",
                            "Datenbankupdate erforderlich", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }

                    return true;
                }
            }
            catch (Exception exception)
            {
                Log.Error(exception, "Die Verbindung zur Fuhrparkdatenbank ist fehlgeschlagen.");
            }

            var unavailable = Services.GetRequiredService<ServerUnavailableWindow>();
            unavailable.Initialize(settings);

            if (unavailable.ShowDialog() != true)
            {
                return false;
            }
        }
    }

    /// <summary>Uebergibt den konfigurierten Dokumentenpfad an die Ablage.</summary>
    private static void ConfigureDocumentStorage(ServerConnectionSettings settings)
    {
        var storage = Services.GetRequiredService<FileSystemDocumentStorage>();
        storage.Configure(settings.DocumentsPath);
    }

    private bool ShowLogin()
    {
        var login = Services.GetRequiredService<LoginWindow>();
        return login.ShowDialog() == true;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unbehandelte Ausnahme in der Oberflaeche.");
        ShowUnhandledError(e.Exception);
        e.Handled = true;
    }

    private static void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            Log.Fatal(exception, "Unbehandelte Ausnahme in der Anwendung.");
        }
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unbeobachtete Ausnahme in einer Hintergrundaufgabe.");
        e.SetObserved();
    }

    private static void ShowUnhandledError(Exception exception)
    {
        try
        {
            var dialog = Services.GetService<IDialogService>();

            if (dialog is not null)
            {
                dialog.ShowError(ViewModelBase.Describe(exception), exception);
                return;
            }
        }
        catch (Exception)
        {
            // Faellt auf die einfache Meldung zurueck.
        }

        MessageBox.Show(
            ViewModelBase.Describe(exception),
            "Fuhrparkmanagement", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
