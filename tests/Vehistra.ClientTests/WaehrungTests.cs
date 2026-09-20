using System.Globalization;
using Vehistra.Client.Converters;

namespace Vehistra.ClientTests;

/// <summary>
/// In den Tabellen stand bei leeren Kostenfeldern ein nacktes „EUR“ - so, als
/// wäre der Betrag null Euro oder verloren gegangen. Gemeldet wurde es als
/// Schönheitsfehler; die Verwechslungsgefahr ist aber real, wenn eine Spalte
/// mit Beträgen plötzlich eine Zeile ohne Zahl enthält.
/// </summary>
public class WaehrungTests
{
    private static readonly CultureInfo Deutsch = new("de-DE");

    private static string Formatiere(object? wert) =>
        (string)new CurrencyConverter().Convert(wert, typeof(string), null, Deutsch);

    [Fact]
    public void Ohne_Betrag_steht_ein_Gedankenstrich()
    {
        Formatiere(null).ShouldBe("–");
    }

    [Fact]
    public void Ein_Betrag_wird_mit_Waehrung_gezeigt()
    {
        Formatiere(1234.5m).ShouldBe("1.234,50 EUR");
    }

    [Fact]
    public void Null_Euro_bleibt_null_Euro()
    {
        // Ein eingetragener Betrag von 0 ist etwas anderes als kein Eintrag.
        Formatiere(0m).ShouldBe("0,00 EUR");
    }

    [Fact]
    public void Auch_ganze_Zahlen_bekommen_zwei_Nachkommastellen()
    {
        Formatiere(42).ShouldBe("42,00 EUR");
    }
}
