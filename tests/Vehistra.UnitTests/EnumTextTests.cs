using System.Reflection;
using Vehistra.Domain.Enums;

namespace Vehistra.UnitTests;

/// <summary>
/// Beschriftungen der Aufzaehlungswerte. Sie stehen am Bildschirm, in
/// Ausdrucken und in Exporten - und sie standen dort in falschem Deutsch:
/// "Wartet auf teile", "Verfuegbar", "Login failed".
/// </summary>
public class EnumTextTests
{
    [Theory]
    [InlineData(WorkshopOrderStatus.WartetAufTeile, "Wartet auf Teile")]
    [InlineData(WorkshopOrderStatus.InBearbeitung, "In Bearbeitung")]
    [InlineData(WorkshopOrderStatus.TerminVereinbart, "Termin vereinbart")]
    [InlineData(WorkshopOrderStatus.FahrzeugAbgegeben, "Fahrzeug abgegeben")]
    [InlineData(LicensePlateStatus.Verfuegbar, "Verfügbar")]
    [InlineData(LicensePlateStatus.AusserBetrieb, "Außer Betrieb")]
    [InlineData(InspectionType.UvvPruefung, "UVV-Prüfung")]
    [InlineData(InspectionResult.BestandenMitMaengeln, "Bestanden mit Mängeln")]
    [InlineData(AccidentType.UnfallMitFremdbeteiligung, "Unfall mit Fremdbeteiligung")]
    [InlineData(AuditAction.LoginFailed, "Anmeldung fehlgeschlagen")]
    [InlineData(AuditAction.PasswordReset, "Kennwort zurückgesetzt")]
    [InlineData(WarningLevel.BaldFaellig, "Bald fällig")]
    public void Der_Wert_wird_lesbar_beschriftet(object wert, string erwartet) =>
        EnumText.Of(wert).ShouldBe(erwartet);

    [Fact]
    public void Ohne_Angabe_wird_der_Bezeichner_getrennt() =>
        // Kein Eintrag noetig, wo die Trennung schon richtiges Deutsch ergibt.
        EnumText.Of(DamageStatus.Gemeldet).ShouldBe("Gemeldet");

    [Fact]
    public void Kein_Wert_traegt_noch_einen_ASCII_Umlaut()
    {
        var verdaechtig = new[] { "ue", "ae", "oe" };

        var treffer = new List<string>();

        foreach (var typ in typeof(EnumText).Assembly.GetTypes().Where(t => t.IsEnum && t.IsPublic))
        {
            foreach (var wert in Enum.GetValues(typ))
            {
                var text = EnumText.Of(wert);

                // "Neue", "Fuhrpark" und Ähnliches sind echte Wortbestandteile;
                // verdächtig ist nur, was aus einem Bezeichner stammt, der den
                // Umlaut ersetzt hat.
                var name = wert.ToString() ?? string.Empty;

                if (verdaechtig.Any(v => name.Contains(v, StringComparison.Ordinal))
                    && text == EnumText.Trenne(name))
                {
                    treffer.Add($"{typ.Name}.{name} -> {text}");
                }
            }
        }

        treffer.ShouldBeEmpty(
            "Diese Werte erscheinen mit ae/oe/ue in der Oberfläche: " + string.Join(", ", treffer));
    }
}
