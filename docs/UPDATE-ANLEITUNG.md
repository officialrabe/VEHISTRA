# Updates einspielen

Diese Anleitung beschreibt, wie eine neue Vehistra-Version auf den Server gelegt
wird und wie die Arbeitsplätze sie bekommen.

> Die wichtigste Zusage zuerst: Ein Update löscht die Fuhrparkdatenbank niemals
> und erstellt sie niemals neu. Alle vorhandenen Daten bleiben vollständig
> erhalten. Vor jeder Datenbankänderung wird automatisch eine Sicherung
> erstellt. Schlägt diese Sicherung fehl, wird das Update abgebrochen.

---

## 1 – Wie das Update funktioniert

```
   Entwicklung              Server                    Arbeitsplaetze
   -----------              ------                    --------------
   CreateRelease.ps1  --->  Updates\1.1.0\      --->  Meldung beim Start
                            Updates\latest.json       Update ausfuehren
                                                      Programm neu starten
```

Auf dem Server liegt im Updateordner die Datei `latest.json`. Darin steht,
welche Version aktuell ist. Jeder Arbeitsplatz liest diese Datei beim Start und
meldet sich, wenn eine neuere Version bereitsteht.

---

## 2 – Aufbau des Updateordners

```
D:\Fuhrpark\Updates\
    latest.json                  beschreibt die aktuelle Version
    1.1.0\
        Vehistra-Update.exe      das eigentliche Updatepaket
        release-notes.txt        was sich geändert hat
        checksums.sha256         Prüfsummen aller Dateien
        Dokumentation\           die Anleitungen dieser Version
    Archive\
        1.0.0\                   die Vorgängerversion
```

Die Vorgängerversion bleibt im Ordner `Archive`. So können Sie im Notfall
zurück.

---

## 3 – Inhalt von latest.json

```
{
  "version": "1.1.0",
  "minimumVersion": "1.0.0",
  "installer": "1.1.0/Vehistra-Update.exe",
  "releaseNotes": "1.1.0/release-notes.txt",
  "checksum": "3f7a…",
  "mandatory": false,
  "releasedAt": "2026-03-14T09:00:00",
  "minimumDatabaseVersion": null
}
```

| Feld | Bedeutung |
| --- | --- |
| version | Die neue Programmversion |
| minimumVersion | Älteste Version, die direkt aktualisieren darf |
| installer | Pfad zum Updatepaket, relativ zum Updateordner |
| releaseNotes | Pfad zu den Versionshinweisen |
| checksum | SHA-256-Prüfsumme des Updatepakets |
| mandatory | `true` erzwingt das Update vor dem Weiterarbeiten |
| releasedAt | Zeitpunkt der Veröffentlichung |

---

## 4 – Eine neue Version bereitstellen

1. Kopieren Sie den Ordner `Release\1.1.0` aus der Lieferung nach
   `D:\Fuhrpark\Updates\1.1.0`.
2. Verschieben Sie die bisherige Version nach `D:\Fuhrpark\Updates\Archive`.
3. Kopieren Sie die Datei `latest.json` aus `Release\1.1.0` in das
   Wurzelverzeichnis `D:\Fuhrpark\Updates`.
4. Öffnen Sie `latest.json` und prüfen Sie, dass bei `installer` der Pfad
   `1.1.0/Vehistra-Update.exe` steht.

Fertig. Beim nächsten Programmstart melden die Arbeitsplätze das Update.

---

## 5 – Ein Pflichtupdate festlegen

Enthält eine Version eine wichtige Korrektur, setzen Sie in `latest.json`:

```
"mandatory": true
```

Arbeitsplätze mit einer älteren Version fordern dann zum Update auf, bevor
weitergearbeitet werden kann. Nutzen Sie das sparsam – es unterbricht die
Arbeit.

---

## 6 – Was am Arbeitsplatz passiert

1. Beim Start meldet Vehistra: „Version 1.1.0 steht bereit."
2. Der Mitarbeiter sieht die Versionshinweise und klickt auf „Jetzt aktualisieren".
3. Vehistra prüft die Prüfsumme des Updatepakets.
4. Vehistra startet `Vehistra.Updater.exe` und beendet sich selbst.
5. Der Updater arbeitet sechs Schritte ab:

| Schritt | Was passiert |
| --- | --- |
| 1 | Warten, bis das Hauptprogramm vollständig geschlossen ist |
| 2 | Die bisherigen Programmdateien werden als ZIP gesichert |
| 3 | Das Updatepaket wird still installiert |
| 4 | Die Datenbank wird auf ausstehende Änderungen geprüft |
| 5 | Sicherung der Datenbank, danach die Änderungen anwenden |
| 6 | Das Ergebnis wird im Updateprotokoll festgehalten |

6. Der Updater meldet „Fertig stellen und starten".

---

## 7 – Sicherheitsnetze beim Update

**Sicherung vor der Datenbankänderung.** Vor jeder Schemaänderung wird eine
vollständige Sicherung erstellt und mit `RESTORE VERIFYONLY` geprüft. Schlägt
das fehl, bricht der Updater ab und lässt die Datenbank unverändert.

**Migrationssperre.** Aktualisieren mehrere Arbeitsplätze gleichzeitig, setzt
der erste eine Sperre in der Datenbank. Die anderen warten. So kann die Struktur
niemals doppelt geändert werden.

**Rückfall bei Fehlern.** Schlägt die Installation der Programmdateien fehl,
stellt der Updater den vorherigen Stand aus der ZIP-Sicherung wieder her und
zeigt eine Anleitung zur Wiederherstellung an.

**Prüfsummen.** Vor der Installation wird die SHA-256-Prüfsumme des Pakets
geprüft. Stimmt sie nicht, wird das Update nicht gestartet.

---

## 8 – Versionsnummern verstehen

Vehistra verwendet semantische Versionierung `HAUPT.NEBEN.KORREKTUR`:

| Teil | Wann er steigt | Beispiel |
| --- | --- | --- |
| HAUPT | Grundlegende Änderungen, eventuell mit Anpassungsbedarf | 1.4.2 → 2.0.0 |
| NEBEN | Neue Funktionen, alles Bisherige bleibt | 1.4.2 → 1.5.0 |
| KORREKTUR | Nur Fehlerbehebungen | 1.4.2 → 1.4.3 |

---

## 9 – Nach dem Update prüfen

1. Öffnen Sie im Programm „Hilfe & Support".
2. Die angezeigte Version muss die neue sein.
3. Die Diagnose sollte in allen Zeilen „IN ORDNUNG" melden.
4. Stichprobe: Öffnen Sie ein Fahrzeug und prüfen Sie, ob die Historie
   vollständig ist.

Das Updateprotokoll finden Sie im Programm unter „Einstellungen" und in
`C:\ProgramData\LSP Virtual Services\Vehistra\Logs`.

---

## 10 – Wenn ein Update fehlschlägt

| Meldung | Bedeutung | Was tun |
| --- | --- | --- |
| „Die Prüfsumme stimmt nicht" | Die Datei ist unvollständig kopiert | Updateordner neu befüllen |
| „Das Backup konnte nicht erstellt werden" | Kein Platz oder keine Rechte im Backupordner | Backupordner prüfen, dann erneut |
| „Eine andere Migration läuft" | Ein anderer Arbeitsplatz aktualisiert gerade | Einige Minuten warten |
| „Der Installer wurde mit Rückgabewert … beendet" | Programmdateien in Benutzung | Alle Vehistra-Fenster schließen, erneut |

Der Updater stellt bei einem Fehlschlag die Programmdateien selbstständig
wieder her. Die Datenbank wird bei einem Abbruch nicht verändert.

---

## 11 – Eine Version zurücknehmen

1. Alle Arbeitsplätze schließen Vehistra.
2. Spielen Sie die Datenbanksicherung zurück, die der Updater vor der Migration
   erstellt hat – siehe `BACKUP-UND-WIEDERHERSTELLUNG.pdf`.
3. Installieren Sie auf den Arbeitsplätzen die alte Version aus
   `Updates\Archive\<Version>`.
4. Setzen Sie `latest.json` auf die alte Version zurück.

> Nehmen Sie eine Version nur zurück, wenn es sich nicht vermeiden lässt.
> Zwischen Sicherung und Rücknahme erfasste Daten gehen dabei verloren.

---

Vehistra – Open Fleet Management
Vehicle · Driver · Maintenance · Compliance
Developed & maintained by LSP Virtual Services
vehistra.dev · support@vehistra.dev
