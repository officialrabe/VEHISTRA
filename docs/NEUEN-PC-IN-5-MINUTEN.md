# Neuen PC in 5 Minuten einrichten

So richten Sie einen weiteren Arbeitsplatz für Vehistra ein. Der Server muss
bereits fertig eingerichtet sein – siehe `SERVER-EINRICHTUNG-EINFACH.pdf`.

---

## Was Sie brauchen

- Die Datei `Vehistra-Setup.exe`
- Administratorrechte auf dem Arbeitsplatz-PC
- Den Pfad zur Firmenkonfiguration, zum Beispiel
  `\\FUHRPARK-SRV01\Fuhrpark\Vehistra-Firmenkonfiguration.fmcfg`
- Benutzername und Passwort des Mitarbeiters

---

## Minute 1 – Programm installieren

1. Kopieren Sie `Vehistra-Setup.exe` auf den PC oder öffnen Sie die Datei direkt
   aus der Netzwerkfreigabe.
2. Doppelklick auf die Datei.
3. Bestätigen Sie die Nachfrage von Windows mit „Ja".
4. Klicken Sie im Assistenten auf „Weiter" und danach auf „Installieren".

Meldet der Assistent, dass die .NET Desktop Runtime 10 fehlt: Laden Sie sie
unter `https://dotnet.microsoft.com/download/dotnet/10.0` im Bereich
„Desktop Runtime" für „Windows x64" herunter, installieren Sie sie und starten
Sie das Setup erneut.

---

## Minute 2 – Programm starten

Doppelklick auf das Symbol „Vehistra" auf dem Desktop.

Beim allerersten Start erscheint das Fenster „Serververbindung einrichten".

---

## Minute 3 – Firmenkonfiguration laden

Das ist der schnellste Weg – Sie müssen nichts abtippen.

1. Klicken Sie auf „Firmenkonfiguration laden".
2. Wählen Sie die Datei `Vehistra-Firmenkonfiguration.fmcfg` aus der
   Netzwerkfreigabe.
3. Servername, Datenbankname und die Ordnerpfade werden automatisch eingetragen.

> Die Datei enthält bewusst keine Passwörter. Wird SQL-Authentifizierung
> verwendet, tragen Sie Benutzername und Passwort einmalig von Hand ein.

### Alternative: von Hand eintragen

| Feld | Beispiel |
| --- | --- |
| Server | `FUHRPARK-SRV01\SQLEXPRESS` |
| Datenbank | `VehistraDB` |
| Anmeldung | Windows-Authentifizierung |
| Dokumentenordner | `\\FUHRPARK-SRV01\Fuhrpark\Dokumente` |
| Updateordner | `\\FUHRPARK-SRV01\Fuhrpark\Updates` |

---

## Minute 4 – Verbindung testen und speichern

1. Klicken Sie auf „Verbindung testen".
2. Es sollte „Die Verbindung wurde erfolgreich hergestellt" erscheinen.
3. Klicken Sie auf „Speichern".

Erscheint stattdessen eine Fehlermeldung, lesen Sie die angezeigten Hinweise –
sie nennen den konkreten nächsten Schritt. Hilft das nicht weiter, steht in
`FEHLERBEHEBUNG.pdf` mehr dazu.

---

## Minute 5 – Anmelden

1. Es erscheint das Anmeldefenster mit dem Vehistra-Schriftzug.
2. Geben Sie Benutzername und Passwort ein.
3. Klicken Sie auf „Anmelden".

Fertig. Der Arbeitsplatz ist eingerichtet.

---

## Was der Mitarbeiter jetzt sieht

Nach der Anmeldung öffnet sich die Übersicht mit:

- der Anzahl aller Fahrzeuge und wie viele davon einsatzbereit sind
- allen anstehenden Hauptuntersuchungen
- offenen Schäden und Werkstattaufträgen
- dem Bereich „AUFMERKSAMKEIT ERFORDERLICH" mit allem, was jetzt zu tun ist

Welche Bereiche sichtbar sind, hängt von der Rolle des Benutzers ab. Ein
Mitarbeiter der Werkstatt sieht andere Bereiche als die Geschäftsleitung.

---

## Häufige Fragen

**Muss auf dem PC eine Datenbank installiert werden?**
Nein. Die Datenbank liegt ausschließlich auf dem Server. Auf dem Arbeitsplatz
wird nichts zwischengespeichert.

**Was passiert, wenn der Server nicht erreichbar ist?**
Vehistra zeigt den Hinweis „SERVER NICHT ERREICHBAR" mit den Schaltflächen
„Erneut versuchen", „Diagnose", „Servereinstellungen" und „Programm beenden".
Das Programm stürzt nicht ab. Es gibt bewusst keine lokale Ersatzdatenbank,
damit keine widersprüchlichen Datenstände entstehen.

**Wie bekommt der PC neue Versionen?**
Automatisch. Vehistra prüft beim Start den Updateordner auf dem Server und
meldet neue Versionen. Details in `UPDATE-ANLEITUNG.pdf`.

**Wo liegen die Einstellungen auf dem PC?**
Unter `C:\ProgramData\LSP Virtual Services\Vehistra`. Dort liegen auch die
Protokolle. Im Programmordner unter „Programme" wird nichts verändert.

**Kann ich die Konfiguration auf weitere PCs kopieren?**
Ja – dafür ist die `.fmcfg`-Datei gedacht. Ein gespeichertes SQL-Passwort wird
dabei bewusst nicht mitkopiert, weil es an den jeweiligen Computer gebunden ist.

---

Vehistra – Open Fleet Management
Vehicle · Driver · Maintenance · Compliance
Developed & maintained by LSP Virtual Services
vehistra.dev · support@vehistra.dev
