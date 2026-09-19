# Änderungen

Alle bemerkenswerten Änderungen an Vehistra. Die Versionsnummern folgen
`HAUPT.NEBEN.KORREKTUR` – siehe `docs/UPDATE-ANLEITUNG.md`, Abschnitt 8.

Der Release-Workflow liest die Abschnitte dieser Datei und übernimmt sie in die
Versionshinweise der Veröffentlichung.

## 1.4.0

Neu
- **Updates ohne Updateablage.** Das Programm kann die Updates unmittelbar von
  der Veröffentlichungsseite des Projekts holen: suchen, herunterladen,
  Prüfsumme prüfen, installieren – alles aus dem Programm heraus. Gedacht vor
  allem für den Solo-Platz, auf dem es keine Netzwerkfreigabe gibt.
  Einzuschalten unter *Updates · Quelle*; im Auslieferungszustand bleibt die
  Updateablage im Firmennetz eingestellt, und ohne diese Umstellung greift das
  Programm nie von sich aus ins Internet. Übertragen wird nur die Anfrage
  selbst – keine Fahrzeug-, Fahrer- oder Betriebsdaten. Angesprochen werden
  ausschließlich die Adressen von GitHub; jede andere wird abgelehnt. Fehlt zu
  einer Veröffentlichung die Datei `checksums.sha256`, wird das Update gar
  nicht erst angeboten.
- **Blankoformulare dort, wo man sie sucht.** „Blankoformular drucken" gibt es
  jetzt auch auf der Werkstatt- und der Unfallseite, ohne Umweg über *Berichte
  & Formulare* und ohne dass ein Vorgang ausgewählt sein muss.

Behoben
- **Fehler waren unsichtbar.** 14 der 20 Ansichten zeigten eine Fehlermeldung
  nirgends an, und protokolliert wurde sie auch nicht. Schlug eine Aktion fehl,
  sah das aus wie eine tote Schaltfläche. Die Fehlerleiste sitzt jetzt im
  Hauptfenster und gilt damit für jede Ansicht, und jede Ausnahme steht im
  Protokoll – und damit auch im Supportpaket.
- **Schaltflächen, die eine Auswahl brauchen, sind jetzt abgeblendet**, solange
  nichts ausgewählt ist. 45 Befehle in 16 Ansichten kehrten bisher wortlos
  zurück – für den Anwender nicht von einer kaputten Schaltfläche zu
  unterscheiden. Betroffen waren unter anderem „Bearbeiten“, „Löschen“,
  „Status ändern“, „Bericht drucken“, „Kilometerstand erfassen“ und die elf
  Befehle der Stammdatenkataloge.
- **Falsches Deutsch bei Aufzählungswerten.** Am Bildschirm stand „Wartet auf
  teile“, „Verfuegbar“, „Bald faellig“ und im Protokoll „Login failed“ – der
  Bezeichner wurde maschinell getrennt und kleingeschrieben. Jeder Wert trägt
  jetzt seine Beschriftung, und dieselbe steht auch in Excel, CSV und PDF: dort
  stand bisher der blanke Bezeichner. Ebenso in den Hinweisen des Überblicks
  („TUEV … faellig“) und in der Kopfzeile der Fahrzeugakte („1 offene Schäden“
  statt „1 offener Schaden“).
- **Berichte melden, wo die Datei liegt.** Ein Bericht öffnet sich in einem
  anderen Programm; blieb das aus, stand nirgends, wohin er gespeichert wurde.

## 1.3.0

Diese Version schließt die letzten fünf Punkte aus dem Abgleich mit der
bisherigen Web-Version. Damit ist die Windows-Anwendung in allen verglichenen
Punkten gleichwertig oder weiter.

Ab dieser Version werden Veröffentlichungen nicht mehr als Beta gekennzeichnet.

Neu
- **Kataloge vollständig bearbeitbar.** Fahrzeugkategorien, Schadenskategorien
  und Fahrzeugstatus haben jetzt einen richtigen Bearbeitungsdialog: Farbe,
  Beschreibung, Reihenfolge und Zustand statt nur einer Eingabezeile für den
  Namen. Beim Fahrzeugstatus kommt die **fachliche Bedeutung** hinzu – sie
  entscheidet, welchen Status ein Ablauf setzt (Ausmusterung, Werkstatt,
  Import). Alle neun Bedeutungen sind mitgeliefert und vergeben, deshalb wird
  eine Bedeutung nicht neu erfunden, sondern **übertragen**: der bisherige
  Träger gibt sie im selben Schritt ab, sodass sie immer genau ein Status
  trägt. Der Dialog fragt vorher nach. Entfernen bleibt gesperrt, sonst würde
  ein Ablauf stillschweigend keinen Status mehr setzen.
- **Dritte Warnstufe beim TÜV.** Neben „bald fällig" und „Hinweis" ist jetzt
  einstellbar, ab wie vielen Tagen **vor** dem Termin eine Frist als kritisch
  gilt (Voreinstellung 7 Tage). Bisher wurde eine Frist erst kritisch, wenn sie
  abgelaufen war. Die drei Stufen werden beim Speichern in eine sinnvolle
  Reihenfolge gebracht.
- **Werkstatt-Langzeitwarnung einstellbar.** Wie lange ein Fahrzeug in der
  Werkstatt stehen darf, bevor es auffällt, stand mit sieben und vierzehn Tagen
  fest im Code. Die Frist ist jetzt einstellbar (Voreinstellung 7 Tage, ab dem
  Doppelten kritisch). Der Überblick zeigt die Zahl, ein Klick darauf öffnet
  die Werkstattliste mit genau diesem Filter.
- **Aufbewahrungsdauer der Sicherungen.** Das Programm kann alte Sicherungen
  selbst entfernen (Einstellungen · Sicherungen). Voreinstellung 0, also aus –
  Löschen lässt sich nicht zurückholen. Gelöscht wird nur im eingestellten
  Verzeichnis, nur was dem eigenen Namensschema entspricht; die drei neuesten
  Sicherungen und alle Sicherungen vor einer Migration bleiben immer erhalten.
  Jede Löschung steht im Protokoll, und „Alte Sicherungen aufräumen" zeigt
  vorher, was wegfallen würde.
- **Fahrerakte.** Alles zu einem Fahrer auf einer Seite: Stammdaten, Kontakt,
  alle Fahrzeuge mit Historie, Schäden, Unfälle und Werkstattvorgänge. Zu
  öffnen aus der Fahrerliste, aus der Registerkarte „Fahrer" der Fahrzeugakte
  und aus der globalen Suche. Jeder Bereich prüft sein eigenes Recht.
- **Zähler an den Registerkarten der Fahrzeugakte**, etwa „SCHÄDEN (3)". Leere
  Bereiche bleiben ohne Zahl.

Behoben
- **Zwei Prüfungen liefen ins Leere.** Sowohl beim Stilllegen des letzten
  aktiven Fahrzeugstatus als auch beim Umbenennen einer mitgelieferten
  Schadenskategorie wurde der neue Wert mit dem geladenen Objekt verglichen.
  Hatte der Aufrufer genau dieses Objekt bearbeitet, waren beide Werte gleich
  und die Sperre wirkungslos. Die Vergleichswerte kommen jetzt frisch aus der
  Datenbank.
- **Benachrichtigung „TÜV abgelaufen"** trug diesen Titel künftig auch bei noch
  gültigen Fristen, weil er an der Warnstufe hing. Er hängt jetzt am Datum.

## 1.2.0

Diese Version schließt die Lücken gegenüber der bisherigen Web-Version. Der
Abgleich beider Programme hat neun Unterschiede ergeben; die vier wichtigsten
sind hier behoben.

Neu
- **Listen als Excel oder CSV.** Die Berichtsseite exportierte alle neun Listen
  ausschließlich als PDF. Format ist jetzt wählbar – Excel, CSV oder PDF – mit
  einem Satz dazu, was die Auswahl bedeutet. Voreinstellung ist Excel.
- **Kennzahlen im Dashboard sind anklickbar.** Ein Klick auf eine Zahl öffnet
  die Liste dahinter, bereits gefiltert: „ohne festen Fahrer", „TÜV
  abgelaufen", „offene Schäden", „in der Werkstatt" und 18 weitere. Bisher
  musste man die Zahl lesen und den Filter von Hand nachbauen.
- **Vier neue Filter in der Fahrzeugliste**: Hersteller (aus dem Bestand, nicht
  aus einer festen Liste), Fahrerzuordnung (mit/ohne festen Fahrer), Anmeldung
  (angemeldet/abgemeldet) und die Hauptuntersuchung nach Stufe (abgelaufen oder
  fällig in 14, 30, 60 Tagen) statt nur „fällig ja/nein".
- **Export „Fahrerzuordnungen"**: vollständige Historie der festen Fahrer je
  Fahrzeug, mit Von, Bis („laufend", solange offen), zugewiesen durch und
  Bemerkung.
- **Maximale Dateigröße für Dokumente einstellbar** (Standard 25 MB). Die
  Ablage begrenzt weiterhin hart auf 50 MB; die Einstellung wird beim Speichern
  auf diesen Bereich begrenzt, damit die Oberfläche nichts zusagt, was die
  Ablage nicht hält.

Behoben
- **Dokumente werden nach ihrem Inhalt geprüft, nicht nach der Dateiendung.**
  Was nach einem Programm aussieht (EXE/DLL, ELF, Java, Shebang), wird
  abgelehnt – auch wenn die Datei „Rechnung.pdf" heißt. Passt der Inhalt nicht
  zur Endung, etwa ein PNG als `.pdf`, wird ebenfalls abgelehnt. Formate ohne
  verlässliche Signatur (`.txt`, `.csv`, `.eml`) bleiben erlaubt. Das war die
  einzige Stelle, an der die Web-Version sicherer war.
- Der Listenexport verlangt jetzt ausdrücklich das Recht „Daten exportieren".
- Dateinamen der Exporte sind auf Deutsch, nicht mehr englische interne Namen.

Geprüft
- Zwölf Modultests gegen die echte Dokumentenablage: umbenannte EXE, ELF,
  PNG-als-PDF, leere Datei, Pfadanteile im Dateinamen.
- Sieben Integrationstests für die neuen Filter, einzeln und in Kombination,
  inklusive der gestaffelten Fristen.
- 122 Integrations-, 93 Modul- und 7 Ansichtsmodelltests grün.

Nicht geprüft
- Dass ein Klick auf eine Kennzahl im Programm tatsächlich die gefilterte Liste
  öffnet. Die Verdrahtung ist übersetzt, aber nur ein Durchlauf auf einem
  Windows-Rechner zeigt das Verhalten.

## 1.1.1

Behoben
- **Die Prüfsumme des Updatepakets wurde nie geprüft.** Die Prüfung war
  vorhanden und getestet, `latest.json` trägt den Wert, und die Anleitung sagte
  zu, dass geprüft wird – aufgerufen hat die Prüfung niemand. Damit lief eine
  `Vehistra-Update.exe` aus der Updateablage ungeprüft mit
  Administratorrechten. Jetzt prüft der Updatedienst selbst, unmittelbar vor dem
  Start, und startet bei einer Abweichung nichts.
  - Erwartet wird der Wert aus `latest.json`; fehlt er, wird
    `checksums.sha256` neben dem Paket herangezogen.
  - Gibt es keinen von beiden, gilt das Paket als ungeprüft und wird ebenfalls
    nicht gestartet. Die Meldung nennt die tatsächliche Prüfsumme zum
    Nachtragen.
  - Die Prüfung sitzt im Dienst, nicht in der Oberfläche – so kann sie kein
    Aufrufer übergehen.
  - Unter „Updates" steht das Ergebnis schon vor dem Klick auf „Installieren",
    mit Text und nicht nur über die Farbe.

  Was die Prüfsumme leistet und was nicht, steht jetzt auch in der Anleitung:
  sie erkennt ein unvollständig kopiertes oder verändertes Paket, schützt aber
  nicht gegen jemanden, der auf die Updateablage schreiben darf. Dagegen hilft
  die Codesignatur und ein Updateordner, in den nur Administratoren schreiben.

Geprüft
- Sieben Tests für die Prüfsummenprüfung, darunter das nachträglich vertauschte
  Paket und der Nachweis, dass der Updater bei einer Abweichung nicht startet.

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
