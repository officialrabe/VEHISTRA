namespace Vehistra.Client.Services;

/// <summary>
/// Voreinstellung, mit der eine Liste geoeffnet wird. Das Dashboard zeigt
/// Zahlen; ein Klick darauf soll die Fahrzeuge zeigen, die dahinterstehen,
/// statt den Anwender die Liste von Hand filtern zu lassen.
/// </summary>
public enum ListPreset
{
    Keine = 0,

    // Fahrzeuge
    AlleFahrzeuge,
    AktiveFahrzeuge,
    MitFestemFahrer,
    OhneFestenFahrer,
    FahrzeugeInWerkstatt,
    FahrzeugeMitOffenenSchaeden,
    NichtFahrbereit,
    Abgemeldet,

    // Hauptuntersuchung
    TuevAbgelaufen,
    TuevIn14Tagen,
    TuevIn30Tagen,
    TuevIn60Tagen,

    // Schaeden
    OffeneSchaeden,
    KritischeSchaeden,

    // Werkstatt
    WerkstattOffen,
    WerkstattHeute,
    WerkstattUeberfaellig,

    // Kennzeichen
    KennzeichenVerfuegbar,
    KennzeichenReserviert,
    ReservierungLaeuftAus,
    ReservierungAbgelaufen,

    // Wartung
    WartungFaellig
}

/// <summary>
/// Ansichten, die eine Voreinstellung uebernehmen koennen. Wird vor
/// <see cref="ViewModels.ViewModelBase.LoadAsync"/> aufgerufen, damit die
/// Ansicht schon gefiltert erscheint und nicht erst nachlaedt.
/// </summary>
public interface IAcceptsPreset
{
    void ApplyPreset(ListPreset preset);
}
