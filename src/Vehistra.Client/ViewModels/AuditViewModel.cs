using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vehistra.Application.Abstractions;
using Vehistra.Application.Dtos;
using Vehistra.Client.Services;
using Vehistra.Domain.Enums;

namespace Vehistra.Client.ViewModels;

/// <summary>
/// Audit-Log. Die Eintraege sind unveraenderlich und koennen ueber die Anwendung
/// weder bearbeitet noch geloescht werden.
/// </summary>
public sealed partial class AuditViewModel : ViewModelBase
{
    private readonly IAuditService _audit;
    private readonly IExportService _export;
    private readonly IDialogService _dialogs;

    [ObservableProperty]
    private AuditLogListItem? _selectedEntry;

    [ObservableProperty]
    private DateTime? _from = DateTime.Today.AddDays(-30);

    [ObservableProperty]
    private DateTime? _to = DateTime.Today;

    [ObservableProperty]
    private string? _userFilter;

    [ObservableProperty]
    private string? _entityFilter;

    [ObservableProperty]
    private AuditAction? _actionFilter;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private int _pageNumber = 1;

    [ObservableProperty]
    private int _pageCount = 1;

    public AuditViewModel(IAuditService audit, IExportService export, IDialogService dialogs)
    {
        _audit = audit;
        _export = export;
        _dialogs = dialogs;
    }

    public override string Title => "Audit-Log";

    public override string? Subtitle =>
        $"{TotalCount} Einträge · Seite {PageNumber} von {PageCount} · Einträge sind unveränderlich";

    public ObservableCollection<AuditLogListItem> Entries { get; } = [];

    public ObservableCollection<string> UserNames { get; } = [];

    public ObservableCollection<string> EntityNames { get; } = [];

    public IReadOnlyList<AuditAction> ActionValues { get; } = Enum.GetValues<AuditAction>();

    public override async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        await RunAsync(async () =>
        {
            if (UserNames.Count == 0)
            {
                foreach (var name in await _audit.GetUserNamesAsync(cancellationToken).ConfigureAwait(true))
                {
                    UserNames.Add(name);
                }

                foreach (var name in await _audit.GetEntityNamesAsync(cancellationToken).ConfigureAwait(true))
                {
                    EntityNames.Add(name);
                }
            }

            var result = await _audit.GetAsync(new AuditFilter
            {
                From = From,
                To = To,
                UserName = UserFilter,
                EntityName = EntityFilter,
                Action = ActionFilter,
                SearchText = SearchText,
                PageNumber = PageNumber,
                PageSize = 200
            }, cancellationToken).ConfigureAwait(true);

            Entries.Clear();
            foreach (var entry in result.Items)
            {
                Entries.Add(entry);
            }

            TotalCount = result.TotalCount;
            PageCount = Math.Max(1, result.PageCount);
            OnPropertyChanged(nameof(Subtitle));
        }).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task ApplyFilterAsync()
    {
        PageNumber = 1;
        await LoadAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task ResetFilterAsync()
    {
        From = DateTime.Today.AddDays(-30);
        To = DateTime.Today;
        UserFilter = null;
        EntityFilter = null;
        ActionFilter = null;
        SearchText = string.Empty;
        PageNumber = 1;

        await LoadAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task NextPageAsync()
    {
        if (PageNumber >= PageCount)
        {
            return;
        }

        PageNumber++;
        await LoadAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task PreviousPageAsync()
    {
        if (PageNumber <= 1)
        {
            return;
        }

        PageNumber--;
        await LoadAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        var target = _dialogs.SaveFile("CSV-Datei (*.csv)|*.csv",
            $"Auditlog_{DateTime.Now:yyyyMMdd}.csv", "Audit-Log exportieren");

        if (string.IsNullOrWhiteSpace(target))
        {
            return;
        }

        await RunAsync(async () =>
        {
            var columns = new[]
            {
                "Zeitpunkt", "Benutzer", "Computer", "Aktion", "Entität", "Datensatz",
                "Bezeichnung", "Alte Werte", "Neue Werte", "Zusatz"
            };

            var rows = Entries.Select(e => (IReadOnlyList<string?>)
            [
                e.Timestamp.ToString("dd.MM.yyyy HH:mm:ss"), e.UserName, e.ComputerName, e.Action.ToString(),
                e.EntityName, e.EntityId, e.EntityDisplay, e.OldValues, e.NewValues, e.AdditionalInfo
            ]).ToList();

            await _export.ExportTableAsync("Audit-Log", columns, rows, ExportFormat.Csv, target)
                .ConfigureAwait(true);
        }, $"Das Audit-Log wurde exportiert: {target}").ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync().ConfigureAwait(true);
}
