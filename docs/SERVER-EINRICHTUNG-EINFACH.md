# Server einrichten – Schritt für Schritt

Diese Anleitung führt Sie durch die komplette Einrichtung des Vehistra-Servers.
Sie ist so geschrieben, dass sie ohne Vorkenntnisse in Datenbanken oder
Netzwerktechnik befolgt werden kann. Arbeiten Sie die Kapitel der Reihe nach ab
und haken Sie jedes erledigte Kapitel ab.

Wenn etwas nicht klappt: In Kapitel 19 stehen die häufigsten Probleme mit
Lösung. Kommen Sie damit nicht weiter, hilft der Support unter
support@vehistra.dev weiter.

> Wichtig: Es wird nichts an Ihrem Netzwerk verändert, was Sie nicht selbst
> bestätigen. Das Einrichtungsprogramm installiert insbesondere keinen SQL
> Server im Hintergrund – Sie installieren ihn in Kapitel 3 bewusst selbst.

---

## Kapitel 1 – Was Sie brauchen

Bevor Sie anfangen, sollten diese Dinge bereitstehen:

- Ein Windows-Server oder ein Windows-PC, der immer läuft. Dieser Computer wird
  ab jetzt „der Server" genannt.
- Administratorrechte auf dem Server. Ohne diese Rechte lässt sich weder der
  SQL Server noch Vehistra einrichten.
- Etwa 60 Minuten Zeit. Die eigentliche Einrichtung dauert 15 Minuten, der
  Download des SQL Servers kann je nach Leitung länger dauern.
- Die Datei `VehistraServerSetup.exe` aus dem Ordner `Serverwerkzeuge` Ihrer
  Vehistra-Lieferung.
- Einen Namen für den Server, den Sie sich merken können, zum Beispiel
  `FUHRPARK-SRV01`.

### Mindestanforderungen an den Server

| Was | Minimum | Empfohlen |
| --- | --- | --- |
| Betriebssystem | Windows 10 (64 Bit) | Windows Server 2019 oder neuer |
| Arbeitsspeicher | 8 GB | 16 GB |
| Freier Speicherplatz | 20 GB | 100 GB auf einem Datenlaufwerk |
| Netzwerk | 100 Mbit/s | 1 Gbit/s |
| Sicherung | externe Festplatte | Bandlaufwerk oder NAS mit Versionierung |

Der Server muss nicht leistungsstark sein. Bei 150 Fahrzeugen und zehn
gleichzeitigen Arbeitsplätzen genügt ein normaler Büro-PC mit 16 GB
Arbeitsspeicher und einer SSD.

---

## Kapitel 2 – Wie Vehistra aufgebaut ist

Vehistra besteht aus zwei Teilen:

1. Auf jedem Arbeitsplatz-PC läuft das Programm `Vehistra.exe`.
2. Auf dem Server liegt die gemeinsame Datenbank mit allen Fahrzeugdaten.

Alle Arbeitsplätze arbeiten gleichzeitig mit derselben Datenbank. Ändert jemand
einen Kilometerstand, sehen die anderen die Änderung sofort. Es gibt keine
lokalen Kopien, die abgeglichen werden müssten.

Zusätzlich liegen auf dem Server drei Ordner:

- Der Dokumentenordner mit Fahrzeugscheinen, Rechnungen und Fotos.
- Der Backupordner mit den Sicherungen der Datenbank.
- Der Updateordner mit neuen Programmversionen.

```
   Arbeitsplatz 1   \
   Arbeitsplatz 2    \
   Arbeitsplatz 3     >--- Firmennetz --->  SERVER
   ...               /                        |- SQL Server (VehistraDB)
   Arbeitsplatz 15  /                         |- Dokumentenordner
                                              |- Backupordner
                                              '- Updateordner
```

---

## Kapitel 3 – Microsoft SQL Server Express installieren

Die Datenbank benötigt „Microsoft SQL Server Express". Das ist ein kostenloses
Programm von Microsoft. Vehistra installiert es bewusst nicht im Hintergrund,
damit Sie die Kontrolle über Ihren Server behalten.

1. Öffnen Sie auf dem Server die Seite
   `https://www.microsoft.com/de-de/sql-server/sql-server-downloads`
2. Klicken Sie im Bereich „Express" auf „Jetzt herunterladen".
3. Starten Sie die heruntergeladene Datei.
4. Wählen Sie den Installationstyp „Basic" bzw. „Standard".
5. Bestätigen Sie die Lizenzbedingungen.
6. Behalten Sie den vorgeschlagenen Instanznamen `SQLEXPRESS` bei.
7. Warten Sie, bis die Installation abgeschlossen ist.
8. Notieren Sie sich die angezeigte Verbindungszeichenfolge – Sie brauchen sie
   gleich nicht zwingend, aber sie ist ein guter Beleg.

> Merken Sie sich: Der Servername setzt sich aus Computername und Instanzname
> zusammen, zum Beispiel `FUHRPARK-SRV01\SQLEXPRESS`. Der Backslash gehört dazu.

### Warum Express und nicht die Vollversion?

SQL Server Express ist kostenlos und erlaubt Datenbanken bis 10 GB. Für 150
Fahrzeuge mit zehn Jahren Historie reicht das mit großem Abstand – eine solche
Datenbank ist typischerweise unter 500 MB groß. Dokumente und Fotos liegen im
Dateisystem und zählen nicht zu diesem Limit.

---

## Kapitel 4 – TCP/IP im SQL Server aktivieren

Damit die Arbeitsplätze den Server erreichen, muss das Netzwerkprotokoll
TCP/IP eingeschaltet sein. Nach einer Neuinstallation ist es oft aus.

1. Drücken Sie die Windows-Taste und tippen Sie `SQL Server Configuration Manager`.
2. Öffnen Sie links „SQL Server-Netzwerkkonfiguration".
3. Klicken Sie auf „Protokolle für SQLEXPRESS".
4. Klicken Sie rechts mit der rechten Maustaste auf „TCP/IP".
5. Wählen Sie „Aktivieren" und bestätigen Sie den Hinweis.
6. Öffnen Sie links „SQL Server-Dienste".
7. Rechtsklick auf „SQL Server (SQLEXPRESS)" und „Neu starten".

Liegt die Datenbank auf demselben Computer wie das Programm, ist dieser Schritt
nicht nötig. Für den Mehrplatzbetrieb ist er Pflicht.

---

## Kapitel 5 – SQL Server-Browser starten

Der Dienst „SQL Server-Browser" teilt den Arbeitsplätzen mit, über welchen Port
die Instanz `SQLEXPRESS` erreichbar ist. Ohne diesen Dienst finden die
Arbeitsplätze eine benannte Instanz nicht.

1. Bleiben Sie im SQL Server Configuration Manager unter „SQL Server-Dienste".
2. Rechtsklick auf „SQL Server-Browser" und „Eigenschaften".
3. Auf der Registerkarte „Dienst" den Startmodus auf „Automatisch" stellen.
4. Mit „OK" bestätigen.
5. Rechtsklick auf „SQL Server-Browser" und „Starten".

---

## Kapitel 6 – Windows-Firewall freigeben

Die Windows-Firewall blockiert Datenbankzugriffe von außen. Geben Sie zwei
Ports frei.

1. Drücken Sie die Windows-Taste und tippen Sie `Windows Defender Firewall mit erweiterter Sicherheit`.
2. Klicken Sie links auf „Eingehende Regeln".
3. Klicken Sie rechts auf „Neue Regel".
4. Wählen Sie „Port" und dann „Weiter".
5. Wählen Sie „TCP" und tragen Sie bei „Bestimmte lokale Ports" `1433` ein.
6. Wählen Sie „Verbindung zulassen".
7. Lassen Sie alle drei Profile angehakt oder mindestens „Domäne" und „Privat".
8. Vergeben Sie den Namen `Vehistra – SQL Server (TCP 1433)`.
9. Wiederholen Sie die Schritte 3 bis 8 für **UDP Port 1434**
   (Name: `Vehistra – SQL Server-Browser (UDP 1434)`).

> Öffnen Sie diese Ports niemals ins Internet. Sie werden ausschließlich im
> internen Firmennetz gebraucht.

---

## Kapitel 7 – Ordnerstruktur auf dem Server anlegen

Legen Sie die Ordner am besten auf einem Datenlaufwerk an, nicht auf `C:`.
So bleibt der Systemdatenträger frei und die Sicherung wird einfacher.

```
D:\Fuhrpark\
    Dokumente\       Fahrzeugscheine, Rechnungen, Fotos
    Backups\         Sicherungen der Datenbank
    Updates\         neue Programmversionen
        Archive\     ältere Versionen
```

Legen Sie diese Ordner jetzt im Windows-Explorer an. Das Einrichtungsprogramm
kann das in den Schritten 6 bis 8 auch für Sie übernehmen.

---

## Kapitel 8 – Ordner im Netzwerk freigeben

Die Arbeitsplätze müssen den Dokumenten- und den Updateordner erreichen.

1. Rechtsklick auf `D:\Fuhrpark` und „Eigenschaften".
2. Registerkarte „Freigabe" und dann „Erweiterte Freigabe".
3. Haken bei „Diesen Ordner freigeben" setzen.
4. Freigabenamen `Fuhrpark` vergeben.
5. Auf „Berechtigungen" klicken.
6. Der Gruppe, in der Ihre Mitarbeiter sind (zum Beispiel `Domänen-Benutzer`),
   das Recht „Ändern" erteilen. „Jeder" sollte entfernt werden.
7. Zweimal mit „OK" bestätigen.
8. Wechseln Sie auf die Registerkarte „Sicherheit" und erteilen Sie derselben
   Gruppe dort ebenfalls „Ändern".

Der Netzwerkpfad lautet danach `\\FUHRPARK-SRV01\Fuhrpark`.

### Warum zweimal Berechtigungen?

Windows prüft bei Netzwerkzugriffen zwei Ebenen: die Freigabeberechtigung und
die NTFS-Berechtigung („Sicherheit"). Es gilt immer die strengere der beiden.
Deshalb müssen Sie beide setzen.

---

## Kapitel 9 – Das Einrichtungsprogramm starten

1. Kopieren Sie den Ordner `Serverwerkzeuge` aus Ihrer Vehistra-Lieferung auf
   den Server, zum Beispiel nach `C:\Vehistra-Setup`.
2. Klicken Sie mit der rechten Maustaste auf `VehistraServerSetup.exe`.
3. Wählen Sie „Als Administrator ausführen".
4. Bestätigen Sie die Nachfrage von Windows mit „Ja".

Es öffnet sich ein Fenster mit zwölf Schritten auf der linken Seite. Jeder
Schritt erklärt, was er tut. Sie klicken jeweils auf die blaue Schaltfläche und
danach auf „Weiter".

---

## Kapitel 10 – Die zwölf Schritte im Einzelnen

| Schritt | Was passiert | Worauf achten |
| --- | --- | --- |
| 1 Systemprüfung | Betriebssystem, Rechte, Speicherplatz | Es müssen Administratorrechte gemeldet werden |
| 2 SQL Server erkennen | sucht installierte Instanzen | Wird nichts gefunden: Kapitel 3 wiederholen |
| 3 Verbindung testen | stellt die Verbindung her | Servername inklusive `\SQLEXPRESS` |
| 4 Datenbank | legt `VehistraDB` an | Vorhandene Datenbank wird weiterverwendet |
| 5 Datenbankstruktur | erzeugt Tabellen und Stammdaten | Dauert einige Sekunden |
| 6 Dokumentenordner | legt den Ordner an | Pfad aus Kapitel 7 eintragen |
| 7 Backupordner | legt den Ordner an | Am besten auf einem anderen Laufwerk |
| 8 Updateordner | legt Ordner und `latest.json` an | Unterordner `Archive` entsteht automatisch |
| 9 Netzwerkfreigaben | prüft Erreichbarkeit und Schreibrechte | UNC-Pfade aus Kapitel 8 eintragen |
| 10 Erster Administrator | legt das erste Benutzerkonto an | Passwort sicher notieren |
| 11 Abschlussprüfung | prüft alles noch einmal | Alle Zeilen sollten „IN ORDNUNG" zeigen |
| 12 Client-Konfiguration | erzeugt die `.fmcfg`-Datei | Diese Datei brauchen Sie für Kapitel 13 |

---

## Kapitel 11 – Anmeldeverfahren wählen

In Schritt 3 entscheiden Sie, wie sich Vehistra am SQL Server anmeldet.

**Windows-Authentifizierung (empfohlen).** Der Server erkennt die Mitarbeiter an
ihrem Windows-Konto. Es muss kein Passwort gespeichert werden. Das ist die
sicherste Variante und in einer Domäne immer die richtige Wahl.

**SQL-Server-Anmeldung.** Benutzername und Passwort werden im Programm
hinterlegt. Diese Variante brauchen Sie nur, wenn die Arbeitsplätze nicht in
derselben Domäne sind wie der Server.

> Wird ein SQL-Passwort gespeichert, verschlüsselt Vehistra es mit der
> Windows-Datenschutz-API (DPAPI) und bindet es an diesen einen Computer. Es
> steht niemals im Klartext in einer Konfigurationsdatei, niemals in einem
> Protokoll und niemals in der Client-Konfigurationsdatei.

---

## Kapitel 12 – Das erste Administratorkonto

In Schritt 10 legen Sie das erste Benutzerkonto an. Dieses Konto darf alles und
kann anschließend im Programm weitere Benutzer anlegen.

- Benutzername: kurz und eindeutig, zum Beispiel `admin` oder `m.mueller`
- Passwort: mindestens zehn Zeichen, Groß- und Kleinbuchstaben, eine Ziffer
- Notieren Sie das Passwort und legen Sie es an einen sicheren Ort

Vehistra speichert Passwörter niemals im Klartext. Sie werden mit PBKDF2 und
210.000 Durchläufen gehasht. Selbst wer die Datenbank liest, kann daraus kein
Passwort zurückrechnen.

---

## Kapitel 13 – Die Client-Konfiguration verteilen

Schritt 12 erzeugt die Datei `Vehistra-Firmenkonfiguration.fmcfg` auf dem
Desktop. Darin stehen Servername, Datenbankname und die Ordnerpfade – **keine
Passwörter**.

Legen Sie diese Datei in die Freigabe, zum Beispiel nach
`\\FUHRPARK-SRV01\Fuhrpark\Vehistra-Firmenkonfiguration.fmcfg`.

Beim Einrichten eines neuen Arbeitsplatzes wird sie einfach ausgewählt – siehe
die Anleitung `NEUEN-PC-IN-5-MINUTEN.pdf`.

---

## Kapitel 14 – Datenbankberechtigungen prüfen

Die Mitarbeiter brauchen auf der Datenbank `VehistraDB` Lese- und Schreibrechte.
Bei Windows-Authentifizierung legen Sie dazu am besten eine Gruppe an.

1. Öffnen Sie „SQL Server Management Studio" (kostenlos bei Microsoft).
2. Verbinden Sie sich mit `FUHRPARK-SRV01\SQLEXPRESS`.
3. Öffnen Sie „Sicherheit" und dann „Anmeldungen".
4. Rechtsklick, „Neue Anmeldung", Windows-Gruppe auswählen.
5. Links auf „Benutzerzuordnung" klicken.
6. Bei `VehistraDB` einen Haken setzen.
7. Unten `db_datareader` und `db_datawriter` anhaken.
8. Mit „OK" bestätigen.

Mehr Rechte sind im Normalbetrieb nicht nötig. Für Programmupdates, die die
Datenbankstruktur ändern, wird zusätzlich `db_owner` benötigt – dieses Recht
sollte nur das Administratorkonto besitzen.

---

## Kapitel 15 – Sicherung einrichten

Eine Sicherung, die nie getestet wurde, ist keine Sicherung. Richten Sie beides
ein: das automatische Erstellen und das regelmäßige Prüfen.

Empfohlener Aufbewahrungsplan:

- 7 tägliche Sicherungen
- 4 wöchentliche Sicherungen
- 12 monatliche Sicherungen

Vehistra erstellt vor jeder Datenbankänderung durch ein Update automatisch eine
Sicherung. Das ersetzt aber keine regelmäßige Sicherung.

Die vollständige Anleitung mit dem fertigen Skript für die Aufgabenplanung steht
in `BACKUP-UND-WIEDERHERSTELLUNG.pdf`.

---

## Kapitel 16 – Updateordner vorbereiten

Neue Vehistra-Versionen legen Sie in den Updateordner. Die Arbeitsplätze melden
das Update beim nächsten Programmstart von selbst.

```
D:\Fuhrpark\Updates\
    latest.json              beschreibt die aktuelle Version
    1.1.0\
        Vehistra-Update.exe
        release-notes.txt
        checksums.sha256
    Archive\
        1.0.0\               ältere Version zur Sicherheit
```

Die genaue Vorgehensweise steht in `UPDATE-ANLEITUNG.pdf`.

---

## Kapitel 17 – Die Einrichtung prüfen

Starten Sie auf dem Server `VehistraServerCheck.exe`. Das Programm prüft in
wenigen Sekunden alles Wichtige und schreibt zu jedem Punkt im Klartext, was zu
tun ist.

Geprüft werden unter anderem:

- Ist eine Serververbindung hinterlegt?
- Ist der Server im Netz erreichbar und ist Port 1433 offen?
- Existiert die Datenbank und ist ihre Struktur aktuell?
- Darf das verwendete Konto lesen und schreiben?
- Gibt es ein aktives Administratorkonto?
- Sind Dokumenten-, Backup- und Updateordner erreichbar und beschreibbar?
- Reicht der freie Speicherplatz?

Alle Zeilen sollten das Kennzeichen „IN ORDNUNG" tragen. Mit „Bericht speichern"
legen Sie das Ergebnis als Textdatei ab – hilfreich für den Support.

---

## Kapitel 18 – Den ersten Arbeitsplatz einrichten

1. Kopieren Sie `Vehistra-Setup.exe` auf den Arbeitsplatz-PC.
2. Starten Sie die Datei und folgen Sie dem Assistenten.
3. Starten Sie Vehistra über die neue Desktop-Verknüpfung.
4. Wählen Sie im Startdialog „Firmenkonfiguration laden" und geben Sie den Pfad
   aus Kapitel 13 an.
5. Melden Sie sich mit dem Administratorkonto aus Kapitel 12 an.

Die ausführliche Fassung steht in `NEUEN-PC-IN-5-MINUTEN.pdf`.

---

## Kapitel 19 – Wenn etwas nicht funktioniert

| Meldung | Ursache | Lösung |
| --- | --- | --- |
| „Der SQL Server konnte nicht gefunden werden" | Dienst läuft nicht, TCP/IP aus oder Firewall zu | Kapitel 4, 5 und 6 wiederholen |
| „Die Anmeldung wurde abgelehnt" | Konto hat keinen Zugriff | Kapitel 14 durchgehen |
| „Die Datenbank existiert nicht" | Einrichtung nicht abgeschlossen | Einrichtungsprogramm ab Schritt 4 wiederholen |
| „Der Dokumentenordner ist nicht erreichbar" | Freigabe fehlt oder Rechte fehlen | Kapitel 8 prüfen |
| „Es kann nicht geschrieben werden" | NTFS-Rechte fehlen | Registerkarte „Sicherheit" prüfen |
| „Zeitlimit überschritten" | Server aus oder Netz getrennt | Server und Netzwerkkabel prüfen |

Ausführlicher steht das in `FEHLERBEHEBUNG.pdf`.

---

## Kapitel 20 – Datenschutz und Datensparsamkeit

Vehistra ist bewusst schlank gehalten, was Daten angeht:

- Alle Daten bleiben in Ihrem Firmennetz. Es gibt keine Cloudübertragung.
- Es gibt keine Telemetrie, kein Tracking und keine Werbebausteine.
- An LSP Virtual Services werden keinerlei Daten ungefragt übertragen.
- Ein Supportpaket enthält Protokolle, Versionsangaben und die Systemdiagnose –
  aber keine Passwörter, keine SQL-Zugangsdaten und keine Fahrzeug- oder
  Personendaten.
- Wer welche Änderung wann vorgenommen hat, steht im Prüfprotokoll. Normale
  Mitarbeiter können dieses Protokoll nicht verändern.

---

## Kapitel 21 – Wartung im laufenden Betrieb

Diese wenigen Punkte halten den Server dauerhaft gesund:

- **Wöchentlich:** Im Programm unter „Hilfe & Support" die Diagnose ausführen.
- **Monatlich:** Eine Sicherung testweise auf einem Testsystem zurückspielen.
- **Monatlich:** Freien Speicherplatz auf dem Datenlaufwerk prüfen.
- **Bei jedem Update:** Die Versionshinweise lesen.
- **Jährlich:** Benutzerkonten durchsehen und ausgeschiedene Mitarbeiter
  deaktivieren.

---

## Kapitel 22 – Hilfe und Kontakt

Kommen Sie nicht weiter, melden Sie sich beim Support. Hilfreich ist, wenn Sie
Folgendes mitschicken:

1. Den Bericht aus `VehistraServerCheck.exe` („Bericht speichern")
2. Das Supportpaket aus dem Programm unter „Hilfe & Support"
3. Eine kurze Beschreibung, was Sie getan haben und was dann passierte

Vehistra – Open Fleet Management
Vehicle · Driver · Maintenance · Compliance
Developed & maintained by LSP Virtual Services
vehistra.dev · support@vehistra.dev
