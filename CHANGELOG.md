# Änderungen

Alle bemerkenswerten Änderungen an Vehistra. Die Versionsnummern folgen
`HAUPT.NEBEN.KORREKTUR` – siehe `docs/UPDATE-ANLEITUNG.md`, Abschnitt 8.

Der Release-Workflow liest die Abschnitte dieser Datei und übernimmt sie in die
Versionshinweise der Veröffentlichung.

## 1.1.0

Behoben
- **Listen zeigten Änderungen erst nach „Aktualisieren".** Sehr viele Befehle
  erledigen ihre Arbeit und laden danach die Liste neu – und dieses Neuladen
  lief in dieselbe Sperre, die Doppelklicks abfängt. Die innere Arbeit wurde
  dabei stillschweigend verworfen. Betroffen waren 19 Stellen in 15 Ansichten,
  unter anderem: die Fahrzeugliste beim Öffnen, Fahrer aktivieren und
  deaktivieren, Schaden schließen, Unfall schließen, Benutzer deaktivieren,
  Passwort zurücksetzen, Dokument archivieren, Kennzeichen freigeben,
  Wartungsregel löschen, Rolle löschen, Werkstatt anlegen, Backup erstellen,
  Werkstattvorgang umstellen, Ausmusterung zurücknehmen und die Updateprüfung.
  Der Schutz gegen Doppelklicks bleibt bestehen.

Neu
- **Eigene Stammdaten** lassen sich unter „Einstellungen" pflegen – bisher
  waren die mitgelieferten Einträge die einzige Auswahl:
  - **Fahrzeugkategorien** (Einsatzbereiche): anlegen, umbenennen, stilllegen,
    löschen.
  - **Schadenskategorien**: anlegen, umbenennen, stilllegen, löschen.
  - **Fahrzeugstatus**: anlegen, umbenennen, stilllegen, löschen, und je Status
    festlegen, ob Fahrzeuge darin als „einsatzbereit" oder „verfügbar" zählen –
    davon leben die Kennzahlen im Dashboard.

  Überall gilt dasselbe Vorgehen:
  - Mitgelieferte Einträge lassen sich stilllegen, aber nicht löschen.
  - Gelöscht wird nur, was nirgends verwendet wird; sonst nennt die Meldung,
    woran es hängt. Historien und erfasste Vorgänge werden nie verändert.
  - Ein stillgelegter Eintrag verschwindet aus der Auswahl, bleibt aber an
    bereits zugeordneten Datensätzen erhalten und lässt sich wieder einschalten.
  - Doppelte Namen werden abgewiesen, auch in anderer Schreibweise.

  Zwei Sonderfälle, die das Programm absichert:
  - Mitgelieferte Fahrzeugstatus dürfen umbenannt werden. Abläufe wie
    Ausmusterung, Werkstatt und Import hängen an ihrer internen Zuordnung, nicht
    am Namen.
  - Die mitgelieferte Schadenskategorie „Unfall" dagegen lässt sich **nicht**
    umbenennen: die Schadensmeldung aus einem Unfall findet sie über ihren
    Namen. Stilllegen ist möglich, die Zuordnung funktioniert weiterhin.
  - Der letzte aktive Fahrzeugstatus lässt sich nicht stilllegen – ohne einen
    aktiven Status könnte kein Fahrzeug mehr angelegt werden.

Geprüft
- 23 neue Tests für die Stammdatenverwaltung (Anlegen, doppelte Namen,
  Umbenennen, Stilllegen, Löschen, Rechteprüfung, die beiden Sonderfälle und
  der Nachweis, dass Ausmusterung und Unfallschaden weiterhin ihren Status
  bzw. ihre Kategorie finden).
- Neues Testprojekt für die Ansichtsmodelle des Arbeitsplatzprogramms, das den
  Verschachtelungsfehler festnagelt. Es läuft nur unter Windows, also in der CI.

## 1.0.0

Erste vollständige Fassung. Sie wird als Beta veröffentlicht: vollständig und
getestet, aber noch nicht im Dauerbetrieb erprobt.

Neu
- Der Solo-Platz braucht **keinen Datenbankserver** mehr. Die Datenbank ist
  eine einzelne Datei unter
  `C:\ProgramData\LSP Virtual Services\Vehistra\Vehistra.db`. Microsoft SQL
  Server Express muss dafür nicht mehr installiert werden.
- Der Installationsassistent verlangt bei der Solo-Platz-Installation nichts
  mehr nachzuinstallieren. Die Einrichtung dauert rund zehn Minuten.
- Sicherungen des Solo-Platzes entstehen als vollständige Dateikopie und werden
  anschließend geöffnet und auf Beschädigung geprüft. Das funktioniert auch,
  während das Programm läuft.
- Der Einrichtungsassistent spricht beim Solo-Platz die passende Sprache: er
  zeigt die Datenbankdatei statt Servername und SQL-Anmeldung, sucht keine
  SQL-Instanzen mehr und verlangt keine Konfigurationsdatei für weitere
  Arbeitsplätze.
- Ist die Datenbankdatei verschwunden oder leer, meldet Vehistra das beim Start
  mit den passenden Prüfschritten – statt stillschweigend eine leere Datenbank
  anzulegen.
- `VehistraServerCheck.exe` nennt in der ersten Zeile die Betriebsart und
  überspringt beim Solo-Platz alle Prüfungen, die es dort nicht gibt
  (Netzwerkfreigaben, SQL-Dienst, Firewall).
- Anleitung BACKUP-UND-WIEDERHERSTELLUNG um Kapitel 9 erweitert: Sicherung und
  Wiederherstellung der Datenbankdatei, inklusive Notfallplan. FEHLERBEHEBUNG
  hat ein Kapitel 11 für den Solo-Platz.
- Vehistra steht unter der **MIT-Lizenz**. Die Datei `LICENSE` liegt im
  Repository und wird mitinstalliert.
- `SIGNING-POLICY.md` beschreibt, wie die Auslieferungen gebaut und signiert
  werden. Der Releaseablauf signiert die Installationspakete, sobald ein
  Signaturzugang hinterlegt ist – ohne Zugang läuft er unverändert weiter und
  weist nur darauf hin.

Behoben
- Die Systemdiagnose und das Supportpaket nannten beim Solo-Platz einen
  Servernamen, den es nicht gibt. Sie nennen jetzt Betriebsart, Datenbankdatei
  und deren Größe.
- Die Wiederherstellungshilfe und das Fenster bei nicht erreichbarer Datenbank
  verwiesen beim Solo-Platz auf SQL-Server-Dienst, Firewall und Management
  Studio – alles Dinge, die es dort nicht gibt.
- Die Systemdiagnose meldete beim Solo-Platz „0,00 MB" für eine gefüllte
  Datenbank. Sie zählte nur die `.db`-Datei, während die jüngsten Änderungen
  noch im Begleitprotokoll standen.

Verbessert
- Datenbankfehler des Solo-Platzes werden in verständliche Hinweise übersetzt
  (Datei gesperrt, kein Schreibrecht, Datenträger voll, Datei beschädigt).
- Die MIT-Lizenz wird als `LIZENZ.txt` mitinstalliert.
- Das Datenbankschema liegt in zwei eigenen Projekten – eines für den
  Netzwerkbetrieb, eines für den Solo-Platz. Beide Betriebsarten nutzen
  dasselbe Datenmodell.

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
