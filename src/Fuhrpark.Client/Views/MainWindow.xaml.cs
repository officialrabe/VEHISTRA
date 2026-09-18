using System.Windows;
using Fuhrpark.Client.ViewModels;

namespace Fuhrpark.Client.Views;

/// <summary>Hauptfenster mit Seitenleiste, globaler Suche und Arbeitsbereich.</summary>
public partial class MainWindow : Window
{
    private readonly ShellViewModel _viewModel;

    public MainWindow(ShellViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = viewModel;

        viewModel.LogoutRequested += OnLogoutRequested;

        Loaded += async (_, _) => await viewModel.LoadAsync().ConfigureAwait(true);
    }

    private void OnToggleNotifications(object sender, RoutedEventArgs e) =>
        NotificationPopup.IsOpen = !NotificationPopup.IsOpen;

    private void OnLogoutRequested(object? sender, EventArgs e)
    {
        // Beim Abmelden wird die Anwendung beendet, damit keine Sitzungsdaten zurueckbleiben.
        System.Windows.Application.Current.Shutdown();
    }
}
