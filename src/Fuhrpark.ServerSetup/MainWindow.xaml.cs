using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;

namespace Fuhrpark.ServerSetup;

/// <summary>
/// Hauptfenster des Einrichtungsassistenten. Enthaelt keine Geschaeftslogik -
/// alle Pruefungen laufen im <see cref="SetupViewModel"/>.
/// </summary>
[SupportedOSPlatform("windows")]
public partial class MainWindow : Window
{
    private readonly SetupViewModel _viewModel;

    public MainWindow(SetupViewModel viewModel)
    {
        _viewModel = viewModel;

        InitializeComponent();

        DataContext = viewModel;
    }

    /// <summary>Passwoerter werden nie gebunden, sondern direkt uebergeben.</summary>
    private void OnSqlPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox box)
        {
            _viewModel.SetSqlPassword(box.Password);
        }
    }

    private void OnAdminPasswordChanged(object sender, RoutedEventArgs e)
    {
        var password = AdminPasswordBox.Password;
        var repeat = AdminPasswordRepeatBox.Password;

        var matches = string.Equals(password, repeat, StringComparison.Ordinal);

        PasswordMismatchText.Visibility = matches || repeat.Length == 0
            ? Visibility.Collapsed
            : Visibility.Visible;

        _viewModel.SetAdministratorPassword(matches ? password : string.Empty);
    }

    private void OnSqlAuthenticationChecked(object sender, RoutedEventArgs e)
        => _viewModel.UseWindowsAuthentication = false;

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
