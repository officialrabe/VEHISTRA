using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fuhrpark.Application.Abstractions;
using Fuhrpark.Application.Services;
using Fuhrpark.Client.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Fuhrpark.Client.ViewModels;

/// <summary>Ansichtsmodell des Anmeldefensters.</summary>
public sealed partial class LoginViewModel : ViewModelBase
{
    private readonly IAuthenticationService _authentication;
    private readonly IConnectionSettingsStore _connectionStore;
    private readonly ISettingsService _settings;
    private readonly IServiceProvider _services;
    private readonly IDialogService _dialogs;

    private string _password = string.Empty;

    [ObservableProperty]
    private string _userName = string.Empty;

    [ObservableProperty]
    private string _companyName = "Fuhrparkmanagement";

    [ObservableProperty]
    private string _serverDescription = string.Empty;

    public LoginViewModel(
        IAuthenticationService authentication,
        IConnectionSettingsStore connectionStore,
        ISettingsService settings,
        IServiceProvider services,
        IDialogService dialogs)
    {
        _authentication = authentication;
        _connectionStore = connectionStore;
        _settings = settings;
        _services = services;
        _dialogs = dialogs;
    }

    public event EventHandler? LoginSucceeded;

    public event EventHandler? ExitRequested;

    public override async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var connection = _connectionStore.Load();

        ServerDescription = connection is null
            ? "Keine Serververbindung eingerichtet"
            : $"Server: {connection.Server}{Environment.NewLine}Datenbank: {connection.Database}";

        await RunAsync(async () =>
        {
            var company = await _settings.GetCompanyProfileAsync(cancellationToken).ConfigureAwait(true);

            if (!string.IsNullOrWhiteSpace(company.Name))
            {
                CompanyName = company.Name;
            }

            // Hinweis, falls noch gar kein Administrator existiert.
            if (!await _authentication.HasAnyActiveAdministratorAsync(cancellationToken).ConfigureAwait(true))
            {
                ErrorMessage =
                    "In der Fuhrparkdatenbank ist kein aktives Administratorkonto vorhanden. " +
                    "Bitte führen Sie zuerst FuhrparkServerSetup.exe auf dem Server aus.";
            }
        }).ConfigureAwait(true);
    }

    /// <summary>Uebernimmt das Passwort aus der PasswordBox (wird nie dauerhaft gebunden).</summary>
    public void SetPassword(string password) => _password = password;

    [RelayCommand]
    private async Task LoginAsync()
    {
        ErrorMessage = null;

        await RunAsync(async () =>
        {
            var result = await _authentication.LoginAsync(UserName, _password).ConfigureAwait(true);

            if (!result.IsSuccessful)
            {
                ErrorMessage = result.ErrorMessage;
                _password = string.Empty;
                return;
            }

            if (result.MustChangePassword)
            {
                _dialogs.ShowInformation(
                    "Ihr Passwort wurde zurückgesetzt. Bitte vergeben Sie nach der Anmeldung " +
                    "unter „Hilfe & Support → Passwort ändern“ ein neues Passwort.",
                    "Passwortänderung erforderlich");
            }

            _password = string.Empty;
            LoginSucceeded?.Invoke(this, EventArgs.Empty);
        }).ConfigureAwait(true);
    }

    [RelayCommand]
    private void OpenServerSettings()
    {
        var window = _services.GetRequiredService<Views.ServerSettingsWindow>();
        window.ShowDialog();

        var connection = _connectionStore.Load();
        ServerDescription = connection is null
            ? "Keine Serververbindung eingerichtet"
            : $"Server: {connection.Server}{Environment.NewLine}Datenbank: {connection.Database}";
    }

    [RelayCommand]
    private void Exit() => ExitRequested?.Invoke(this, EventArgs.Empty);
}
