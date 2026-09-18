using System.Text;
using System.Windows;

namespace Fuhrpark.Updater;

/// <summary>Fenster der Updatekomponente mit Fortschrittsanzeige und Protokoll.</summary>
public partial class MainWindow : Window
{
    private readonly UpdateRunner _runner;
    private readonly UpdateOptions _options;
    private readonly StringBuilder _log = new();

    private bool _completed;

    public MainWindow(UpdateRunner runner, UpdateOptions options)
    {
        InitializeComponent();

        _runner = runner;
        _options = options;

        VersionText.Text = string.IsNullOrWhiteSpace(options.TargetVersion)
            ? "Update des Fuhrparkmanagements"
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
            CloseButton.Content = "_Fertig stellen und starten";
        }
    }

    private void OnClose(object sender, RoutedEventArgs e)
    {
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
