# Änderungen

Alle bemerkenswerten Änderungen an Vehistra. Die Versionsnummern folgen
`HAUPT.NEBEN.KORREKTUR` – siehe `docs/UPDATE-ANLEITUNG.md`, Abschnitt 8.

Der Release-Workflow liest die Abschnitte dieser Datei und übernimmt sie in die
Versionshinweise der Veröffentlichung.

## 0.6.0

Neu
- Der Installationsassistent fragt jetzt nach der Betriebsart:
  „Netzwerk-Installation" für einen Arbeitsplatz im Firmennetz oder
  „Solo-Platz-Installation", bei der alles auf einem Computer liegt.
- Beim Solo-Platz wird der Einrichtungsassistent mitinstalliert, im Startmenü
  verlinkt und direkt nach der Installation angeboten.
- Fehlt Microsoft SQL Server Express, erklärt der Installer das vor der
  Installation statt den Anwender in eine Sackgasse laufen zu lassen.
- Der Einrichtungsassistent erkennt den Solo-Platz: Server und Ordner sind
  vorbelegt, und er fragt nicht mehr nach Netzwerkfreigaben, die es nicht gibt.
- Neue Anleitung EINZELPLATZ-INSTALLATION mit acht Kapiteln, inklusive Wechsel
  auf Netzwerkbetrieb ohne Datenverlust.

Verbessert
- Das Updatepaket übernimmt die ursprünglich gewählte Betriebsart. Ein
  Solo-Platz verliert seinen Einrichtungsassistenten nicht, und ein
  Arbeitsplatz im Netz bekommt ihn nicht nachträglich untergeschoben.
- Die Versionshinweise einer Veröffentlichung nennen jetzt, was sich geändert
  hat, statt nur den Lieferumfang aufzuzählen.

## 0.5.0

Erste ausgelieferte Vorabversion.

Neu
- Fahrzeuge, Fahrer, Hauptuntersuchungen, Kilometerstände, Wartung, Schäden,
  Unfälle, Werkstattaufträge, Kennzeichen, Reservierungen, Zulassung,
  Ausmusterung, Versicherungen, Schlüssel und Dokumente.
- Fahrzeugdetailseite mit dreizehn Registerkarten und durchgehender Zeitleiste.
  Historien werden ergänzt, niemals überschrieben.
- Werkstattbericht (ein Blatt) und Unfallbericht (zwei Blätter mit
  Skizzenraster), jeweils blanko und vorausgefüllt, als echte Vektor-PDFs.
- Servereinrichtung in zwölf Schritten und ein Diagnoseprogramm, das
  Klartextmeldungen statt SQL-Fehlernummern ausgibt.
- Updates über eine zentrale Ablage mit Prüfsummen, Pflichtupdates und
  Migrationssperre. Vor jeder Datenbankänderung wird gesichert; schlägt die
  Sicherung fehl, bricht das Update ab.
- Fünf Anleitungen als Markdown und PDF.

Bekannte Einschränkungen
- Die Installationspakete sind nicht signiert. Windows zeigt beim Start eine
  SmartScreen-Warnung.
