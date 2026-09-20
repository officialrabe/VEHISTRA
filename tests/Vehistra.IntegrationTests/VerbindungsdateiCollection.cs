namespace Vehistra.IntegrationTests;

/// <summary>
/// Die Verbindungsdaten liegen in genau einer Datei je Rechner
/// (<see cref="Vehistra.Infrastructure.ApplicationPaths"/>). Jede Testklasse,
/// die sie schreibt, ueberschreibt damit auch die der anderen - und wer
/// danach die Sicherungsablage sucht, findet die fremde. Genau so fielen in
/// der CI zwei Aufbewahrungstests um, nachdem eine dritte Klasse dazukam.
///
/// Diese Sammlung laesst solche Klassen nacheinander laufen.
/// </summary>
[CollectionDefinition(Name)]
public sealed class VerbindungsdateiCollection
{
    public const string Name = "Verbindungsdatei";
}
