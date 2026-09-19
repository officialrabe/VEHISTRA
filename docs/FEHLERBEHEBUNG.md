# Fehlerbehebung

Hier stehen die Probleme, die im Alltag vorkommen – jeweils mit Ursache und
konkreter Lösung. Die Meldungen sind so formuliert, wie Vehistra sie anzeigt.

> Erste Maßnahme bei fast jedem Problem: Starten Sie auf dem Server
> `VehistraServerCheck.exe`. Das Programm prüft alles Wichtige und nennt zu
> jedem Punkt den nächsten Schritt.

> Läuft Vehistra als **Solo-Platz** auf einem einzigen Computer, gibt es keinen
> Server und keinen SQL-Dienst. Die Kapitel 2 und 3 betreffen Sie dann nicht –
> zuständig ist Kapitel 11. Die Betriebsart nennt `VehistraServerCheck.exe` in
> der ersten Zeile.

---

## 1 – Das Programm startet nicht

### „Die .NET Desktop Runtime 10 wurde nicht gefunden"

Die Laufzeitumgebung von Microsoft fehlt.

1. Öffnen Sie `https://dotnet.microsoft.com/download/dotnet/10.0`
2. Laden Sie unter „Desktop Runtime" die Variante „Windows x64" herunter
3. Installieren Sie die Datei
4. Starten Sie Vehistra erneut

### Das Fenster erscheint kurz und verschwindet wieder

Sehen Sie im Protokoll nach:
`C:\ProgramData\LSP Virtual Services\Vehistra\Logs`

Öffnen Sie die neueste Datei und lesen Sie die letzten Zeilen. Schicken Sie
diese Datei an den Support, wenn Sie damit nicht weiterkommen.

---

## 2 – SERVER NICHT ERREICHBAR

Dieses Fenster erscheint im Netzwerkbetrieb, wenn Vehistra die Datenbank nicht
erreicht. Beim Solo-Platz heißt dasselbe Fenster „DATENBANKDATEI NICHT
ERREICHBAR" und nennt dort die passenden Prüfschritte – weiter bei Kapitel 11.
Es stehen vier Schaltflächen bereit:

| Schaltfläche | Was sie tut |
| --- | --- |
| Erneut versuchen | Baut die Verbindung noch einmal auf |
| Diagnose | Prüft Server, Port, Datenbank und Ordner |
| Servereinstellungen | Öffnet die Verbindungseinstellungen |
| Programm beenden | Schließt Vehistra |

Arbeiten Sie diese Punkte der Reihe nach ab:

1. **Ist der Server eingeschaltet?** Am einfachsten prüfen Sie das, indem Sie
   im Explorer `\\FUHRPARK-SRV01` eingeben.
2. **Ist der PC im Netz?** Prüfen Sie das Netzwerksymbol unten rechts.
3. **Läuft der SQL Server?** Auf dem Server: Windows-Taste, `Dienste` tippen,
   nach „SQL Server (SQLEXPRESS)" suchen. Der Status muss „Wird ausgeführt"
   lauten.
4. **Ist der Servername richtig?** Klicken Sie auf „Servereinstellungen". Der
   Name muss die Instanz enthalten, zum Beispiel `FUHRPARK-SRV01\SQLEXPRESS`.

---

## 3 – Meldungen rund um die Datenbank

### „Der SQL Server konnte nicht gefunden werden"

Der Server antwortet gar nicht.

- Läuft der Dienst „SQL Server (SQLEXPRESS)"?
- Ist im SQL Server Configuration Manager das Protokoll TCP/IP aktiviert?
- Läuft der Dienst „SQL Server-Browser"?
- Ist die Firewall für TCP 1433 und UDP 1434 freigegeben?
- Ist der Servername richtig geschrieben?

Alle Schritte stehen ausführlich in `SERVER-EINRICHTUNG-EINFACH.pdf`,
Kapitel 4 bis 6.

### „Die Anmeldung am SQL Server wurde abgelehnt"

Das verwendete Konto darf nicht auf die Datenbank zugreifen.

- Bei Windows-Authentifizierung: Ist das Windows-Konto des Mitarbeiters am SQL
  Server als Anmeldung eingetragen? Siehe Kapitel 14 der Servereinrichtung.
- Bei SQL-Authentifizierung: Stimmen Benutzername und Passwort? Ist der
  gemischte Authentifizierungsmodus am SQL Server aktiviert?

### „Die Datenbank existiert nicht"

- Wurde `VehistraServerSetup.exe` auf dem Server schon ausgeführt?
- Ist der Datenbankname richtig geschrieben (`VehistraDB`)?

### „Dem Konto fehlen Berechtigungen"

Das Konto braucht auf `VehistraDB` die Rollen `db_datareader` und
`db_datawriter`. Siehe Kapitel 14 der Servereinrichtung.

### „Zeitlimit überschritten"

Der Server antwortet zu langsam oder gar nicht.

- Ist der Server stark ausgelastet?
- Ist die Netzwerkverbindung stabil (WLAN statt Kabel)?
- Testen Sie mit `VehistraServerCheck.exe`, ob der Port erreichbar ist.

---

## 4 – Meldungen rund um Dokumente

### „Der Dokumentenordner ist nicht erreichbar"

- Geben Sie den angezeigten Pfad im Explorer ein. Lässt er sich öffnen?
- Ist die Freigabe auf dem Server noch vorhanden?
- Hat das Windows-Konto des Mitarbeiters Zugriff auf die Freigabe?

### „In den Dokumentenordner kann nicht geschrieben werden"

Windows prüft zwei Ebenen von Berechtigungen. Setzen Sie beide:

1. Rechtsklick auf den Ordner, „Eigenschaften", „Freigabe", „Erweiterte
   Freigabe", „Berechtigungen": mindestens „Ändern".
2. Registerkarte „Sicherheit": ebenfalls mindestens „Ändern".

### „Die Datei konnte nicht geöffnet werden"

- Wurde die Datei auf dem Server von Hand gelöscht oder verschoben?
- Ist auf dem Arbeitsplatz ein Programm installiert, das den Dateityp öffnen
  kann (zum Beispiel ein PDF-Betrachter)?

---

## 5 – Meldungen beim Speichern

### „Der Datensatz wurde zwischenzeitlich von jemand anderem geändert"

Zwei Personen haben dasselbe Fahrzeug gleichzeitig bearbeitet. Vehistra
überschreibt fremde Änderungen niemals stillschweigend und bietet an:

| Schaltfläche | Was sie tut |
| --- | --- |
| Aktuelle Daten laden | Verwirft Ihre Eingaben und lädt den neuen Stand |
| Änderungen vergleichen | Zeigt Feld für Feld, was sich unterscheidet |
| Abbrechen | Lässt das Formular unverändert stehen |

Vergleichen Sie zuerst, bevor Sie etwas verwerfen.

### „Das Kennzeichen ist bereits vergeben"

Ein Kennzeichen darf nur einmal aktiv sein. Prüfen Sie unter „Kennzeichen", ob
das Kennzeichen noch einem ausgemusterten Fahrzeug zugeordnet ist.

### „Der Kilometerstand ist niedriger als der letzte erfasste Wert"

Kilometerstände können nicht sinken. Prüfen Sie, ob Sie sich vertippt haben.
War der letzte Wert falsch, korrigieren Sie zuerst diesen Eintrag – die
Historie bleibt dabei erhalten und die Korrektur wird protokolliert.

---

## 6 – Meldungen beim Import

### „Die Datei konnte nicht gelesen werden"

- Ist die Datei noch in Excel geöffnet? Schließen Sie sie.
- Liegt eine `.csv` oder `.xlsx` vor? Andere Formate werden nicht gelesen.
- Ist die Datei größer als das erlaubte Maximum?

### „Pflichtfeld fehlt in Zeile …"

Die Vorschau nennt die Zeile und das Feld. Ergänzen Sie den Wert in der Datei
und starten Sie den Import erneut. Vehistra bricht den Import ab, bevor etwas
geschrieben wird – es entstehen keine halben Datenstände.

### „Der Datensatz existiert bereits"

Der Importassistent überschreibt niemals stillschweigend. Sie entscheiden je
Datensatz, ob übersprungen oder aktualisiert wird.

---

## 7 – Probleme beim Update

| Meldung | Was tun |
| --- | --- |
| „Die Prüfsumme stimmt nicht" | Updateordner auf dem Server neu befüllen |
| „Das Backup konnte nicht erstellt werden" | Backupordner und freien Platz prüfen; das Update wurde abgebrochen, die Datenbank ist unverändert |
| „Eine andere Migration läuft" | Ein anderer Arbeitsplatz aktualisiert gerade – einige Minuten warten |
| „Der Installer wurde mit Rückgabewert … beendet" | Alle Vehistra-Fenster schließen und erneut versuchen |
| „Die Updateablage ist nicht erreichbar" | Netzwerkfreigabe und Pfad prüfen |

Der Updater stellt bei einem Fehlschlag die alten Programmdateien selbst wieder
her. Mehr dazu in `UPDATE-ANLEITUNG.pdf`.

---

## 8 – Probleme beim Drucken von Berichten

### Der Werkstattbericht ist leer

- Ist ein Fahrzeug ausgewählt?
- Der Blankobericht ist absichtlich leer – er ist zum Ausfüllen von Hand
  gedacht.

### Das PDF lässt sich nicht öffnen

Auf dem Arbeitsplatz fehlt ein PDF-Betrachter. Installieren Sie einen, oder
öffnen Sie die Datei über einen Browser.

### Der Ausdruck ist abgeschnitten

Stellen Sie im Druckdialog „Tatsächliche Größe" bzw. „100 %" ein, nicht
„An Seite anpassen".

---

## 9 – Anmeldeprobleme

### „Benutzername oder Passwort ist falsch"

Nach mehreren Fehlversuchen wird das Konto vorübergehend gesperrt. Warten Sie
die angezeigte Zeit ab. Ein Administrator kann die Sperre im Bereich „Benutzer"
sofort aufheben.

### „Das Konto ist deaktiviert"

Ein Administrator hat das Konto deaktiviert, zum Beispiel weil der Mitarbeiter
ausgeschieden ist. Wenden Sie sich an Ihren Administrator.

### Das Administratorpasswort ist verloren

Ein anderer Administrator kann im Bereich „Benutzer" ein Einmalpasswort
vergeben. Gibt es keinen zweiten Administrator mehr, führen Sie auf dem Server
`VehistraServerSetup.exe` aus – Schritt 10 legt bei Bedarf ein neues
Administratorkonto an.

---

## 10 – Wenn nichts hilft: Supportpaket erstellen

1. Öffnen Sie im Programm „Hilfe & Support".
2. Klicken Sie auf „Supportpaket erstellen".
3. Wählen Sie einen Speicherort, zum Beispiel den Desktop.
4. Schicken Sie die erzeugte ZIP-Datei an `support@vehistra.dev`.
5. Beschreiben Sie kurz: Was haben Sie getan? Was ist passiert? Was hatten Sie
   erwartet?

Das Supportpaket enthält:

- die Protokolldateien
- die Programm- und Datenbankversion
- die Systemdiagnose

Es enthält ausdrücklich **nicht**:

- Passwörter
- SQL-Zugangsdaten
- Fahrzeug- oder Personendaten

Es wird nichts automatisch versendet. Sie entscheiden, was Sie weitergeben.

---

## 11 – Solo-Platz: Meldungen rund um die Datenbankdatei

Beim Solo-Platz ist die Datenbank eine Datei:

```
C:\ProgramData\LSP Virtual Services\Vehistra\Vehistra.db
```

Daneben liegen `Vehistra.db-wal` und `Vehistra.db-shm`. Diese drei Dateien
gehören zusammen. Wird nur eine davon kopiert, verschoben oder gelöscht, ist
die Datenbank unbrauchbar.

### „DATENBANKDATEI NICHT ERREICHBAR" beim Start

Vehistra findet die Datei nicht oder sie ist leer. Das Fenster nennt den
hinterlegten Ort und bietet „Diagnose", „Servereinstellungen" und „Erneut
versuchen".

- Ist der Ordner noch da? Wurde die Datei versehentlich verschoben oder
  gelöscht, spielen Sie die letzte Sicherung zurück
  (`BACKUP-UND-WIEDERHERSTELLUNG.pdf`, Kapitel 9.3).
- Wurde die Einrichtung überhaupt schon ausgeführt? Starten Sie im Startmenü
  „Datenbank einrichten" (`VehistraServerSetup.exe`).
- Vehistra legt **keine** leere Ersatzdatenbank an und arbeitet auch nicht mit
  einer weiter – eine leere Datei wird als Problem gemeldet, nicht stillschweigend
  benutzt.

### „Die Datenbankdatei konnte nicht geöffnet werden"

- Existiert der Ordner noch? Wurde er umbenannt oder verschoben?
- Hat das angemeldete Windows-Konto Schreibrechte auf den Ordner?
- Liegt die Datei auf einer Wechselfestplatte, die gerade nicht angeschlossen
  ist?

Den hinterlegten Weg zur Datei zeigt „Hilfe & Support" → „Systemdiagnose".

### „Die Datenbankdatei ist schreibgeschützt"

Rechtsklick auf die Datei → „Eigenschaften" → den Haken bei „Schreibgeschützt"
entfernen. Bleibt die Meldung, fehlen dem Windows-Konto die Rechte auf den
Ordner; ein Administrator muss sie erteilen.

### „Die Datenbankdatei wird gerade von einem anderen Programm verwendet"

- Läuft Vehistra ein zweites Mal? Im Task-Manager nach `Vehistra.exe` sehen und
  alle Fenster schließen.
- Sichert gerade ein Sicherungsprogramm oder ein Cloud-Dienst
  (OneDrive, Dropbox) den Ordner? Schließen Sie den Datenbankordner dort aus.
- **Die Datenbankdatei darf nicht auf einer Netzwerkfreigabe liegen.** Sollen
  mehrere Computer gemeinsam arbeiten, ist das der Netzwerkbetrieb – siehe
  `EINZELPLATZ-INSTALLATION.pdf`, Kapitel 7.

### „Die Datenbankdatei ist beschädigt oder keine gültige Datenbank"

Meist die Folge einer Dateikopie, die während des laufenden Programms erstellt
wurde, oder eines Stromausfalls mitten im Schreiben.

1. Vehistra schließen.
2. Die letzte Sicherung zurückspielen –
   `BACKUP-UND-WIEDERHERSTELLUNG.pdf`, Kapitel 9.3.
3. Die beschädigte Datei aufbewahren und dem Support beschreiben, was zuvor
   geschehen ist.

### „Auf dem Laufwerk ist kein Platz mehr frei"

Speicherplatz freigeben und den Vorgang wiederholen. Auch der Ordner für die
Sicherungen braucht freien Platz – eine Sicherung ist etwa so groß wie die
Datenbank selbst.

### Die Systemdiagnose meldet eine sehr kleine Datenbank

Solange das Programm läuft, stehen die jüngsten Änderungen in
`Vehistra.db-wal`. Die Diagnose zählt diese Datei mit. Weicht die Anzeige
deutlich von der Größe im Explorer ab, ist das normal und kein Fehler.

---

Vehistra – Open Fleet Management
Vehicle · Driver · Maintenance · Compliance
Developed & maintained by LSP Virtual Services
vehistra.dev · support@vehistra.dev
