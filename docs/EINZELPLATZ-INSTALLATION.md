# Solo-Platz einrichten

Diese Anleitung ist für den Fall, dass Vehistra auf **einem einzigen Computer**
laufen soll – ohne Server, ohne Netzwerkfreigaben, **ohne zusätzliche
Datenbankinstallation**.

Sollen mehrere Arbeitsplätze gleichzeitig mit demselben Fuhrpark arbeiten, ist
`SERVER-EINRICHTUNG-EINFACH.pdf` die richtige Anleitung.

---

## Was der Unterschied ist

| | Solo-Platz | Netzwerk |
| --- | --- | --- |
| Datenbank | eine Datei auf diesem Computer | SQL Server auf dem Firmenserver |
| Zusätzlich zu installieren | **nichts** | Microsoft SQL Server Express |
| Gleichzeitige Nutzer | einer | beliebig viele |
| Netzwerkfreigaben, Firewall, TCP/IP | nicht nötig | nötig |
| Zeitaufwand | etwa 10 Minuten | etwa 60 Minuten |

Sie können später auf den Netzwerkbetrieb wechseln, ohne Daten zu verlieren –
siehe Kapitel 7.

---

## Kapitel 1 – Was Sie brauchen

- Einen Windows-PC (64 Bit), Windows 10 oder neuer
- Administratorrechte für die Installation
- Etwa 10 Minuten Zeit
- Die Datei `Vehistra-Setup.exe`
- Etwa 2 GB freien Speicherplatz

> Wichtig: Beim Solo-Platz liegen Programm, Datenbank und Sicherung auf
> demselben Gerät. Geht die Festplatte kaputt, ist ohne eine Sicherung außer
> Haus alles verloren. Lesen Sie dazu Kapitel 6.

---

## Kapitel 2 – Vehistra installieren

1. Doppelklick auf `Vehistra-Setup.exe`.
2. Bestätigen Sie die Nachfrage von Windows mit „Ja".
3. Der Assistent fragt nach der Installationsart. **Wählen Sie
   „Solo-Platz-Installation – alles auf diesem Computer, ohne
   Datenbankserver".**
4. „Weiter", dann „Installieren".
5. Lassen Sie am Ende den Haken bei **„Jetzt die örtliche Datenbank
   einrichten"** gesetzt und klicken Sie auf „Fertigstellen".

Durch die Wahl „Solo-Platz" wird der Einrichtungsassistent mitinstalliert, der
im nächsten Kapitel die Datenbank anlegt.

Meldet der Assistent, dass die .NET Desktop Runtime 10 fehlt: unter
`https://dotnet.microsoft.com/download/dotnet/10.0` im Bereich „Desktop
Runtime" die Variante „Windows x64" herunterladen, installieren und das Setup
erneut starten.

---

## Kapitel 3 – Die Datenbank anlegen

Der Einrichtungsassistent öffnet sich mit dem Untertitel „Solo-Platz einrichten
– alles auf diesem Computer". Alle Felder sind bereits sinnvoll vorbelegt.

| Schritt | Was zu tun ist |
| --- | --- |
| 1 Systemprüfung | Auf „System prüfen" klicken. |
| 2 Datenbankserver | Auf „Prüfen" klicken. Es erscheint „Beim Solo-Platz wird kein Datenbankserver benötigt", dann „Weiter". |
| 3 Datenbankdatei | Der Ort ist vorbelegt. Auf „Verbindung testen" klicken. |
| 4 Datenbank | Auf „Datenbank anlegen" klicken. Die Datei entsteht. |
| 5 Datenbankstruktur | Auf „Struktur einrichten" klicken. Dauert einige Sekunden. |
| 6 Dokumentenordner | Vorbelegt mit `C:\Vehistra\Dokumente`. „Ordner anlegen". |
| 7 Backupordner | Vorbelegt mit `C:\Vehistra\Backups`. Wenn möglich auf ein anderes Laufwerk ändern. |
| 8 Updateordner | Vorbelegt. „Ordner anlegen". |
| 9 Netzwerkfreigaben | Wird beim Solo-Platz nicht gebraucht. „Ordner prüfen", dann „Weiter". |
| 10 Erster Administrator | Benutzername und Passwort festlegen. **Passwort sicher notieren.** |
| 11 Abschlussprüfung | „Abschluss prüfen". Alle Zeilen sollten „IN ORDNUNG" zeigen. |
| 12 Abschluss | Auf „Einrichtung abschließen" klicken, dann den Assistenten schließen. |

Die Datenbank liegt anschließend unter
`C:\ProgramData\LSP Virtual Services\Vehistra\Vehistra.db`.

---

## Kapitel 4 – Zum ersten Mal anmelden

Doppelklick auf „Vehistra" auf dem Desktop und mit dem Konto aus Kapitel 3
Schritt 10 anmelden. Mehr ist nicht zu tun – die Verbindung zur Datenbankdatei
hat der Einrichtungsassistent bereits hinterlegt.

---

## Kapitel 5 – Die Einrichtung prüfen

Starten Sie im Startmenü „Serverprüfung". Das Programm erkennt den Solo-Platz
von selbst und meldet bei „Netzwerk": *„Beim Solo-Platz wird kein Netzwerk
benötigt."*

Alle Zeilen sollten „IN ORDNUNG" tragen.

---

## Kapitel 6 – Sicherung: hier besonders wichtig

Im Netzwerkbetrieb liegt die Sicherung auf einem Server, der üblicherweise
mitgesichert wird. **Beim Solo-Platz gibt es diesen zweiten Ort nicht.**
Festplatte defekt heißt dann: Datenbank *und* Sicherung weg.

Vehistra sichert die Datenbank in eine eigenständige Kopie und prüft sie
anschließend, indem es sie öffnet. Das geschieht im laufenden Betrieb, Sie
müssen das Programm nicht schließen.

Richten Sie beides ein:

1. Die Sicherung im Programm unter „ADMINISTRATION" → „Backups"
   (das Backupverzeichnis hinterlegen Sie unter „Einstellungen"). Eine
   Sicherung ohne festes Verzeichnis lässt sich nicht erstellen.
   Für einen täglichen Lauf ohne Zutun: `BACKUP-UND-WIEDERHERSTELLUNG.pdf`,
   Abschnitt 9.2.
2. **Eine Kopie außer Haus** – auf eine Wechselfestplatte, die nicht am PC
   bleibt, oder in einen Cloudspeicher Ihrer Wahl. Mindestens wöchentlich.

Prüfen Sie einmal im Monat, ob sich eine Sicherung zurückspielen lässt.

> Kopieren Sie die Datei `Vehistra.db` **niemals** von Hand, während Vehistra
> läuft. Die jüngsten Änderungen stehen dann noch in der Begleitdatei
> `Vehistra.db-wal` und würden fehlen. Verwenden Sie immer die Sicherung aus
> dem Programm – sie erzeugt eine in sich stimmige Kopie.

---

## Kapitel 7 – Später auf Netzwerkbetrieb wechseln

Wächst der Betrieb, lässt sich der Solo-Platz ausbauen:

1. Server aufsetzen und SQL Server Express installieren
   (`SERVER-EINRICHTUNG-EINFACH.pdf`, Kapitel 3 bis 6).
2. Auf dem Server mit `VehistraServerSetup.exe` eine leere Datenbank anlegen.
3. Die Daten aus dem Solo-Platz exportieren (Einstellungen → „Export") und auf
   dem Server importieren.
4. Dokumentenordner auf den Server kopieren und freigeben.
5. Auf dem bisherigen Solo-PC unter „Servereinstellungen" auf Netzwerkbetrieb
   umstellen und den Servernamen eintragen.
6. Weitere Arbeitsplätze nach `NEUEN-PC-IN-5-MINUTEN.pdf` einrichten.

---

## Häufige Fragen

**Muss der PC immer laufen?**
Nein. Sie sind der einzige Nutzer.

**Wo liegen meine Daten?**
Die Datenbank unter `C:\ProgramData\LSP Virtual Services\Vehistra\Vehistra.db`,
die Fahrzeugdokumente im Dokumentenordner aus Kapitel 3 Schritt 6.

**Warum keine Datenbank auf einem Netzlaufwerk?**
Die Dateisperrung über Netzwerkfreigaben ist nicht verlässlich; bei zwei
gleichzeitigen Zugriffen droht eine beschädigte Datei. Für mehrere
Arbeitsplätze ist der Netzwerkbetrieb mit SQL Server vorgesehen.

**Kann ich einen zweiten Arbeitsplatz anschließen?**
Nicht an einen Solo-Platz. Dafür ist der Wechsel aus Kapitel 7 nötig.

**Bekomme ich Updates?**
Ja. `Vehistra-Update.exe` ausführen oder den Updateordner verwenden. Die
Solo-Wahl bleibt dabei erhalten, und die Datenbank wird niemals gelöscht.

---

Vehistra – Open Fleet Management
Vehicle · Driver · Maintenance · Compliance
Developed & maintained by LSP Virtual Services
vehistra.dev · support@vehistra.dev
