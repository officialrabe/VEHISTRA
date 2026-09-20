using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Vehistra.Application.Abstractions;
using Vehistra.Application.Dtos;
using Vehistra.Application.Services;
using Vehistra.Client.Services;
using Vehistra.Client.ViewModels;
using Vehistra.Client.Views;
using Vehistra.Domain.Enums;
using Vehistra.Domain.Security;
using Vehistra.Infrastructure;
using Vehistra.Infrastructure.Persistence;
using Vehistra.Infrastructure.Persistence.Seeding;
using Vehistra.Reporting;

namespace Vehistra.Screenshots;

/// <summary>
/// Erzeugt Bildschirmfotos der echten Ansichten - dieselben Ansichten, dieselben
/// Vorlagen, dieselben Farben wie im Programm, nur mit Beispieldaten und ohne
/// dass jemand klicken muss.
///
/// Gerendert wird mit RenderTargetBitmap. Das braucht keinen Bildschirm und
/// kein geoeffnetes Fenster: der Inhalt des Fensters wird gemessen, angeordnet
/// und als Bild abgelegt. Deshalb laeuft das auch auf einem Bauserver.
/// </summary>
public static class Program
{
    /// <summary>Breite und Hoehe in geraeteunabhaengigen Pixeln.</summary>
    private const int Breite = 1920;

    private const int Hoehe = 1200;

    /// <summary>Zweifache Aufloesung - fuer Bildschirme mit hoher Punktdichte.</summary>
    private const double Skalierung = 2.0;

    [STAThread]
    public static int Main(string[] args)
    {
        var ziel = args.Length > 0 ? args[0] : Path.Combine(Environment.CurrentDirectory, "screenshots");
        Directory.CreateDirectory(ziel);

        var anwendung = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };

        // Dieselben Ressourcen wie App.xaml - sonst fehlen Farben und Vorlagen.
        foreach (var quelle in new[] { "Colors", "Controls", "DataTemplates" })
        {
            anwendung.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri($"pack://application:,,,/Vehistra;component/Resources/{quelle}.xaml")
            });
        }

        var ergebnis = 0;

        anwendung.Dispatcher.InvokeAsync(async () =>
        {
            try
            {
                await ErzeugeAsync(ziel).ConfigureAwait(true);
                Console.WriteLine($"Fertig. Die Bilder liegen in {ziel}.");
            }
            catch (Exception fehler)
            {
                Console.Error.WriteLine(fehler);
                ergebnis = 1;
            }
            finally
            {
                anwendung.Shutdown();
            }
        });

        anwendung.Run();
        return ergebnis;
    }

    private static async Task ErzeugeAsync(string ziel)
    {
        var arbeitsordner = Path.Combine(Path.GetTempPath(), "vehistra-screenshots-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(arbeitsordner);

        var verbindung = new ServerConnectionSettings
        {
            Provider = DatabaseProvider.Sqlite,
            DatabaseFile = Path.Combine(arbeitsordner, "Vehistra.db"),
            DocumentsPath = Path.Combine(arbeitsordner, "Dokumente"),
            BackupPath = Path.Combine(arbeitsordner, "Sicherungen"),
            UpdatePath = Path.Combine(arbeitsordner, "Updates")
        };

        Directory.CreateDirectory(verbindung.DocumentsPath!);
        Directory.CreateDirectory(verbindung.BackupPath!);

        var dienste = new ServiceCollection();

        dienste.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        Vehistra.Application.DependencyInjection.AddVehistraApplication(dienste);
        dienste.AddSingleton<IReportService, QuestPdfReportService>();
        dienste.AddVehistraInfrastructure(_ => verbindung, "1.4.0");
        dienste.AddSingleton<IDialogService, StummerDialogdienst>();
        dienste.AddSingleton<INavigationService, NavigationService>();
        dienste.AddSingleton<IReportLauncher, ReportLauncher>();
        dienste.AddScoped<IReportGenerator, ReportGenerator>();
        dienste.AddSingleton<AppShellState>();
        dienste.AddViewModels();
        dienste.AddWindows();

        var anbieter = dienste.BuildServiceProvider();

        // Datenbank aufbauen und mit Beispieldaten fuellen.
        using (var bereich = anbieter.CreateScope())
        {
            var db = bereich.ServiceProvider.GetRequiredService<VehistraDbContext>();
            await db.Database.MigrateAsync().ConfigureAwait(true);

            await bereich.ServiceProvider.GetRequiredService<DatabaseSeeder>()
                .SeedSystemDataAsync().ConfigureAwait(true);
            await bereich.ServiceProvider.GetRequiredService<DevelopmentDataSeeder>()
                .SeedAsync().ConfigureAwait(true);
        }

        // Anmelden wie ein Verwalter - sonst blendet die Oberflaeche zu Recht
        // die Haelfte aus.
        anbieter.GetRequiredService<ICurrentUserService>().SetUser(new CurrentUser(
            1, "verwaltung", "Sabine", "Verwaltung",
            [RoleNames.Administrator],
            [.. Permissions.All.Select(p => p.Name)],
            false));

        using (var bereich = anbieter.CreateScope())
        {
            var einstellungen = bereich.ServiceProvider.GetRequiredService<ISettingsService>();

            await einstellungen.SetAsync(SettingsKeys.CompanyName, "Musterbetrieb GmbH").ConfigureAwait(true);
            await einstellungen.SetAsync(SettingsKeys.CompanyCity, "Musterstadt").ConfigureAwait(true);
            einstellungen.InvalidateCache();
        }

        var shell = anbieter.GetRequiredService<ShellViewModel>();
        var fenster = new MainWindow(shell) { Width = Breite, Height = Hoehe };

        await shell.LoadAsync().ConfigureAwait(true);
        await Warte().ConfigureAwait(true);

        var navigation = anbieter.GetRequiredService<INavigationService>();

        var bilder = new (string Datei, Func<Task> Oeffnen)[]
        {
            ("01-ueberblick", () => navigation.NavigateToAsync<DashboardViewModel>()),
            ("02-fahrzeuge", () => navigation.NavigateToAsync<VehicleListViewModel>()),
            ("03-fahrzeugakte", () => navigation.OpenVehicleAsync(1)),
            ("04-tuev-fristen", () => navigation.NavigateToAsync<InspectionViewModel>()),
            ("05-werkstatt", () => navigation.NavigateToAsync<WorkshopViewModel>()),
            ("06-schaeden", () => navigation.NavigateToAsync<DamageViewModel>())
        };

        foreach (var (datei, oeffnen) in bilder)
        {
            await oeffnen().ConfigureAwait(true);
            await Warte().ConfigureAwait(true);

            WaehleErsteZeile(navigation.Current);
            await Warte().ConfigureAwait(true);

            Speichere(fenster, Path.Combine(ziel, datei + ".png"));
            Console.WriteLine($"  {datei}.png");
        }
    }

    /// <summary>
    /// Waehlt in einer Liste die erste Zeile aus. Ohne Auswahl sind die
    /// Schaltflaechen der Werkzeugleiste abgeblendet - auf einem Bild sieht
    /// das aus, als koenne das Programm nichts.
    /// </summary>
    private static void WaehleErsteZeile(object? ansichtsmodell)
    {
        if (ansichtsmodell is null)
        {
            return;
        }

        var typ = ansichtsmodell.GetType();

        foreach (var name in new[]
                 {
                     "SelectedVehicle", "SelectedOrder", "SelectedItem", "SelectedDamage",
                     "SelectedAccident", "SelectedDriver", "SelectedPlate"
                 })
        {
            var auswahl = typ.GetProperty(name);

            if (auswahl is null || auswahl.GetValue(ansichtsmodell) is not null)
            {
                continue;
            }

            // Die passende Liste heisst wie die Auswahl, nur im Plural -
            // "SelectedVehicle" zu "Vehicles". Zur Not wird gesucht.
            var liste = typ.GetProperties()
                .Where(e => typeof(System.Collections.IEnumerable).IsAssignableFrom(e.PropertyType))
                .Select(e => e.GetValue(ansichtsmodell) as System.Collections.IEnumerable)
                .OfType<System.Collections.IEnumerable>()
                .SelectMany(e => e.Cast<object>())
                .FirstOrDefault(eintrag => auswahl.PropertyType.IsInstanceOfType(eintrag));

            if (liste is not null)
            {
                auswahl.SetValue(ansichtsmodell, liste);
                return;
            }
        }
    }

    /// <summary>Misst, ordnet an und legt den Fensterinhalt als PNG ab.</summary>
    private static void Speichere(Window fenster, string pfad)
    {
        var inhalt = (FrameworkElement)fenster.Content;

        inhalt.Measure(new Size(Breite, Hoehe));
        inhalt.Arrange(new Rect(0, 0, Breite, Hoehe));
        inhalt.UpdateLayout();

        var bild = new RenderTargetBitmap(
            (int)(Breite * Skalierung), (int)(Hoehe * Skalierung),
            96 * Skalierung, 96 * Skalierung, PixelFormats.Pbgra32);

        bild.Render(inhalt);

        var geber = new PngBitmapEncoder();
        geber.Frames.Add(BitmapFrame.Create(bild));

        using var datei = File.Create(pfad);
        geber.Save(datei);
    }

    /// <summary>
    /// Laesst die Oberflaeche zu Ende arbeiten: Bindungen, Vorlagen und die
    /// Ladevorgaenge der Ansicht laufen ueber den Dispatcher.
    /// </summary>
    private static async Task Warte()
    {
        for (var durchgang = 0; durchgang < 3; durchgang++)
        {
            await System.Windows.Application.Current.Dispatcher
                .InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
            await Task.Delay(250).ConfigureAwait(true);
        }
    }

    /// <summary>
    /// Beantwortet Rueckfragen von selbst: hier klickt niemand, und ein
    /// wartender Dialog wuerde den Lauf haengen lassen.
    /// </summary>
    private sealed class StummerDialogdienst : IDialogService
    {
        public void ShowInformation(string message, string title = "Hinweis") { }

        public void ShowWarning(string message, string title = "Warnung") { }

        public void ShowError(string message, Exception? exception = null, string title = "Fehler") =>
            Console.Error.WriteLine($"{title}: {message}");

        public bool Confirm(string message, string title = "Bestätigen") => false;

        public string? Prompt(string message, string title = "Eingabe", string? initialValue = null) => null;

        public string? OpenFile(string filter, string title = "Datei auswählen") => null;

        public IReadOnlyList<string> OpenFiles(string filter, string title = "Dateien auswählen") => [];

        public string? SaveFile(string filter, string defaultFileName, string title = "Speichern unter") => null;

        public string? SelectFolder(string description) => null;

        public ConcurrencyResolution ResolveConcurrencyConflict(string entityDescription, string? differences) =>
            ConcurrencyResolution.Cancel;

        public bool? ShowDialog(object viewModel) => false;

        public void OpenInShell(string path) { }
    }
}
