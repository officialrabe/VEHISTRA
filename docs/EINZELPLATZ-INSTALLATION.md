# Solo-Platz einrichten

Diese Anleitung ist für den Fall, dass Vehistra auf **einem einzigen Computer**
laufen soll – ohne Server, ohne Netzwerkfreigaben, ohne zweiten Rechner.

Wenn mehrere Arbeitsplätze gleichzeitig mit demselben Fuhrpark arbeiten sollen,
ist stattdessen `SERVER-EINRICHTUNG-EINFACH.pdf` die richtige Anleitung.

---

## Was der Unterschied ist

| | Solo-Platz | Netzwerk |
| --- | --- | --- |
| Datenbank | auf diesem Computer | auf dem Firmenserver |
| Gleichzeitige Nutzer | einer | beliebig viele |
| Netzwerkfreigaben | nicht nötig | nötig |
| Firewall, TCP/IP, SQL-Browser | nicht nötig | nötig |
| Zeitaufwand | etwa 30 Minuten | etwa 60 Minuten |

Sie können später jederzeit auf den Netzwerkbetrieb wechseln: Die Datenbank
lässt sich sichern und auf einem Server zurückspielen. Ihre Daten gehen dabei
nicht verloren.

---

## Kapitel 1 – Was Sie brauchen

- Einen Windows-PC (64 Bit), Windows 10 oder neuer
- Administratorrechte auf diesem PC
- Etwa 30 Minuten Zeit
- Die Datei `Vehistra-Setup.exe`
- Etwa 10 GB freien Speicherplatz

Der PC muss nicht leistungsstark sein. Ein normaler Büro-PC mit 8 GB
Arbeitsspeicher und einer SSD genügt.

> Wichtig: Bei einem Solo-Platz liegen Programm, Datenbank und Sicherung auf
> demselben Gerät. Geht die Festplatte kaputt, ist ohne eine Sicherung außer
> Haus alles verloren. Lesen Sie dazu Kapitel 7.

---

## Kapitel 2 – Microsoft SQL Server Express installieren

Vehistra speichert seine Daten in einer richtigen Datenbank, nicht in einer
Datei. Dafür wird „Microsoft SQL Server Express" benötigt – ein kostenloses
Programm von Microsoft. Vehistra installiert es bewusst nicht im Hintergrund.

1. Öffnen Sie `https://www.microsoft.com/de-de/sql-server/sql-server-downloads`
2. Klicken Sie im Bereich „Express" auf „Jetzt herunterladen".
3. Starten Sie die heruntergeladene Datei.
4. Wählen Sie den Installationstyp „Basic" bzw. „Standard".
5. Bestätigen Sie die Lizenzbedingungen.
6. **Behalten Sie den vorgeschlagenen Instanznamen `SQLEXPRESS` bei.**
7. Warten Sie, bis die Installation abgeschlossen ist.

Das war der aufwendigste Teil. Alles Weitere geht schnell.

> Sie müssen **nicht** TCP/IP aktivieren, **nicht** den SQL-Server-Browser
> starten und **nichts** in der Firewall freigeben. Das braucht nur der
> Netzwerkbetrieb, weil dort andere Computer zugreifen müssen.

---

## Kapitel 3 – Vehistra installieren

1. Doppelklick auf `Vehistra-Setup.exe`.
2. Bestätigen Sie die Nachfrage von Windows mit „Ja".
3. Der Assistent fragt nach der Installationsart. **Wählen Sie hier
   „Solo-Platz-Installation – alles auf diesem Computer".**
4. Klicken Sie auf „Weiter" und danach auf „Installieren".
5. Lassen Sie am Ende den Haken bei **„Jetzt die örtliche Datenbank
   einrichten"** gesetzt und klicken Sie auf „Fertigstellen".

Durch die Wahl „Solo-Platz" wird zusätzlich der Einrichtungsassistent
mitinstalliert, der im nächsten Kapitel die Datenbank anlegt. Bei der
Netzwerk-Installation fehlt er, weil dort der Server getrennt eingerichtet wird.

Meldet der Assistent, dass SQL Server Express fehlt: Kapitel 2 nachholen und
danach im Startmenü „Datenbank einrichten" aufrufen.

---

## Kapitel 4 – Die Datenbank anlegen

Der Einrichtungsassistent öffnet sich mit dem Untertitel „Solo-Platz einrichten
– alles auf diesem Computer". Er führt durch zwölf Schritte und hat alle Felder
bereits sinnvoll vorbelegt.

| Schritt | Was zu tun ist |
| --- | --- |
| 1 Systemprüfung | Auf „System prüfen" klicken. Es müssen Administratorrechte gemeldet werden. |
| 2 SQL Server erkennen | Auf „SQL Server suchen" klicken. Ihre Instanz erscheint in der Liste. |
| 3 Verbindung testen | Der Server steht bereits auf `.\SQLEXPRESS`. Der Punkt steht für „dieser Computer". Windows-Authentifizierung beibehalten. |
| 4 Datenbank | Auf „Datenbank anlegen" klicken. |
| 5 Datenbankstruktur | Auf „Struktur einrichten" klicken. Dauert einige Sekunden. |
| 6 Dokumentenordner | Vorbelegt mit `C:\Vehistra\Dokumente`. Auf „Ordner anlegen" klicken. |
| 7 Backupordner | Vorbelegt mit `C:\Vehistra\Backups`. Am besten auf ein anderes Laufwerk ändern, falls vorhanden. |
| 8 Updateordner | Vorbelegt mit `C:\Vehistra\Updates`. Auf „Ordner anlegen" klicken. |
| 9 Netzwerkfreigaben | Wird beim Solo-Platz nicht gebraucht. Auf „Freigaben prüfen" und „Weiter" klicken. |
| 10 Erster Administrator | Benutzername und Passwort festlegen. **Passwort sicher notieren.** |
| 11 Abschlussprüfung | Auf „Abschluss prüfen" klicken. Alle Zeilen sollten „IN ORDNUNG" zeigen. |
| 12 Client-Konfiguration | Beim Solo-Platz nicht nötig – Sie können den Assistenten schließen. |

---

## Kapitel 5 – Zum ersten Mal anmelden

1. Doppelklick auf „Vehistra" auf dem Desktop.
2. Beim ersten Start erscheint das Fenster „Serververbindung einrichten".
3. Tragen Sie ein:
   - Server: `.\SQLEXPRESS`
   - Datenbank: `VehistraDB`
   - Anmeldung: Windows-Authentifizierung
   - Dokumentenordner: `C:\Vehistra\Dokumente`
   - Updateordner: leer lassen
4. „Verbindung testen", dann „Speichern".
5. Melden Sie sich mit dem Konto aus Kapitel 4 Schritt 10 an.

Fertig.

---

## Kapitel 6 – Die Einrichtung prüfen

Starten Sie im Startmenü „Serverprüfung". Das Programm prüft alles Wichtige und
erkennt den Solo-Platz von selbst – bei „Serverstandort" meldet es:
*„Die Datenbank liegt auf diesem Computer. Eine Netzwerkprüfung ist nicht
nötig."*

Alle Zeilen sollten „IN ORDNUNG" tragen.

---

## Kapitel 7 – Sicherung: hier besonders wichtig

Im Netzwerkbetrieb liegt die Sicherung auf einem Server, der üblicherweise
mitgesichert wird. **Beim Solo-Platz gibt es diesen zweiten Ort nicht.**
Festplatte defekt heißt dann: Datenbank *und* Sicherung weg.

Richten Sie deshalb beides ein:

1. Die automatische tägliche Sicherung wie in
   `BACKUP-UND-WIEDERHERSTELLUNG.pdf`, Kapitel 3 beschrieben. Ersetzen Sie dort
   `D:\Fuhrpark\Backups` durch Ihren Backupordner.
2. **Eine regelmäßige Kopie außer Haus** – auf eine Wechselfestplatte, die nicht
   am PC bleibt, oder in einen Cloudspeicher Ihrer Wahl. Mindestens wöchentlich.

Prüfen Sie einmal im Monat, ob sich eine Sicherung zurückspielen lässt
(`BACKUP-UND-WIEDERHERSTELLUNG.pdf`, Kapitel 6). Eine ungeprüfte Sicherung ist
keine Sicherung.

---

## Kapitel 8 – Später auf Netzwerkbetrieb wechseln

Wächst der Betrieb, lässt sich der Solo-Platz zum Netzwerkbetrieb ausbauen,
ohne Daten zu verlieren:

1. Server aufsetzen und SQL Server Express installieren
   (`SERVER-EINRICHTUNG-EINFACH.pdf`, Kapitel 3 bis 6).
2. Auf dem Solo-PC eine Sicherung der Datenbank erstellen.
3. Die Sicherung auf dem Server zurückspielen
   (`BACKUP-UND-WIEDERHERSTELLUNG.pdf`, Kapitel 5).
4. Dokumentenordner auf den Server kopieren und freigeben
   (`SERVER-EINRICHTUNG-EINFACH.pdf`, Kapitel 7 und 8).
5. Auf dem bisherigen Solo-PC unter „Servereinstellungen" den neuen Servernamen
   eintragen.
6. Weitere Arbeitsplätze nach `NEUEN-PC-IN-5-MINUTEN.pdf` einrichten.

---

## Häufige Fragen

**Muss der PC immer laufen?**
Nein. Sie sind der einzige Nutzer – wenn er aus ist, arbeitet niemand damit.

**Kann ich später einen zweiten Arbeitsplatz anschließen?**
Nicht direkt an einen Solo-Platz. Dafür braucht es den Wechsel aus Kapitel 8,
weil am Solo-Platz Firewall und Netzwerkprotokolle bewusst zugelassen bleiben,
wie Windows sie standardmäßig setzt.

**Warum keine einfache Datei statt einer Datenbank?**
Eine Datenbank verhindert, dass bei einem Absturz mitten im Speichern
widersprüchliche Daten entstehen, und führt lückenlos Buch darüber, wer was
wann geändert hat. Bei Fahrzeugdaten mit Prüfterminen und Unfallakten ist das
kein Luxus.

**Bekomme ich Updates?**
Ja. Legen Sie neue Versionen in Ihren Updateordner, oder installieren Sie
`Vehistra-Update.exe` von Hand. Die Solo-Wahl bleibt dabei erhalten.

---

Vehistra – Open Fleet Management
Vehicle · Driver · Maintenance · Compliance
Developed & maintained by LSP Virtual Services
vehistra.dev · support@vehistra.dev
