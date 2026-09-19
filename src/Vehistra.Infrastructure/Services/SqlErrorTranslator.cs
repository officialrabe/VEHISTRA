using Microsoft.Data.Sqlite;
using Microsoft.Data.SqlClient;

namespace Vehistra.Infrastructure.Services;

/// <summary>
/// Uebersetzt technische SQL-Server-Fehler in verstaendliche Hinweise.
/// Statt "SQL Error 53" erhaelt der Anwender eine konkrete Pruefliste.
/// </summary>
public static class SqlErrorTranslator
{
    public static (string Message, IReadOnlyList<string> Hints) Translate(Exception exception, string server, string database)
    {
        if (exception is SqliteException sqliteException)
        {
            return TranslateSqlite(sqliteException);
        }

        if (exception is SqlException sqlException)
        {
            return sqlException.Number switch
            {
                -2 or 258 =>
                (
                    $"Die Verbindung zum SQL Server '{server}' ist in ein Zeitlimit gelaufen.",
                    new[]
                    {
                        "Ist der Server eingeschaltet und im Netzwerk erreichbar?",
                        "Antwortet der Server auf einen Ping?",
                        "Ist die Firewall fuer den SQL-Server-Port (Standard 1433) freigegeben?",
                        "Laeuft der Dienst 'SQL Server-Browser', wenn eine benannte Instanz verwendet wird?"
                    }
                ),

                53 or 17 or 1326 =>
                (
                    $"Der SQL Server '{server}' konnte nicht gefunden werden.",
                    new[]
                    {
                        "Laeuft der Dienst 'SQL Server (SQLEXPRESS)' auf dem Server?",
                        "Ist im SQL Server Configuration Manager das Protokoll TCP/IP aktiviert?",
                        "Ist die Windows-Firewall fuer den SQL Server freigegeben?",
                        "Ist der Servername korrekt geschrieben (z. B. FUHRPARK-SRV01\\SQLEXPRESS)?"
                    }
                ),

                18456 =>
                (
                    "Die Anmeldung am SQL Server wurde abgelehnt.",
                    new[]
                    {
                        "Stimmen Benutzername und Passwort?",
                        "Wurde dem Konto der Zugriff auf die Datenbank erteilt?",
                        "Ist bei SQL-Authentifizierung der gemischte Authentifizierungsmodus aktiviert?",
                        "Bei Windows-Authentifizierung: Besitzt das angemeldete Windows-Konto Zugriff?"
                    }
                ),

                4060 or 911 =>
                (
                    $"Die Datenbank '{database}' existiert auf dem Server '{server}' nicht.",
                    new[]
                    {
                        "Wurde die Servereinrichtung (VehistraServerSetup.exe) bereits ausgefuehrt?",
                        "Ist der Datenbankname korrekt geschrieben?",
                        "Besitzt das verwendete Konto Zugriff auf diese Datenbank?"
                    }
                ),

                262 or 229 or 230 =>
                (
                    "Dem verwendeten Konto fehlen Berechtigungen auf der Datenbank.",
                    new[]
                    {
                        "Das Konto benoetigt mindestens die Rollen 'db_datareader' und 'db_datawriter'.",
                        "Fuer Datenbankupdates werden zusaetzlich Rechte zum Aendern des Schemas benoetigt.",
                        "Fuer Sicherungen wird die Serverrolle 'dbcreator' bzw. 'db_backupoperator' benoetigt."
                    }
                ),

                _ =>
                (
                    $"Beim Zugriff auf den SQL Server ist ein Fehler aufgetreten (Fehlernummer {sqlException.Number}).",
                    new[]
                    {
                        "Bitte die technischen Details an den Support weitergeben.",
                        "Der Diagnosebericht unter 'Hilfe & Support' enthaelt weitere Informationen."
                    }
                )
            };
        }

        if (exception is PlatformNotSupportedException)
        {
            return (
                "Diese Funktion setzt Windows voraus.",
                new[] { "Die Anwendung wurde auf einem nicht unterstuetzten Betriebssystem gestartet." });
        }

        return (
            "Die Verbindung zur Fuhrparkdatenbank konnte nicht hergestellt werden.",
            new[]
            {
                "Ist der Server erreichbar?",
                "Sind die Servereinstellungen korrekt?",
                "Der Diagnosebereich unter 'Hilfe & Support' hilft bei der Eingrenzung."
            });
    }

    /// <summary>
    /// Uebersetzt Fehler des Solo-Platzes. Dort gibt es keinen Server, deshalb
    /// drehen sich die Hinweise um Datei, Ordner und Zugriffsrechte.
    /// </summary>
    private static (string Message, IReadOnlyList<string> Hints) TranslateSqlite(SqliteException exception) =>
        exception.SqliteErrorCode switch
        {
            14 =>   // SQLITE_CANTOPEN
            (
                "Die Datenbankdatei konnte nicht geoeffnet werden.",
                new[]
                {
                    "Existiert der Ordner, in dem die Datei liegen soll?",
                    "Besitzt das angemeldete Windows-Konto Schreibrechte auf diesen Ordner?",
                    "Liegt die Datei auf einem Wechseldatentraeger, der gerade nicht angeschlossen ist?"
                }
            ),

            8 =>    // SQLITE_READONLY
            (
                "Die Datenbankdatei ist schreibgeschuetzt.",
                new[]
                {
                    "Bitte im Explorer die Eigenschaften der Datei pruefen und den Schreibschutz entfernen.",
                    "Besitzt das angemeldete Windows-Konto Schreibrechte auf den Ordner?"
                }
            ),

            5 or 6 =>   // SQLITE_BUSY, SQLITE_LOCKED
            (
                "Die Datenbankdatei wird gerade von einem anderen Programm verwendet.",
                new[]
                {
                    "Laeuft Vehistra noch ein zweites Mal? Bitte alle Fenster schliessen.",
                    "Sichert gerade ein Sicherungsprogramm die Datei?",
                    "Beim Solo-Platz darf die Datei nicht auf einer Netzwerkfreigabe liegen."
                }
            ),

            11 or 26 => // SQLITE_CORRUPT, SQLITE_NOTADB
            (
                "Die Datenbankdatei ist beschaedigt oder keine gueltige Datenbank.",
                new[]
                {
                    "Bitte die letzte Sicherung zurueckspielen - siehe BACKUP-UND-WIEDERHERSTELLUNG.pdf.",
                    "Wurde die Datei von Hand kopiert, waehrend Vehistra lief?"
                }
            ),

            13 =>   // SQLITE_FULL
            (
                "Auf dem Laufwerk ist kein Platz mehr frei.",
                new[]
                {
                    "Bitte Speicherplatz freigeben und den Vorgang wiederholen.",
                    "Auch der Ordner fuer die Sicherungen braucht freien Platz."
                }
            ),

            _ =>
            (
                $"Beim Zugriff auf die Datenbankdatei ist ein Fehler aufgetreten (Fehlernummer {exception.SqliteErrorCode}).",
                new[]
                {
                    "Bitte die technischen Details an den Support weitergeben.",
                    "Der Diagnosebericht unter 'Hilfe & Support' enthaelt weitere Informationen."
                }
            )
        };
}
