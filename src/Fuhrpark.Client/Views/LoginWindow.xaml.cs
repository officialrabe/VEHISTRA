using System.Windows;
using Fuhrpark.Client.ViewModels;

namespace Fuhrpark.Client.Views;

/// <summary>
/// Anmeldefenster. Das Passwort wird nie an das Ansichtsmodell gebunden, sondern
/// nur bei Bedarf als SecureString-naher Wert uebergeben.
/// </summary>
public partial class LoginWindow : Window
{
    private readonly LoginViewModel _viewModel;

    public LoginWindow(LoginViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = viewModel;

        viewModel.LoginSucceeded += OnLoginSucceeded;
        viewModel.ExitRequested += OnExitRequested;

        VersionText.Text = $"Version {typeof(App).Assembly.GetName().Version?.ToString(3) ?? "1.0.0"}";

        Loaded += async (_, _) =>
        {
            await viewModel.LoadAsync().ConfigureAwait(true);
            CompanyText.Text = viewModel.CompanyName;
            ServerText.Text = viewModel.ServerDescription;
            UserNameBox.Focus();
        };
    }

    private void OnPasswordChanged(object sender, RoutedEventArgs e) =>
        _viewModel.SetPassword(PasswordBox.Password);

    private void OnLoginSucceeded(object? sender, EventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void OnExitRequested(object? sender, EventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
