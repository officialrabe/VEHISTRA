using System.IO;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vehistra.Application.Abstractions;
using Vehistra.Application.Dtos;
using Vehistra.Client.Services;
using Vehistra.Domain.Entities;
using Vehistra.Domain.Enums;

namespace Vehistra.Client.ViewModels.Dialogs;

/// <summary>Anlegen und Bearbeiten eines Benutzerkontos.</summary>
public sealed partial class UserEditViewModel : DialogViewModelBase
{
    private readonly IUserService _users;

    private User? _entity;
    private string _password = string.Empty;

    [ObservableProperty]
    private bool _isNew = true;

    [ObservableProperty]
    private string _userName = string.Empty;

    [ObservableProperty]
    private string? _firstName;

    [ObservableProperty]
    private string _lastName = string.Empty;

    [ObservableProperty]
    private string? _email;

    [ObservableProperty]
    private string? _personnelNumber;

    [ObservableProperty]
    private bool _isActive = true;

    [ObservableProperty]
    private bool _mustChangePassword = true;

    [ObservableProperty]
    private string? _comment;

    public UserEditViewModel(IUserService users)
    {
        _users = users;
    }

    public override string Title => IsNew ? "Neuen Benutzer anlegen" : "Benutzer bearbeiten";

    public ObservableCollection<RoleSelection> Roles { get; } = [];

    public void SetPassword(string password) => _password = password;

    public async Task InitializeForNewAsync()
    {
        IsNew = true;
        await LoadRolesAsync().ConfigureAwait(true);
    }

    public async Task InitializeForEditAsync(int userId)
    {
        IsNew = false;
        await LoadRolesAsync().ConfigureAwait(true);

        _entity = await _users.GetUserAsync(userId).ConfigureAwait(true);

        if (_entity is null)
        {
            ErrorMessage = "Der Benutzer wurde nicht gefunden.";
            return;
        }

        UserName = _entity.UserName;
        FirstName = _entity.FirstName;
        LastName = _entity.LastName;
        Email = _entity.Email;
        PersonnelNumber = _entity.PersonnelNumber;
        IsActive = _entity.IsActive;
        MustChangePassword = _entity.MustChangePassword;
        Comment = _entity.Comment;

        var assigned = _entity.UserRoles.Select(ur => ur.RoleId).ToHashSet();

        foreach (var selection in Roles)
        {
            selection.IsSelected = assigned.Contains(selection.Role.Id);
        }
    }

    protected override async Task<bool> SaveAsync()
    {
        var roleIds = Roles.Where(r => r.IsSelected).Select(r => r.Role.Id).ToList();

        if (roleIds.Count == 0)
        {
            ErrorMessage = "Bitte weisen Sie dem Benutzer mindestens eine Rolle zu.";
            return false;
        }

        return await RunAsync(async () =>
        {
            if (IsNew)
            {
                await _users.CreateUserAsync(new User
                {
                    UserName = UserName?.Trim() ?? string.Empty,
                    FirstName = FirstName?.Trim() ?? string.Empty,
                    LastName = LastName?.Trim() ?? string.Empty,
                    Email = Email?.Trim(),
                    PersonnelNumber = PersonnelNumber?.Trim(),
                    IsActive = IsActive,
                    MustChangePassword = MustChangePassword,
                    Comment = Comment
                }, _password, roleIds).ConfigureAwait(true);

                return;
            }

            var user = _entity!;
            user.UserName = UserName?.Trim() ?? string.Empty;
            user.FirstName = FirstName?.Trim() ?? string.Empty;
            user.LastName = LastName?.Trim() ?? string.Empty;
            user.Email = Email?.Trim();
            user.PersonnelNumber = PersonnelNumber?.Trim();
            user.IsActive = IsActive;
            user.MustChangePassword = MustChangePassword;
            user.Comment = Comment;

            await _users.UpdateUserAsync(user, roleIds).ConfigureAwait(true);
        }).ConfigureAwait(true);
    }

    private async Task LoadRolesAsync()
    {
        if (Roles.Count > 0)
        {
            return;
        }

        foreach (var role in await _users.GetRolesAsync().ConfigureAwait(true))
        {
            Roles.Add(new RoleSelection(role));
        }
    }
}

/// <summary>Auswahl einer Rolle im Benutzerdialog.</summary>
public sealed partial class RoleSelection : ObservableObject
{
    public RoleSelection(Role role)
    {
        Role = role;
    }

    public Role Role { get; }

    public string DisplayName => Role.DisplayName;

    public string? Description => Role.Description;

    [ObservableProperty]
    private bool _isSelected;
}

/// <summary>Anlegen und Bearbeiten einer Rolle inklusive Berechtigungen.</summary>
public sealed partial class RoleEditViewModel : DialogViewModelBase
{
    private readonly IUserService _users;

    private Role? _entity;

    [ObservableProperty]
    private bool _isNew = true;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string? _description;

    public RoleEditViewModel(IUserService users)
    {
        _users = users;
    }

    public override string Title => IsNew ? "Neue Rolle anlegen" : "Rolle bearbeiten";

    public ObservableCollection<PermissionGroupSelection> PermissionGroups { get; } = [];

    public async Task InitializeForNewAsync()
    {
        IsNew = true;
        await LoadPermissionsAsync([]).ConfigureAwait(true);
    }

    public async Task InitializeForEditAsync(int roleId)
    {
        IsNew = false;

        _entity = await _users.GetRoleAsync(roleId).ConfigureAwait(true);

        if (_entity is null)
        {
            ErrorMessage = "Die Rolle wurde nicht gefunden.";
            return;
        }

        Name = _entity.Name;
        DisplayName = _entity.DisplayName;
        Description = _entity.Description;

        var assigned = await _users.GetRolePermissionNamesAsync(roleId).ConfigureAwait(true);
        await LoadPermissionsAsync(assigned).ConfigureAwait(true);
    }

    protected override async Task<bool> SaveAsync()
    {
        var permissions = PermissionGroups
            .SelectMany(g => g.Permissions)
            .Where(p => p.IsSelected)
            .Select(p => p.Permission.Name)
            .ToList();

        return await RunAsync(async () =>
        {
            if (IsNew)
            {
                await _users.CreateRoleAsync(new Role
                {
                    Name = Name?.Trim().ToUpperInvariant() ?? string.Empty,
                    DisplayName = DisplayName?.Trim() ?? string.Empty,
                    Description = Description
                }, permissions).ConfigureAwait(true);

                return;
            }

            var role = _entity!;
            role.DisplayName = DisplayName?.Trim() ?? string.Empty;
            role.Description = Description;

            await _users.UpdateRoleAsync(role, permissions).ConfigureAwait(true);
        }).ConfigureAwait(true);
    }

    private async Task LoadPermissionsAsync(IReadOnlyList<string> assigned)
    {
        PermissionGroups.Clear();

        var permissions = await _users.GetPermissionsAsync().ConfigureAwait(true);
        var assignedSet = assigned.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var group in permissions.GroupBy(p => p.Group).OrderBy(g => g.Key))
        {
            var selections = group
                .OrderBy(p => p.DisplayName)
                .Select(p => new PermissionSelection(p) { IsSelected = assignedSet.Contains(p.Name) })
                .ToList();

            PermissionGroups.Add(new PermissionGroupSelection(group.Key, selections));
        }
    }
}

/// <summary>Berechtigungen eines Bereichs im Rollendialog.</summary>
public sealed partial class PermissionGroupSelection : ObservableObject
{
    public PermissionGroupSelection(string group, IReadOnlyList<PermissionSelection> permissions)
    {
        Group = group;
        Permissions = permissions;
    }

    public string Group { get; }

    public IReadOnlyList<PermissionSelection> Permissions { get; }

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var permission in Permissions)
        {
            permission.IsSelected = true;
        }
    }

    [RelayCommand]
    private void SelectNone()
    {
        foreach (var permission in Permissions)
        {
            permission.IsSelected = false;
        }
    }
}

/// <summary>Auswahl einer einzelnen Berechtigung.</summary>
public sealed partial class PermissionSelection : ObservableObject
{
    public PermissionSelection(Permission permission)
    {
        Permission = permission;
    }

    public Permission Permission { get; }

    public string DisplayName => Permission.DisplayName;

    public string Name => Permission.Name;

    [ObservableProperty]
    private bool _isSelected;
}

/// <summary>Hochladen eines Dokuments in die zentrale Ablage.</summary>
public sealed partial class DocumentUploadViewModel : DialogViewModelBase
{
    private readonly IDocumentService _documents;
    private readonly IVehicleService _vehicles;
    private readonly IDialogService _dialogs;

    [ObservableProperty]
    private VehicleListItem? _selectedVehicle;

    [ObservableProperty]
    private DocumentCategory _category = DocumentCategory.Sonstiges;

    [ObservableProperty]
    private string _documentTitle = string.Empty;

    [ObservableProperty]
    private string? _description;

    [ObservableProperty]
    private DateTime? _documentDate = DateTime.Today;

    [ObservableProperty]
    private DateTime? _validUntil;

    [ObservableProperty]
    private string? _filePath;

    public DocumentUploadViewModel(
        IDocumentService documents,
        IVehicleService vehicles,
        IDialogService dialogs)
    {
        _documents = documents;
        _vehicles = vehicles;
        _dialogs = dialogs;
    }

    public override string Title => "Dokument hinzufügen";

    public ObservableCollection<VehicleListItem> Vehicles { get; } = [];

    public IReadOnlyList<DocumentCategory> Categories { get; } = Enum.GetValues<DocumentCategory>();

    public async Task InitializeAsync(int? vehicleId)
    {
        await RunAsync(async () =>
        {
            var vehicles = await _vehicles.GetListAsync(new VehicleFilter { IsRetired = null, PageSize = 500 })
                .ConfigureAwait(true);

            Vehicles.Clear();
            foreach (var vehicle in vehicles.Items)
            {
                Vehicles.Add(vehicle);
            }

            SelectedVehicle = Vehicles.FirstOrDefault(v => v.Id == vehicleId);
        }).ConfigureAwait(true);
    }

    [RelayCommand]
    private void BrowseFile()
    {
        var path = _dialogs.OpenFile(
            "Dokumente und Bilder|*.pdf;*.jpg;*.jpeg;*.png;*.docx;*.xlsx;*.txt|Alle Dateien (*.*)|*.*",
            "Datei auswählen");

        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        FilePath = path;

        if (string.IsNullOrWhiteSpace(DocumentTitle))
        {
            DocumentTitle = Path.GetFileNameWithoutExtension(path);
        }
    }

    protected override async Task<bool> SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(FilePath))
        {
            ErrorMessage = "Bitte wählen Sie eine Datei aus.";
            return false;
        }

        return await RunAsync(async () =>
            await _documents.AddFromFileAsync(FilePath, new VehicleDocument
            {
                VehicleId = SelectedVehicle?.Id,
                Category = Category,
                Title = DocumentTitle?.Trim() ?? string.Empty,
                Description = Description,
                DocumentDate = DocumentDate,
                ValidUntil = ValidUntil
            }).ConfigureAwait(true)).ConfigureAwait(true);
    }
}
