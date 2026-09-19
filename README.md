# Vehistra

**Open Fleet Management**
Vehicle · Driver · Maintenance · Compliance

Windows-Desktopanwendung zur Verwaltung eines betrieblichen Fuhrparks mit
zentraler Datenbank auf einem Windows-Server. Ausgelegt auf 150+ Fahrzeuge und
den gleichzeitigen Betrieb an mehreren Arbeitsplätzen.

Developed & maintained by **LSP Virtual Services** · [vehistra.dev](https://vehistra.dev) · support@vehistra.dev

---

## Aufbau

```
   Arbeitsplatz 1   \
   Arbeitsplatz 2    \
   Arbeitsplatz 3     >--- Firmennetz --->  WINDOWS SERVER
   ...               /                        |- SQL Server Express (VehistraDB)
   Arbeitsplatz 15  /                         |- Dokumentenablage
                                              |- Sicherungen
                                              '- Updateablage
```

Alle Arbeitsplätze arbeiten gleichzeitig auf derselben Datenbank. Es gibt keine
lokale Ersatzdatenbank und keinen Abgleich zwischen Kopien.

Alternativ läuft Vehistra als **Solo-Platz** auf einem einzigen Computer: Der
Installationsassistent bietet beide Betriebsarten zur Wahl und liefert beim
Solo-Platz den Einrichtungsassistenten für die örtliche Datenbank gleich mit.
SQL Server Express wird auch dann benötigt – eine dateibasierte Datenbank gibt
es bewusst nicht.

## Programme

| Datei | Zweck |
| --- | --- |
| `Vehistra.exe` | Hauptanwendung am Arbeitsplatz |
| `Vehistra-Setup.exe` | Erstinstallation, wahlweise Netzwerk- oder Solo-Platz |
| `Vehistra-Update.exe` | Updatepaket für vorhandene Installationen |
| `Vehistra.Updater.exe` | führt Programm- und Datenbankupdate aus |
| `VehistraServerSetup.exe` | zwölfschrittige Servereinrichtung |
| `VehistraServerCheck.exe` | Diagnose der Serverinstallation |

## Projektstruktur

```
src/
  Vehistra.Domain           Entitäten, Enums, Rechte, fachliche Regeln
  Vehistra.Application      Fachdienste, Schnittstellen, DTOs
  Vehistra.Infrastructure   EF Core, SQL Server, Sicherheit, Import/Export
  Vehistra.Reporting        PDF-Berichte (QuestPDF, Vektor, DIN A4)
  Vehistra.Client           WPF-Oberfläche (MVVM)
  Vehistra.Updater          Updatekomponente
  Vehistra.ServerSetup      Einrichtungsassistent
  Vehistra.ServerCheck      Diagnoseprogramm
tests/
  Vehistra.UnitTests        Domäne, Sicherheit, Berichte
  Vehistra.IntegrationTests EF Core, Dienste, Rechte, Nebenläufigkeit
tools/
  Installer                 Inno-Setup-Skripte
  ReleaseBuilder            CreateRelease.ps1
  DocBuilder                erzeugt die Anleitungen als PDF
docs/                       die fünf Anleitungen als Markdown
assets/                     Logo und mitgelieferte Schrift
```

Abhängigkeitsrichtung: `Domain` ← `Application` ← `Infrastructure` / `Reporting`
← Anwendungen. Die Domäne kennt keine Datenbank und keine Oberfläche.

## Technik

| Baustein | Auswahl |
| --- | --- |
| Laufzeit | .NET 10, C# (Nullable aktiviert) |
| Oberfläche | WPF mit MVVM (CommunityToolkit.Mvvm) |
| Datenzugriff | EF Core 10, Microsoft SQL Server |
| Berichte | QuestPDF (echte Vektor-PDFs) |
| Tabellen | ClosedXML (XLSX), CsvHelper (CSV) |
| Protokoll | Serilog mit täglicher Rotation |
| Tests | xUnit v3, Shouldly |
| Installation | Inno Setup 6 |

## Entwickeln

```
dotnet restore Vehistra.sln
dotnet build   Vehistra.sln
dotnet test
```

Die WPF-Projekte lassen sich dank `EnableWindowsTargeting` auch auf einem
Build-Agent ohne Windows übersetzen; ausgeführt werden sie nur unter Windows.

## Version erstellen

```
pwsh tools\ReleaseBuilder\CreateRelease.ps1 -Version 1.1.0 -ReleaseNotesFile notes.txt
```

Das Skript übersetzt, testet, veröffentlicht, erzeugt die Anleitungen, baut
beide Installationspakete und stellt `Release\1.1.0` mit `latest.json`,
`checksums.sha256` und der Beispielkonfiguration zusammen. Inno Setup 6 muss
installiert sein.

Codesignatur über `-SignTool <Name eines in Inno Setup hinterlegten Signierers>`.
**Das Zertifikat gehört niemals ins Repository.**

## Anleitungen

| Datei | Inhalt |
| --- | --- |
| `docs/SERVER-EINRICHTUNG-EINFACH.md` | Servereinrichtung in 22 Kapiteln |
| `docs/EINZELPLATZ-INSTALLATION.md` | Solo-Platz ohne Server einrichten |
| `docs/NEUEN-PC-IN-5-MINUTEN.md` | Arbeitsplatz einrichten |
| `docs/UPDATE-ANLEITUNG.md` | Updates bereitstellen und einspielen |
| `docs/BACKUP-UND-WIEDERHERSTELLUNG.md` | Sicherung, Wiederherstellung, Notfallplan |
| `docs/FEHLERBEHEBUNG.md` | Meldungen mit Ursache und Lösung |

Die PDF-Fassungen entstehen beim Releasebau aus denselben Dateien.

## Datenschutz und Sicherheit

- Alle Daten bleiben im Firmennetz. Keine Cloudübertragung, keine Telemetrie,
  kein Tracking, keine Werbebausteine.
- An LSP Virtual Services werden keinerlei Daten ungefragt übertragen.
- Passwörter werden mit PBKDF2-HMAC-SHA256 (210.000 Durchläufe) gehasht.
- Ein lokal gespeichertes SQL-Passwort wird ausschließlich mit Windows DPAPI
  verschlüsselt – niemals im Klartext, niemals in `appsettings.json`, niemals
  im Protokoll, niemals in der exportierten Client-Konfiguration.
- Berechtigungen werden in den Fachdiensten geprüft, nicht nur durch
  ausgeblendete Schaltflächen.
- Das Prüfprotokoll hält WER WAS WANN fest und ist für normale Mitarbeiter
  nicht einsehbar und nicht veränderbar.
- Ein Supportpaket enthält Protokolle, Versionsangaben und die Systemdiagnose –
  aber keine Passwörter, keine SQL-Zugangsdaten und keine Fahrzeug- oder
  Personendaten.
- Veränderliche Daten liegen unter `C:\ProgramData\LSP Virtual Services\Vehistra`,
  niemals unterhalb von „Programme".

## Datenbank

EF-Core-Migrationen, Code First. Ein Update löscht die Datenbank niemals und
erstellt sie niemals neu. Vor jeder Schemaänderung wird eine Sicherung erstellt
und geprüft; schlägt sie fehl, bricht das Update ab. Gleichzeitige Migrationen
mehrerer Arbeitsplätze verhindert eine Sperre in der Datenbank.

Gleichzeitiges Bearbeiten wird über einen Nebenläufigkeitsstempel erkannt. Der
zweite Speichervorgang überschreibt nichts, sondern bietet an, die aktuellen
Daten zu laden oder die Änderungen zu vergleichen.

## Lizenz

Vehistra wird künftig quelloffen über [vehistra.dev](https://vehistra.dev)
veröffentlicht. Die mitgelieferte Schrift DejaVu Sans Mono steht unter der
Bitstream-Vera-Lizenz (siehe `assets/fonts/DejaVuSansMono-LICENSE.txt`).

---

© LSP Virtual Services
