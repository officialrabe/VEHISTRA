# Sicherung und Wiederherstellung

Diese Anleitung beschreibt, wie Sie die Fuhrparkdatenbank und die Dokumente
sichern und wie Sie beides im Notfall zurückholen.

> Eine Sicherung, die nie zurückgespielt wurde, ist keine Sicherung. Testen Sie
> die Wiederherstellung einmal im Monat auf einem Testsystem.

---

## 1 – Was gesichert werden muss

| Was | Wo es liegt | Wie oft |
| --- | --- | --- |
| Datenbank `VehistraDB` | im SQL Server auf dem Server | täglich |
| Dokumentenordner | `D:\Fuhrpark\Dokumente` | täglich |
| Serverkonfiguration | `C:\ProgramData\LSP Virtual Services\Vehistra` | bei Änderungen |
| Updateordner | `D:\Fuhrpark\Updates` | bei Änderungen |

Die Protokolle im Ordner `Logs` müssen nicht gesichert werden.

---

## 2 – Empfohlener Aufbewahrungsplan

- **7 tägliche** Sicherungen – für Fehler, die am selben Tag auffallen
- **4 wöchentliche** Sicherungen – für Fehler, die nach Tagen auffallen
- **12 monatliche** Sicherungen – für Nachweise und alte Stände

Bewahren Sie mindestens eine Sicherung außerhalb des Serverraums auf, zum
Beispiel auf einer Wechselfestplatte oder einem NAS an einem anderen Ort.

---

## 3 – Automatische Sicherung einrichten

### 3.1 Skript anlegen

Legen Sie auf dem Server die Datei `D:\Fuhrpark\Backup-Vehistra.ps1` an:

```
$server   = '.\SQLEXPRESS'
$database = 'VehistraDB'
$ordner   = 'D:\Fuhrpark\Backups'
$stempel  = Get-Date -Format 'yyyy-MM-dd-HHmm'
$datei    = Join-Path $ordner "$database-$stempel.bak"

New-Item -ItemType Directory -Path $ordner -Force | Out-Null

$sicherung = @"
BACKUP DATABASE [$database]
TO DISK = N'$datei'
WITH INIT, COMPRESSION, CHECKSUM, STATS = 10,
     NAME = N'Vehistra - taegliche Sicherung';
RESTORE VERIFYONLY FROM DISK = N'$datei' WITH CHECKSUM;
"@

sqlcmd -S $server -E -b -Q $sicherung
if ($LASTEXITCODE -ne 0) { throw "Die Sicherung ist fehlgeschlagen." }

# Dokumente mitsichern
robocopy 'D:\Fuhrpark\Dokumente' "$ordner\Dokumente" /MIR /R:2 /W:5 /NP /LOG+:"$ordner\robocopy.log"

# Aufbewahrung: 7 taegliche Sicherungen behalten
Get-ChildItem $ordner -Filter '*.bak' |
    Sort-Object LastWriteTime -Descending |
    Select-Object -Skip 7 |
    Remove-Item -Force

Write-Host "Sicherung erstellt: $datei"
```

Die Option `CHECKSUM` und der anschließende `RESTORE VERIFYONLY` prüfen die
Sicherung sofort. Damit merken Sie am selben Tag, wenn etwas nicht stimmt.

### 3.2 Aufgabe in der Aufgabenplanung anlegen

1. Windows-Taste drücken, `Aufgabenplanung` tippen und öffnen.
2. Rechts auf „Aufgabe erstellen" klicken.
3. Name: `Vehistra – tägliche Sicherung`.
4. „Unabhängig von der Benutzeranmeldung ausführen" wählen.
5. Haken bei „Mit höchsten Privilegien ausführen" setzen.
6. Registerkarte „Trigger", „Neu", täglich um 22:00 Uhr.
7. Registerkarte „Aktionen", „Neu":
   - Programm: `powershell.exe`
   - Argumente: `-ExecutionPolicy Bypass -File "D:\Fuhrpark\Backup-Vehistra.ps1"`
8. Mit „OK" bestätigen und das Windows-Passwort eingeben.
9. Rechtsklick auf die Aufgabe und „Ausführen", um sie sofort zu testen.

### 3.3 Prüfen, dass es läuft

Sehen Sie am nächsten Tag im Ordner `D:\Fuhrpark\Backups` nach. Dort muss eine
Datei mit dem Datum des Vortags liegen. Fehlt sie, prüfen Sie in der
Aufgabenplanung das letzte Ausführungsergebnis.

---

## 4 – Sicherung von Hand erstellen

Vor größeren Änderungen lohnt sich eine Sicherung von Hand.

**Im Programm:** Melden Sie sich als Administrator an, öffnen Sie
„Einstellungen" und dort „Datenbank". Klicken Sie auf „Sicherung jetzt
erstellen".

**Auf dem Server:** Führen Sie das Skript aus Abschnitt 3.1 von Hand aus, oder
geben Sie in einer Eingabeaufforderung ein:

```
sqlcmd -S .\SQLEXPRESS -E -Q "BACKUP DATABASE [VehistraDB] TO DISK = N'D:\Fuhrpark\Backups\vor-aenderung.bak' WITH INIT, CHECKSUM"
```

---

## 5 – Datenbank wiederherstellen

> Achtung: Beim Wiederherstellen wird der aktuelle Stand der Datenbank
> überschrieben. Alles, was nach der Sicherung erfasst wurde, geht verloren.
> Erstellen Sie deshalb vorher eine Sicherung des aktuellen Standes.

### Schritt für Schritt

1. Sagen Sie allen Mitarbeitern Bescheid und lassen Sie Vehistra schließen.
2. Sichern Sie den aktuellen Stand (Abschnitt 4).
3. Öffnen Sie auf dem Server eine Eingabeaufforderung als Administrator.
4. Führen Sie aus:

```
sqlcmd -S .\SQLEXPRESS -E -Q "ALTER DATABASE [VehistraDB] SET SINGLE_USER WITH ROLLBACK IMMEDIATE"

sqlcmd -S .\SQLEXPRESS -E -Q "RESTORE DATABASE [VehistraDB] FROM DISK = N'D:\Fuhrpark\Backups\VehistraDB-2026-03-14-2200.bak' WITH REPLACE, RECOVERY"

sqlcmd -S .\SQLEXPRESS -E -Q "ALTER DATABASE [VehistraDB] SET MULTI_USER"
```

5. Spielen Sie die Dokumente zurück:

```
robocopy "D:\Fuhrpark\Backups\Dokumente" "D:\Fuhrpark\Dokumente" /MIR /R:2 /W:5
```

6. Starten Sie `VehistraServerCheck.exe` und prüfen Sie, dass alles „IN ORDNUNG"
   meldet.
7. Melden Sie sich im Programm an und prüfen Sie stichprobenartig ein Fahrzeug.

---

## 6 – Eine Sicherung testen, ohne den Betrieb zu stören

So prüfen Sie eine Sicherung, ohne die echte Datenbank anzufassen: Stellen Sie
sie unter einem anderen Namen wieder her.

```
sqlcmd -S .\SQLEXPRESS -E -Q "RESTORE DATABASE [VehistraDB_Test] FROM DISK = N'D:\Fuhrpark\Backups\VehistraDB-2026-03-14-2200.bak' WITH MOVE 'VehistraDB' TO 'D:\Fuhrpark\Test\VehistraDB_Test.mdf', MOVE 'VehistraDB_log' TO 'D:\Fuhrpark\Test\VehistraDB_Test.ldf', RECOVERY"
```

Prüfen Sie danach im SQL Server Management Studio, ob die Tabellen Daten
enthalten. Löschen Sie die Testdatenbank anschließend wieder.

---

## 7 – Sicherungen des Updaters

Der Updater legt zusätzlich eigene Sicherungen an:

| Was | Wo | Wann |
| --- | --- | --- |
| Programmdateien als ZIP | `C:\ProgramData\LSP Virtual Services\Vehistra\UpdateBackup` | vor jedem Programmupdate |
| Datenbanksicherung | im konfigurierten Backupordner | vor jeder Datenbankänderung |

Von den ZIP-Sicherungen werden die letzten fünf aufbewahrt. Diese Sicherungen
ersetzen die tägliche Sicherung nicht.

---

## 8 – Notfallplan

Bei einem Totalausfall des Servers:

1. Neuen Server aufsetzen und SQL Server Express installieren
   (`SERVER-EINRICHTUNG-EINFACH.pdf`, Kapitel 3 bis 6).
2. Die Ordnerstruktur anlegen und freigeben (Kapitel 7 und 8).
3. Die letzte Sicherung zurückspielen (Abschnitt 5 dieser Anleitung).
4. `VehistraServerSetup.exe` ausführen – die vorhandene Datenbank wird erkannt
   und weiterverwendet.
5. Die Client-Konfiguration neu exportieren und verteilen, falls der Servername
   sich geändert hat.

Bewahren Sie diesen Plan ausgedruckt zusammen mit den Zugangsdaten an einem
sicheren Ort auf. Im Notfall hilft eine Datei auf dem ausgefallenen Server
niemandem.

---

## 9 – Checkliste

| Wann | Was |
| --- | --- |
| Täglich | Automatische Sicherung läuft (Datei im Backupordner prüfen) |
| Wöchentlich | Diagnose im Programm ausführen |
| Monatlich | Eine Sicherung testweise zurückspielen (Abschnitt 6) |
| Monatlich | Freien Speicherplatz prüfen |
| Vierteljährlich | Eine Sicherung außer Haus auslagern |
| Jährlich | Den Notfallplan durchspielen |

---

Vehistra – Open Fleet Management
Vehicle · Driver · Maintenance · Compliance
Developed & maintained by LSP Virtual Services
vehistra.dev · support@vehistra.dev
