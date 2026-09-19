using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vehistra.Application.Abstractions;
using Vehistra.Application.Dtos;
using Vehistra.Client.Services;
using Vehistra.Client.ViewModels.Dialogs;
using Vehistra.Domain.Enums;
using Vehistra.Domain.Security;
using Microsoft.Extensions.DependencyInjection;

namespace Vehistra.Client.ViewModels;

/// <summary>Zentrale Dokumentenverwaltung.</summary>
public sealed partial class DocumentsViewModel : ViewModelBase
{
    private readonly IDocumentService _documents;
    private readonly IDocumentStorage _storage;
    private readonly INavigationService _navigation;
    private readonly ICurrentUserService _currentUser;
    private readonly IDialogService _dialogs;
    private readonly IServiceProvider _services;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenCommand))]
    [NotifyCanExecuteChangedFor(nameof(ArchiveCommand))]
    private DocumentListItem? _selectedDocument;

    [ObservableProperty]
    private DocumentCategory? _categoryFilter;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string? _storageStatus;

    public DocumentsViewModel(
        IDocumentService documents,
        IDocumentStorage storage,
        INavigationService navigation,
        ICurrentUserService currentUser,
        IDialogService dialogs,
        IServiceProvider services)
    {
        _documents = documents;
        _storage = storage;
        _navigation = navigation;
        _currentUser = currentUser;
        _dialogs = dialogs;
        _services = services;
    }

    public override string Title => "Dokumente";

    public override string? Subtitle => StorageStatus ?? $"{Documents.Count} Dokumente";

    public ObservableCollection<DocumentListItem> Documents { get; } = [];

    public IReadOnlyList<DocumentCategory> CategoryValues { get; } = Enum.GetValues<DocumentCategory>();

    public bool CanManage => _currentUser.HasPermission(Permissions.DocumentManage);

    public override async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        await RunAsync(async () =>
        {
            var probe = await _storage.ProbeAsync(cancellationToken).ConfigureAwait(true);

            StorageStatus = probe.Exists
                ? $"Ablage: {_storage.RootPath} · {Documents.Count} Dokumente"
                : probe.Message;

            var items = await _documents
                .GetListAsync(null, CategoryFilter, SearchText, cancellationToken)
                .ConfigureAwait(true);

            Documents.Clear();
            foreach (var item in items)
            {
                Documents.Add(item);
            }

            StorageStatus = probe.Exists
                ? $"Ablage: {_storage.RootPath} · {Documents.Count} Dokumente"
                : probe.Message;

            OnPropertyChanged(nameof(Subtitle));
        }).ConfigureAwait(true);
    }

    partial void OnCategoryFilterChanged(DocumentCategory? value) => _ = LoadAsync();

    partial void OnSearchTextChanged(string value) => _ = LoadAsync();

    [RelayCommand]
    private async Task AddAsync()
    {
        var dialog = _services.GetRequiredService<DocumentUploadViewModel>();
        await dialog.InitializeAsync(null).ConfigureAwait(true);

        if (_dialogs.ShowDialog(dialog) == true)
        {
            await LoadAsync().ConfigureAwait(true);
        }
    }

    /// <summary>Ohne Auswahl bleibt die Schaltflaeche abgeblendet statt wirkungslos.</summary>
    private bool HatAuswahl => SelectedDocument is not null;

    [RelayCommand(CanExecute = nameof(HatAuswahl))]
    private async Task OpenAsync(DocumentListItem? item)
    {
        var target = item ?? SelectedDocument;

        if (target is null)
        {
            return;
        }

        await RunAsync(async () =>
        {
            var path = await _documents.GetFullPathAsync(target.Id).ConfigureAwait(true);
            _dialogs.OpenInShell(path);
        }).ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(HatAuswahl))]
    private async Task ArchiveAsync()
    {
        if (SelectedDocument is null)
        {
            return;
        }

        if (!_dialogs.Confirm(
                $"Soll das Dokument „{SelectedDocument.Title}“ archiviert werden?" + Environment.NewLine +
                "Die Datei bleibt in der Ablage erhalten und wird nur ausgeblendet.",
                "Dokument archivieren"))
        {
            return;
        }

        await RunAsync(async () =>
        {
            await _documents.ArchiveAsync(SelectedDocument.Id).ConfigureAwait(true);
            await LoadAsync().ConfigureAwait(true);
        }, "Das Dokument wurde archiviert.").ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task OpenVehicleAsync()
    {
        if (SelectedDocument?.VehicleId is { } vehicleId)
        {
            await _navigation.OpenVehicleAsync(vehicleId, "documents").ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private void OpenStorageFolder()
    {
        if (_storage.IsConfigured)
        {
            _dialogs.OpenInShell(_storage.RootPath);
        }
        else
        {
            _dialogs.ShowWarning(
                "Es ist keine Dokumentenablage konfiguriert. Bitte hinterlegen Sie den Pfad in den Einstellungen.",
                "Dokumentenablage");
        }
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync().ConfigureAwait(true);
}
