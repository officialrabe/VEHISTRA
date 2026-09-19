using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vehistra.Application.Abstractions;
using Vehistra.Client.Services;
using Vehistra.Client.ViewModels.Dialogs;
using Vehistra.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace Vehistra.Client.ViewModels;

/// <summary>Verwaltung der Rollen und ihrer Berechtigungen.</summary>
public sealed partial class RolesViewModel : ViewModelBase
{
    private readonly IUserService _users;
    private readonly IDialogService _dialogs;
    private readonly IServiceProvider _services;

    [ObservableProperty]
    private Role? _selectedRole;

    public RolesViewModel(IUserService users, IDialogService dialogs, IServiceProvider services)
    {
        _users = users;
        _dialogs = dialogs;
        _services = services;
    }

    public override string Title => "Rollen & Rechte";

    public override string? Subtitle =>
        "Berechtigungen werden nicht nur in der Oberfläche, sondern auch in allen Fachdiensten geprüft";

    public ObservableCollection<Role> Roles { get; } = [];

    public ObservableCollection<PermissionGroupView> Permissions { get; } = [];

    public override async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        await RunAsync(async () =>
        {
            var roles = await _users.GetRolesAsync(cancellationToken).ConfigureAwait(true);

            Roles.Clear();
            foreach (var role in roles)
            {
                Roles.Add(role);
            }

            SelectedRole = Roles.FirstOrDefault();
        }).ConfigureAwait(true);
    }

    partial void OnSelectedRoleChanged(Role? value) => ShowPermissions(value);

    private void ShowPermissions(Role? role)
    {
        Permissions.Clear();

        if (role is null)
        {
            return;
        }

        var groups = role.RolePermissions
            .Where(rp => rp.Permission is not null)
            .Select(rp => rp.Permission!)
            .GroupBy(p => p.Group)
            .OrderBy(g => g.Key);

        foreach (var group in groups)
        {
            Permissions.Add(new PermissionGroupView(
                group.Key,
                group.OrderBy(p => p.DisplayName).Select(p => p.DisplayName).ToList()));
        }
    }

    [RelayCommand]
    private async Task CreateAsync()
    {
        var dialog = _services.GetRequiredService<RoleEditViewModel>();
        await dialog.InitializeForNewAsync().ConfigureAwait(true);

        if (_dialogs.ShowDialog(dialog) == true)
        {
            await LoadAsync().ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private async Task EditAsync()
    {
        if (SelectedRole is null)
        {
            return;
        }

        var dialog = _services.GetRequiredService<RoleEditViewModel>();
        await dialog.InitializeForEditAsync(SelectedRole.Id).ConfigureAwait(true);

        if (_dialogs.ShowDialog(dialog) == true)
        {
            await LoadAsync().ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (SelectedRole is null)
        {
            return;
        }

        if (!_dialogs.Confirm($"Soll die Rolle „{SelectedRole.DisplayName}“ gelöscht werden?", "Rolle löschen"))
        {
            return;
        }

        await RunAsync(async () =>
        {
            await _users.DeleteRoleAsync(SelectedRole.Id).ConfigureAwait(true);
            await LoadAsync().ConfigureAwait(true);
        }, "Die Rolle wurde gelöscht.").ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync().ConfigureAwait(true);
}

/// <summary>Berechtigungen einer Rolle, nach Bereich gruppiert.</summary>
public sealed record PermissionGroupView(string Group, IReadOnlyList<string> Permissions);
