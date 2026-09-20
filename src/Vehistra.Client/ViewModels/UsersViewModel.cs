using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vehistra.Application.Abstractions;
using Vehistra.Client.Services;
using Vehistra.Client.ViewModels.Dialogs;
using Vehistra.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace Vehistra.Client.ViewModels;

/// <summary>Benutzerverwaltung.</summary>
public sealed partial class UsersViewModel : ViewModelBase
{
    private readonly IUserService _users;
    private readonly IAuthenticationService _authentication;
    private readonly IDialogService _dialogs;
    private readonly IServiceProvider _services;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditCommand))]
    [NotifyCanExecuteChangedFor(nameof(ToggleActiveCommand))]
    [NotifyCanExecuteChangedFor(nameof(ResetPasswordCommand))]
    private User? _selectedUser;

    [ObservableProperty]
    private bool _includeInactive = true;

    public UsersViewModel(
        IUserService users,
        IAuthenticationService authentication,
        IDialogService dialogs,
        IServiceProvider services)
    {
        _users = users;
        _authentication = authentication;
        _dialogs = dialogs;
        _services = services;
    }

    public override string Title => "Benutzer";

    public override string? Subtitle =>
        $"{Users.Count} Benutzerkonten · {Users.Count(u => u.IsActive)} aktiv";

    public ObservableCollection<User> Users { get; } = [];

    public override async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        await RunAsync(async () =>
        {
            var users = await _users.GetUsersAsync(IncludeInactive, cancellationToken).ConfigureAwait(true);

            Users.Clear();
            foreach (var user in users)
            {
                Users.Add(user);
            }

            OnPropertyChanged(nameof(Subtitle));
        }).ConfigureAwait(true);
    }

    partial void OnIncludeInactiveChanged(bool value) => _ = LoadAsync();

    [RelayCommand]
    private async Task CreateAsync()
    {
        var dialog = _services.GetRequiredService<UserEditViewModel>();
        await dialog.InitializeForNewAsync().ConfigureAwait(true);

        if (_dialogs.ShowDialog(dialog) == true)
        {
            await LoadAsync().ConfigureAwait(true);
        }
    }

    /// <summary>Ohne Auswahl bleibt die Schaltflaeche abgeblendet statt wirkungslos.</summary>
    private bool HatAuswahl => SelectedUser is not null;

    [RelayCommand(CanExecute = nameof(HatAuswahl))]
    private async Task EditAsync()
    {
        if (SelectedUser is null)
        {
            return;
        }

        var dialog = _services.GetRequiredService<UserEditViewModel>();
        await dialog.InitializeForEditAsync(SelectedUser.Id).ConfigureAwait(true);

        if (_dialogs.ShowDialog(dialog) == true)
        {
            await LoadAsync().ConfigureAwait(true);
        }
    }

    [RelayCommand(CanExecute = nameof(HatAuswahl))]
    private async Task ToggleActiveAsync()
    {
        if (SelectedUser is null)
        {
            return;
        }

        var user = SelectedUser;
        var activate = !user.IsActive;

        if (!_dialogs.Confirm(
                activate
                    ? $"Soll das Konto „{user.UserName}“ wieder aktiviert werden?"
                    : $"Soll das Konto „{user.UserName}“ deaktiviert werden?",
                "Benutzerkonto"))
        {
            return;
        }

        await RunAsync(async () =>
        {
            await _users.SetUserActiveAsync(user.Id, activate).ConfigureAwait(true);
            await LoadAsync().ConfigureAwait(true);
        }).ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(HatAuswahl))]
    private async Task ResetPasswordAsync()
    {
        if (SelectedUser is null)
        {
            return;
        }

        if (!_dialogs.Confirm(
                $"Soll das Passwort von „{SelectedUser.UserName}“ zurückgesetzt werden?" + Environment.NewLine +
                "Der Benutzer muss beim nächsten Login ein neues Passwort vergeben.",
                "Passwort zurücksetzen"))
        {
            return;
        }

        await RunAsync(async () =>
        {
            var password = await _authentication.ResetPasswordAsync(SelectedUser.Id).ConfigureAwait(true);

            _dialogs.ShowInformation(
                $"Das Passwort wurde zurückgesetzt.{Environment.NewLine}{Environment.NewLine}" +
                $"Einmalpasswort: {password}{Environment.NewLine}{Environment.NewLine}" +
                "Bitte teilen Sie es dem Benutzer persönlich mit. Es wird nicht erneut angezeigt.",
                "Neues Passwort");

            await LoadAsync().ConfigureAwait(true);
        }).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync().ConfigureAwait(true);
}
