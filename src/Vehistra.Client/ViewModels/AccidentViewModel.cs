using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vehistra.Application.Abstractions;
using Vehistra.Application.Dtos;
using Vehistra.Client.Services;
using Vehistra.Client.ViewModels.Dialogs;
using Vehistra.Domain.Security;
using Microsoft.Extensions.DependencyInjection;

namespace Vehistra.Client.ViewModels;

/// <summary>Unfalluebersicht mit Zugriff auf den Unfallbericht.</summary>
public sealed partial class AccidentViewModel : ViewModelBase
{
    private readonly IAccidentService _accidents;
    private readonly IExportService _export;
    private readonly INavigationService _navigation;
    private readonly ICurrentUserService _currentUser;
    private readonly IDialogService _dialogs;
    private readonly IReportGenerator _reports;
    private readonly IServiceProvider _services;

    [ObservableProperty]
    private AccidentListItem? _selectedAccident;

    [ObservableProperty]
    private bool _onlyOpen;

    public AccidentViewModel(
        IAccidentService accidents,
        IExportService export,
        INavigationService navigation,
        ICurrentUserService currentUser,
        IDialogService dialogs,
        IReportGenerator reports,
        IServiceProvider services)
    {
        _accidents = accidents;
        _export = export;
        _navigation = navigation;
        _currentUser = currentUser;
        _dialogs = dialogs;
        _reports = reports;
        _services = services;
    }

    public override string Title => "Unfälle";

    public override string? Subtitle => $"{Accidents.Count} Unfälle · {Accidents.Count(a => a.IsOpen)} offen";

    public ObservableCollection<AccidentListItem> Accidents { get; } = [];

    public bool CanCreate => _currentUser.HasPermission(Permissions.AccidentCreate);

    public bool CanEdit => _currentUser.HasPermission(Permissions.AccidentEdit);

    public bool CanPrint => _currentUser.HasPermission(Permissions.ReportsPrint);

    public bool CanExport => _currentUser.HasPermission(Permissions.DataExport);

    public override async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        await RunAsync(async () =>
        {
            var items = await _accidents.GetListAsync(null, OnlyOpen, cancellationToken).ConfigureAwait(true);

            Accidents.Clear();
            foreach (var item in items)
            {
                Accidents.Add(item);
            }

            OnPropertyChanged(nameof(Subtitle));
        }).ConfigureAwait(true);
    }

    partial void OnOnlyOpenChanged(bool value) => _ = LoadAsync();

    [RelayCommand]
    private async Task CreateAsync()
    {
        var dialog = _services.GetRequiredService<AccidentEditViewModel>();
        await dialog.InitializeForNewAsync(null).ConfigureAwait(true);

        if (_dialogs.ShowDialog(dialog) == true)
        {
            await LoadAsync().ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private async Task EditAsync(AccidentListItem? item)
    {
        var target = item ?? SelectedAccident;

        if (target is null)
        {
            return;
        }

        var dialog = _services.GetRequiredService<AccidentEditViewModel>();
        await dialog.InitializeForEditAsync(target.Id).ConfigureAwait(true);

        if (_dialogs.ShowDialog(dialog) == true)
        {
            await LoadAsync().ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private async Task CloseAccidentAsync()
    {
        if (SelectedAccident is null)
        {
            return;
        }

        if (!_dialogs.Confirm(
                $"Soll der Unfall „{SelectedAccident.AccidentNumber}“ abgeschlossen werden?",
                "Unfall abschließen"))
        {
            return;
        }

        await RunAsync(async () =>
        {
            await _accidents.CloseAsync(SelectedAccident.Id).ConfigureAwait(true);
            await LoadAsync().ConfigureAwait(true);
        }, "Der Unfall wurde abgeschlossen.").ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task PrintReportAsync()
    {
        if (SelectedAccident is null)
        {
            return;
        }

        await RunAsync(
            async () => await _reports.CreateAccidentReportForAccidentAsync(SelectedAccident.Id).ConfigureAwait(true),
            "Der Unfallbericht wurde erstellt.").ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task OpenVehicleAsync()
    {
        if (SelectedAccident is not null)
        {
            await _navigation.OpenVehicleAsync(SelectedAccident.VehicleId, "accident").ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private async Task ExportAsync(string? format)
    {
        var exportFormat = format switch
        {
            "xlsx" => ExportFormat.Xlsx,
            "pdf" => ExportFormat.Pdf,
            _ => ExportFormat.Csv
        };

        var extension = exportFormat.ToString().ToLowerInvariant();
        var target = _dialogs.SaveFile($"{exportFormat} (*.{extension})|*.{extension}",
            $"Unfaelle_{DateTime.Now:yyyyMMdd}.{extension}", "Unfallliste exportieren");

        if (string.IsNullOrWhiteSpace(target))
        {
            return;
        }

        await RunAsync(
            async () => await _export.ExportAsync(ExportArea.Accidents, exportFormat, target).ConfigureAwait(true),
            $"Die Unfallliste wurde exportiert: {target}").ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync().ConfigureAwait(true);
}
