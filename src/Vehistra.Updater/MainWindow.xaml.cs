using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace Vehistra.Updater;

/// <summary>Fenster der Updatekomponente mit Fortschrittsanzeige und Protokoll.</summary>
public partial class MainWindow : Window
{
    private readonly UpdateRunner _runner;
    private readonly UpdateOptions _options;
    private readonly StringBuilder _log = new();

    private bool _completed;

    /// <summary>Zaehlt nach einem erfolgreichen Update bis zum Neustart herunter.</summary>
    private DispatcherTimer? _neustart;

    private int _restsekunden;

    public MainWindow(UpdateRunner runner, UpdateOptions options)
    {
        InitializeComponent();

        _runner = runner;
        _options = options;

        VersionText.Text = string.IsNullOrWhiteSpace(options.TargetVersion)
            ? "Update von Vehistra"
            : $"Update auf Version {options.TargetVersion}";

        Append($"Updatepaket : {options.InstallerPath}");
        Append($"Programmpfad: {options.ApplicationDirectory}");
        Append($"Sicherung vor Datenbankänderung: {(options.CreateBackupBeforeMigration ? "ja" : "nein")}");
        Append(string.Empty);

        if (string.IsNullOrWhiteSpace(options.InstallerPath))
        {
            StatusText.Text = "Es wurde kein Updatepaket übergeben. Bitte starten Sie das Update aus der Anwendung.";
            StartButton.IsEnabled = false;
        }
    }

    private async void OnStart(object sender, RoutedEventArgs e)
    {
        StartButton.IsEnabled = false;
        CloseButton.IsEnabled = false;

        var progress = new Progress<UpdateProgress>(update =>
        {
            Progress.Value = update.Step;
            StatusText.Text = update.Message;
            Append($"[{update.Step}/6] {update.Message}");
        });

        var result = await _runner.RunAsync(_options, progress).ConfigureAwait(true);

        Append(string.Empty);
        Append(result.Message);

        if (!string.IsNullOrWhiteSpace(result.RestoreInstructions))
        {
            Append(string.Empty);
            Append(result.RestoreInstructions);
        }

        StatusText.Text = result.Message;
        StatusText.Foreground = result.IsSuccessful
            ? (System.Windows.Media.Brush)FindResource("OkBrush")
            : (System.Windows.Media.Brush)FindResource("CriticalBrush");

        CloseButton.IsEnabled = true;
        _completed = result.IsSuccessful;

        if (result.IsSuccessful)
        {
            Progress.Value = 6;
            StarteNeustartZaehler();
        }
        else
        {
            CloseButton.Content = "_Schließen";
        }
    }

    /// <summary>
    /// Startet Vehistra nach kurzer Frist von selbst. Ohne das endet das Update
    /// in einem Fenster, das stehen bleibt - und niemand weiss, ob es fertig
    /// ist. Die Frist laesst trotzdem Zeit, das Protokoll zu lesen, und der
    /// Klick auf die Schaltflaeche startet sofort.
    /// </summary>
    private void StarteNeustartZaehler()
    {
        _restsekunden = 5;
        CloseButton.Content = $"_Jetzt starten ({_restsekunden})";

        Append(string.Empty);
        Append($"Vehistra wird in {_restsekunden} Sekunden neu gestartet.");

        _neustart = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _neustart.Tick += (_, _) =>
        {
            _restsekunden--;

            if (_restsekunden <= 0)
            {
                BeendeUndStarte();
                return;
            }

            CloseButton.Content = $"_Jetzt starten ({_restsekunden})";
        };

        _neustart.Start();
    }

    private void OnClose(object sender, RoutedEventArgs e) => BeendeUndStarte();

    private void BeendeUndStarte()
    {
        _neustart?.Stop();
        _neustart = null;

        if (_completed)
        {
            _runner.StartApplication(_options);
        }

        System.Windows.Application.Current.Shutdown();
    }

    private void Append(string line)
    {
        _log.AppendLine(line);
        LogText.Text = _log.ToString();
    }
}
