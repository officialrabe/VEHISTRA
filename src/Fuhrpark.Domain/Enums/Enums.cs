namespace Fuhrpark.Domain.Enums;

/// <summary>Bekannte Systemstatus eines Fahrzeugs. Zusaetzliche Status sind als Stammdaten pflegbar.</summary>
public enum VehicleStatusKind
{
    Aktiv = 1,
    Verfuegbar = 2,
    ImEinsatz = 3,
    Werkstatt = 4,
    Schaden = 5,
    NichtFahrbereit = 6,
    AusserBetrieb = 7,
    Abgemeldet = 8,
    Ausgemustert = 9
}

public enum FuelType
{
    Unbekannt = 0,
    Benzin = 1,
    Diesel = 2,
    Elektro = 3,
    HybridBenzin = 4,
    HybridDiesel = 5,
    PluginHybrid = 6,
    Erdgas = 7,
    Autogas = 8,
    Wasserstoff = 9,
    Sonstiges = 99
}

public enum TransmissionType
{
    Unbekannt = 0,
    Schaltgetriebe = 1,
    Automatik = 2,
    Halbautomatik = 3
}

public enum MileageSource
{
    ManuelleEingabe = 0,
    Werkstatt = 1,
    Tanken = 2,
    Hauptuntersuchung = 3,
    Schadensmeldung = 4,
    Unfallmeldung = 5,
    Import = 6,
    Ausmusterung = 7,
    Fahrerwechsel = 8
}

public enum InspectionType
{
    Hauptuntersuchung = 1,
    Abgasuntersuchung = 2,
    HauptUndAbgasuntersuchung = 3,
    Sicherheitspruefung = 4,
    UvvPruefung = 5,
    Taxameterpruefung = 6,
    Sonstige = 99
}

public enum InspectionResult
{
    Offen = 0,
    Bestanden = 1,
    BestandenMitMaengeln = 2,
    NichtBestanden = 3
}

/// <summary>Warnstufe fuer Fristen (TUEV, Wartung, Kennzeichenreservierung).</summary>
public enum WarningLevel
{
    /// <summary>Alles in Ordnung.</summary>
    Ok = 0,

    /// <summary>Hinweis - Frist naehert sich.</summary>
    Hinweis = 1,

    /// <summary>Bald faellig.</summary>
    BaldFaellig = 2,

    /// <summary>Kritisch bzw. abgelaufen.</summary>
    Kritisch = 3,

    /// <summary>Nicht relevant / inaktiv.</summary>
    Inaktiv = 4
}

public enum MaintenanceIntervalType
{
    /// <summary>Nur nach Datum.</summary>
    Datum = 1,

    /// <summary>Nur nach Kilometerstand.</summary>
    Kilometer = 2,

    /// <summary>Datum oder Kilometerstand - was zuerst eintritt.</summary>
    DatumOderKilometer = 3
}

public enum DamagePriority
{
    Niedrig = 1,
    Normal = 2,
    Hoch = 3,
    Kritisch = 4
}

public enum DamageStatus
{
    Gemeldet = 1,
    Geprueft = 2,
    ReparaturGeplant = 3,
    Werkstatt = 4,
    Repariert = 5,
    Geschlossen = 6
}

public enum DamageArea
{
    Unbekannt = 0,
    VorneLinks = 1,
    VorneMitte = 2,
    VorneRechts = 3,
    SeiteLinks = 4,
    SeiteRechts = 5,
    HintenLinks = 6,
    HintenMitte = 7,
    HintenRechts = 8,
    Dach = 9,
    Innenraum = 10,
    Unterboden = 11,
    Motorraum = 12,
    Sonstiges = 99
}

public enum AccidentType
{
    UnfallMitFremdbeteiligung = 1,
    UnfallOhneFremdbeteiligung = 2,
    ParkschadenVerursacherUnbekannt = 3,
    Wildschaden = 4,
    Glasbruch = 5,
    DiebstahlEinbruch = 6,
    Vandalismus = 7,
    SturmHagelUnwetter = 8,
    TechnischerDefekt = 9,
    Sonstiges = 99
}

public enum WorkshopOrderStatus
{
    Geplant = 1,
    TerminVereinbart = 2,
    FahrzeugAbgegeben = 3,
    InBearbeitung = 4,
    WartetAufTeile = 5,
    Fertig = 6,
    Abgeholt = 7,
    Storniert = 8
}

public enum LicensePlateStatus
{
    Verfuegbar = 1,
    Reserviert = 2,
    Vergeben = 3,
    AusserBetrieb = 4
}

public enum RetirementReason
{
    Verkauft = 1,
    Verschrottet = 2,
    WirtschaftlicherTotalschaden = 3,
    TechnischerDefekt = 4,
    Unfall = 5,
    Leasingrueckgabe = 6,
    Stillgelegt = 7,
    Ersatzteilspender = 8,
    Sonstiges = 99
}

public enum RegistrationEventType
{
    Anmeldung = 1,
    Abmeldung = 2,
    Ummeldung = 3,
    Wiederanmeldung = 4,
    Saisonkennzeichen = 5
}

public enum DocumentCategory
{
    Sonstiges = 0,
    Fahrzeugschein = 1,
    Fahrzeugbrief = 2,
    TuevBericht = 3,
    Werkstattrechnung = 4,
    Werkstattbericht = 5,
    Schadensbild = 6,
    Unfallbericht = 7,
    Gutachten = 8,
    Versicherung = 9,
    Kaufvertrag = 10,
    Leasingvertrag = 11,
    Abmeldebescheinigung = 12,
    Uebergabeprotokoll = 13
}

public enum NotificationCategory
{
    Allgemein = 0,
    Tuev = 1,
    Wartung = 2,
    Kennzeichenreservierung = 3,
    Schaden = 4,
    Unfall = 5,
    Werkstatt = 6,
    Versicherung = 7,
    System = 8,
    Update = 9
}

public enum NotificationSeverity
{
    Information = 0,
    Warnung = 1,
    Kritisch = 2
}

public enum AuditAction
{
    Created = 1,
    Updated = 2,
    Deleted = 3,
    Archived = 4,
    Restored = 5,
    Login = 6,
    LoginFailed = 7,
    Logout = 8,
    PasswordChanged = 9,
    PasswordReset = 10,
    PermissionDenied = 11,
    Exported = 12,
    Imported = 13,
    Printed = 14,
    BackupCreated = 15,
    UpdateInstalled = 16,
    MigrationApplied = 17
}

public enum InsuranceKind
{
    Haftpflicht = 1,
    Teilkasko = 2,
    Vollkasko = 3,
    Insassenunfall = 4,
    Schutzbrief = 5,
    Sonstige = 99
}

public enum UpdateOutcome
{
    Erfolgreich = 1,
    Fehlgeschlagen = 2,
    Abgebrochen = 3,
    RollbackDurchgefuehrt = 4
}
