using System.Runtime.Versioning;
using System.Windows;

namespace Fuhrpark.ServerCheck;

/// <summary>
/// Hauptfenster der Serverpruefung. Die gesamte Logik liegt im
/// <see cref="ServerCheckViewModel"/>.
/// </summary>
[SupportedOSPlatform("windows")]
public partial class MainWindow : Window
{
    private readonly ServerCheckViewModel _viewModel;

    public MainWindow(ServerCheckViewModel viewModel)
    {
        _viewModel = viewModel;

        InitializeComponent();

        DataContext = viewModel;

        // Die Pruefung startet automatisch, damit der Anwender sofort ein Ergebnis sieht.
        Loaded += async (_, _) => await _viewModel.RunChecksCommand.ExecuteAsync(null);
    }
}
