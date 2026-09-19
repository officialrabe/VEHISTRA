using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Vehistra.Application.Abstractions;
using Vehistra.Client.Services;
using Vehistra.Domain.Entities;
using Vehistra.Domain.Enums;

namespace Vehistra.Client.ViewModels.Dialogs;

/// <summary>Welcher Katalog bearbeitet wird.</summary>
public enum CatalogKind
{
    Fahrzeugkategorie,
    Schadenskategorie,
    Fahrzeugstatus
}

/// <summary>
/// Ein Eintrag aus einem der drei Stammdatenkataloge. Ein Dialog fuer alle
/// drei: gemeinsam sind Bezeichnung, Reihenfolge und Zustand, unterschiedlich
/// sind Farbe (Kategorien und Status) und die fachliche Bedeutung (nur Status).
/// </summary>
public sealed partial class CatalogEntryEditViewModel : DialogViewModelBase
{
    private readonly IVehicleService _vehicles;
    private readonly IDamageService _damages;
    private readonly IDialogService _dialogs;

    private VehicleCategory? _vehicleCategory;
    private DamageCategory? _damageCategory;
    private VehicleStatus? _status;

    [ObservableProperty]
    private CatalogKind _catalog = CatalogKind.Fahrzeugkategorie;

    [ObservableProperty]
    private bool _isNew;

    [ObservableProperty]
    private bool _isSystemEntry;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string? _description;

    [ObservableProperty]
    private string? _colorHex;

    [ObservableProperty]
    private int _sortOrder;

    [ObservableProperty]
    private bool _isActive = true;

    [ObservableProperty]
    private VehicleStatusKind? _semantic;

    [ObservableProperty]
    private bool _countsAsOperational;

    [ObservableProperty]
    private bool _countsAsAvailable;

    public CatalogEntryEditViewModel(IVehicleService vehicles, IDamageService damages, IDialogService dialogs)
    {
        _vehicles = vehicles;
        _damages = damages;
        _dialogs = dialogs;
    }

    public override string Title => IsNew
        ? $"Neuer Eintrag – {Beschriftung}"
        : $"{Beschriftung} bearbeiten";

    private string Beschriftung => Catalog switch
    {
        CatalogKind.Schadenskategorie => "Schadenskategorie",
        CatalogKind.Fahrzeugstatus => "Fahrzeugstatus",
        _ => "Fahrzeugkategorie"
    };

    /// <summary>Farbe gibt es bei Kategorien und Status, nicht bei Schadenskategorien.</summary>
    public bool ShowColor => Catalog != CatalogKind.Schadenskategorie;

    /// <summary>Die fachliche Bedeutung gibt es nur beim Fahrzeugstatus.</summary>
    public bool ShowSemantic => Catalog == CatalogKind.Fahrzeugstatus;

    /// <summary>Mitgelieferte Schadenskategorien behalten ihren Namen.</summary>
    public bool CanEditName => !(Catalog == CatalogKind.Schadenskategorie && IsSystemEntry);

    public string? NameHint => CanEditName
        ? null
        : "Mitgelieferte Schadenskategorien behalten ihren Namen: das Programm findet sie darüber, "
          + "etwa den Schaden aus einem Unfall.";

    public string? SemanticHint => Semantic is null
        ? "Ohne Bedeutung setzt kein Ablauf diesen Status. Er steht nur zur Auswahl am Fahrzeug."
        : "Die Bedeutung sagt, welchen Status ein Ablauf setzt - etwa die Ausmusterung oder eine "
          + "Werkstattbuchung. Jede Bedeutung trägt genau ein Status: trägt sie bisher ein anderer, "
          + "geben Sie sie hierüber an diesen weiter. Entfernen lässt sie sich nicht.";

    /// <summary>Auswahl der Farben – dieselben, die auch mitgeliefert werden.</summary>
    public ObservableCollection<string> Colors { get; } =
    [
        "#2E7D32", "#F9A825", "#EF6C00", "#C62828", "#1565C0",
        "#6A1B9A", "#00838F", "#455A64", "#757575"
    ];

    /// <summary>Bedeutungen zur Auswahl; der erste Eintrag ist „keine“.</summary>
    public IReadOnlyList<KeyValuePair<string, VehicleStatusKind?>> Semantics { get; } =
    [
        new("(keine)", null),
        new("Aktiv / im Bestand", VehicleStatusKind.Aktiv),
        new("Verfügbar", VehicleStatusKind.Verfuegbar),
        new("Im Einsatz", VehicleStatusKind.ImEinsatz),
        new("Werkstatt", VehicleStatusKind.Werkstatt),
        new("Schaden", VehicleStatusKind.Schaden),
        new("Nicht fahrbereit", VehicleStatusKind.NichtFahrbereit),
        new("Außer Betrieb", VehicleStatusKind.AusserBetrieb),
        new("Abgemeldet", VehicleStatusKind.Abgemeldet),
        new("Ausgemustert", VehicleStatusKind.Ausgemustert)
    ];

    public void InitializeForNew(CatalogKind catalog)
    {
        Catalog = catalog;
        IsNew = true;
        IsSystemEntry = false;
        Name = string.Empty;
        ColorHex = null;
        SortOrder = 0;
        IsActive = true;
        Semantic = null;
    }

    public void InitializeForEdit(VehicleCategory category)
    {
        Catalog = CatalogKind.Fahrzeugkategorie;
        _vehicleCategory = category;
        IsNew = false;
        IsSystemEntry = category.IsSystemCategory;
        Name = category.Name;
        Description = category.Description;
        ColorHex = category.ColorHex;
        SortOrder = category.SortOrder;
        IsActive = category.IsActive;
    }

    public void InitializeForEdit(DamageCategory category)
    {
        Catalog = CatalogKind.Schadenskategorie;
        _damageCategory = category;
        IsNew = false;
        IsSystemEntry = category.IsSystemCategory;
        Name = category.Name;
        Description = category.Description;
        SortOrder = category.SortOrder;
        IsActive = category.IsActive;
    }

    public void InitializeForEdit(VehicleStatus status)
    {
        Catalog = CatalogKind.Fahrzeugstatus;
        _status = status;
        IsNew = false;
        IsSystemEntry = status.IsSystemStatus;
        Name = status.Name;
        Description = status.Description;
        ColorHex = status.ColorHex;
        SortOrder = status.SortOrder;
        IsActive = status.IsActive;
        Semantic = status.Kind;
        CountsAsOperational = status.CountsAsOperational;
        CountsAsAvailable = status.CountsAsAvailable;
    }

    partial void OnCatalogChanged(CatalogKind value)
    {
        OnPropertyChanged(nameof(ShowColor));
        OnPropertyChanged(nameof(ShowSemantic));
        OnPropertyChanged(nameof(CanEditName));
        OnPropertyChanged(nameof(NameHint));
        OnPropertyChanged(nameof(Title));
    }

    partial void OnIsSystemEntryChanged(bool value)
    {
        OnPropertyChanged(nameof(CanEditName));
        OnPropertyChanged(nameof(NameHint));
    }

    partial void OnSemanticChanged(VehicleStatusKind? value) => OnPropertyChanged(nameof(SemanticHint));

    /// <summary>
    /// Haengt die Bedeutung an diesen Status um? Dann muss der Benutzer es
    /// wissen: ein anderer Eintrag im Katalog gibt sie dabei ab.
    /// </summary>
    private async Task<bool> BedeutungDarfUmgehaengtWerdenAsync()
    {
        if (Catalog != CatalogKind.Fahrzeugstatus || Semantic is not { } bedeutung)
        {
            return true;
        }

        if (!IsNew && _status is not null && _status.Kind == bedeutung)
        {
            return true;
        }

        var bisher = (await _vehicles.GetStatusesAsync(true).ConfigureAwait(true))
            .FirstOrDefault(s => s.Kind == bedeutung && (IsNew || s.Id != _status!.Id));

        if (bisher is null)
        {
            return true;
        }

        return _dialogs.Confirm(
            $"Die Bedeutung „{Bezeichnung(bedeutung)}“ trägt bisher der Status „{bisher.Name}“." +
            Environment.NewLine + Environment.NewLine +
            $"Soll sie an „{Name}“ übergehen? „{bisher.Name}“ bleibt bestehen und steht weiter zur " +
            "Auswahl, wird aber von keinem Ablauf mehr gesetzt. Bereits erfasste Fahrzeuge und die " +
            "Statushistorie bleiben unverändert.",
            "Bedeutung übertragen");
    }

    private string Bezeichnung(VehicleStatusKind bedeutung) =>
        Semantics.FirstOrDefault(e => e.Value == bedeutung).Key ?? bedeutung.ToString();

    protected override async Task<bool> SaveAsync()
    {
        if (!await BedeutungDarfUmgehaengtWerdenAsync().ConfigureAwait(true))
        {
            return false;
        }

        return await RunAsync(async () =>
        {
            switch (Catalog)
            {
                case CatalogKind.Fahrzeugkategorie:
                {
                    if (IsNew)
                    {
                        await _vehicles.CreateCategoryAsync(Name, ColorHex).ConfigureAwait(true);
                        return;
                    }

                    var eintrag = _vehicleCategory!;
                    eintrag.Name = Name;
                    eintrag.Description = Description;
                    eintrag.ColorHex = ColorHex;
                    eintrag.SortOrder = SortOrder;
                    eintrag.IsActive = IsActive;
                    await _vehicles.UpdateCategoryAsync(eintrag).ConfigureAwait(true);
                    return;
                }

                case CatalogKind.Schadenskategorie:
                {
                    if (IsNew)
                    {
                        await _damages.CreateCategoryAsync(Name).ConfigureAwait(true);
                        return;
                    }

                    var eintrag = _damageCategory!;
                    eintrag.Name = Name;
                    eintrag.Description = Description;
                    eintrag.SortOrder = SortOrder;
                    eintrag.IsActive = IsActive;
                    await _damages.UpdateCategoryAsync(eintrag).ConfigureAwait(true);
                    return;
                }

                default:
                {
                    if (IsNew)
                    {
                        var angelegt = await _vehicles.CreateStatusAsync(Name).ConfigureAwait(true);

                        // Farbe, Reihenfolge und Bedeutung folgen im zweiten
                        // Schritt: das Anlegen nimmt bewusst nur den Namen.
                        angelegt.ColorHex = ColorHex;
                        angelegt.Description = Description;
                        angelegt.Kind = Semantic;
                        angelegt.CountsAsOperational = CountsAsOperational;
                        angelegt.CountsAsAvailable = CountsAsAvailable;
                        await _vehicles.UpdateStatusAsync(angelegt).ConfigureAwait(true);
                        return;
                    }

                    var status = _status!;
                    status.Name = Name;
                    status.Description = Description;
                    status.ColorHex = ColorHex;
                    status.SortOrder = SortOrder;
                    status.IsActive = IsActive;
                    status.Kind = Semantic;
                    status.CountsAsOperational = CountsAsOperational;
                    status.CountsAsAvailable = CountsAsAvailable;
                    await _vehicles.UpdateStatusAsync(status).ConfigureAwait(true);
                    return;
                }
            }
        }).ConfigureAwait(true);
    }
}
