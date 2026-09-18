using System.IO;
using System.Collections.ObjectModel;
using System.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vehistra.Application.Abstractions;
using Vehistra.Application.Dtos;
using Vehistra.Client.Services;

namespace Vehistra.Client.ViewModels.Dialogs;

/// <summary>
/// Import-Assistent: Datei auswaehlen, Spalten zuordnen, Vorschau, Validierung, Import.
/// Bestehende Datensaetze werden nur auf ausdrueckliche Anweisung aktualisiert.
/// </summary>
public sealed partial class ImportWizardViewModel : DialogViewModelBase
{
    private readonly IImportService _import;
    private readonly IDialogService _dialogs;

    [ObservableProperty]
    private ExportArea _area = ExportArea.Vehicles;

    [ObservableProperty]
    private int _step = 1;

    [ObservableProperty]
    private string? _filePath;

    [ObservableProperty]
    private int _totalRows;

    [ObservableProperty]
    private bool _updateExisting;

    [ObservableProperty]
    private ImportValidationResult? _validation;

    [ObservableProperty]
    private ImportResult? _result;

    [ObservableProperty]
    private int _progress;

    public ImportWizardViewModel(IImportService import, IDialogService dialogs)
    {
        _import = import;
        _dialogs = dialogs;
    }

    public override string Title => $"Import-Assistent – {AreaDisplay}";

    public override string ConfirmButtonText => "Import starten";

    public string AreaDisplay => Area switch
    {
        ExportArea.Vehicles => "Fahrzeuge",
        ExportArea.Drivers => "Fahrer",
        ExportArea.LicensePlates => "Kennzeichen",
        _ => Area.ToString()
    };

    public ObservableCollection<ColumnMappingRow> Mappings { get; } = [];

    public ObservableCollection<string> TargetFields { get; } = [];

    public ObservableCollection<ImportIssue> Issues { get; } = [];

    public DataView? Preview { get; private set; }

    public void Initialize(ExportArea area)
    {
        Area = area;
        Step = 1;

        TargetFields.Clear();
        TargetFields.Add(string.Empty);

        foreach (var field in _import.GetTargetFields(area))
        {
            TargetFields.Add(field.DisplayName);
        }

        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(AreaDisplay));
    }

    [RelayCommand]
    private async Task ChooseFileAsync()
    {
        var path = _dialogs.OpenFile(
            "Tabellen (*.csv;*.xlsx)|*.csv;*.xlsx|CSV-Dateien (*.csv)|*.csv|Excel-Dateien (*.xlsx)|*.xlsx",
            "Importdatei auswählen");

        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        FilePath = path;

        await RunAsync(async () =>
        {
            var preview = await _import.PreviewAsync(path).ConfigureAwait(true);
            TotalRows = preview.TotalRows;

            var table = new DataTable();
            foreach (var column in preview.Columns)
            {
                table.Columns.Add(column);
            }

            foreach (var row in preview.SampleRows)
            {
                table.Rows.Add(row.ToArray<object?>());
            }

            Preview = table.DefaultView;
            OnPropertyChanged(nameof(Preview));

            var suggestion = _import.SuggestMapping(Area, preview.Columns);
            var fields = _import.GetTargetFields(Area);

            Mappings.Clear();
            foreach (var mapping in suggestion)
            {
                var display = fields.FirstOrDefault(f => f.Name == mapping.TargetField)?.DisplayName ?? string.Empty;
                Mappings.Add(new ColumnMappingRow(mapping.SourceColumn, display));
            }

            Step = 2;
        }).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task ValidateAsync()
    {
        if (string.IsNullOrWhiteSpace(FilePath))
        {
            return;
        }

        await RunAsync(async () =>
        {
            Validation = await _import.ValidateAsync(FilePath, Area, BuildMapping()).ConfigureAwait(true);

            Issues.Clear();
            foreach (var issue in Validation.Issues.Take(300))
            {
                Issues.Add(issue);
            }

            Step = 3;
        }).ConfigureAwait(true);
    }

    protected override async Task<bool> SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(FilePath))
        {
            ErrorMessage = "Bitte wählen Sie zuerst eine Datei aus.";
            return false;
        }

        if (Validation is null)
        {
            await ValidateAsync().ConfigureAwait(true);
        }

        if (Validation?.HasErrors == true)
        {
            ErrorMessage = "Die Datei enthält Fehler. Bitte korrigieren Sie diese vor dem Import.";
            return false;
        }

        var confirmed = _dialogs.Confirm(
            $"Sollen {Validation?.ValidRows ?? 0} Datensätze importiert werden?" + Environment.NewLine +
            (UpdateExisting
                ? "Bereits vorhandene Datensätze werden aktualisiert."
                : "Bereits vorhandene Datensätze werden übersprungen."),
            "Import starten");

        if (!confirmed)
        {
            return false;
        }

        var success = await RunAsync(async () =>
        {
            var progress = new Progress<int>(value => Progress = value);

            Result = await _import
                .ImportAsync(FilePath, Area, BuildMapping(), UpdateExisting, progress)
                .ConfigureAwait(true);

            Issues.Clear();
            foreach (var issue in Result.Issues.Take(300))
            {
                Issues.Add(issue);
            }

            Step = 4;
        }).ConfigureAwait(true);

        if (!success || Result is null)
        {
            return false;
        }

        _dialogs.ShowInformation(
            $"Import abgeschlossen.{Environment.NewLine}{Environment.NewLine}" +
            $"Importiert: {Result.Imported}{Environment.NewLine}" +
            $"Übersprungen: {Result.Skipped}{Environment.NewLine}" +
            $"Fehlerhaft: {Result.Failed}",
            "Import");

        return Result.Failed == 0;
    }

    private IReadOnlyList<ImportColumnMapping> BuildMapping()
    {
        var fields = _import.GetTargetFields(Area);

        return Mappings.Select(m => new ImportColumnMapping
        {
            SourceColumn = m.SourceColumn,
            TargetField = fields.FirstOrDefault(f => f.DisplayName == m.TargetField)?.Name
        }).ToList();
    }
}

/// <summary>Zeile der Spaltenzuordnung im Import-Assistenten.</summary>
public sealed partial class ColumnMappingRow : ObservableObject
{
    public ColumnMappingRow(string sourceColumn, string? targetField)
    {
        SourceColumn = sourceColumn;
        _targetField = targetField;
    }

    public string SourceColumn { get; }

    [ObservableProperty]
    private string? _targetField;
}
