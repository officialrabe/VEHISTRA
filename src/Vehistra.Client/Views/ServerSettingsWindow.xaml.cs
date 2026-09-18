using System.Windows;
using System.Windows.Controls;
using Vehistra.Client.ViewModels;

namespace Vehistra.Client.Views;

/// <summary>Fenster zur Einrichtung der Serververbindung.</summary>
public partial class ServerSettingsWindow : Window
{
    private readonly ServerSettingsViewModel _viewModel;

    public ServerSettingsWindow(ServerSettingsViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = viewModel;

        viewModel.CloseRequested += OnCloseRequested;

        Loaded += async (_, _) => await viewModel.LoadAsync().ConfigureAwait(true);
    }

    private void OnCloseRequested(object? sender, bool result)
    {
        DialogResult = result;
        Close();
    }

    private void OnPasswordChanged(object sender, RoutedEventArgs e) =>
        _viewModel.SetSqlPassword(SqlPasswordBox.Password);

    private void OnSqlAuthenticationChecked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { IsChecked: true })
        {
            _viewModel.UseWindowsAuthentication = false;
        }
    }
}
