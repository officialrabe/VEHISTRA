using Vehistra.Domain.Common;
using Vehistra.Domain.Enums;

namespace Vehistra.UnitTests;

/// <summary>Tests der fachlichen Hilfsfunktionen der Domaenenschicht.</summary>
public class LicensePlateFormatterTests
{
    [Theory]
    [InlineData("fds ab 123", "FDS-AB 123")]
    [InlineData("FDS-AB123", "FDS-AB 123")]
    [InlineData("  fds-ab-123  ", "FDS-AB 123")]
    [InlineData("S AB 1", "S-AB 1")]
    [InlineData("M-A 9999", "M-A 9999")]
    public void Normalize_bringt_Kennzeichen_in_die_Standardform(string input, string expected) =>
        LicensePlateFormatter.Normalize(input).ShouldBe(expected);

    [Theory]
    [InlineData("fds ab 123e", "FDS-AB 123E")]
    [InlineData("fds ab 123h", "FDS-AB 123H")]
    public void Normalize_behaelt_die_Kennungen_fuer_Elektro_und_Oldtimer(string input, string expected) =>
        LicensePlateFormatter.Normalize(input).ShouldBe(expected);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalize_liefert_bei_leerer_Eingabe_eine_leere_Zeichenkette(string? input) =>
        LicensePlateFormatter.Normalize(input).ShouldBeEmpty();

    [Fact]
    public void Normalize_laesst_unbekannte_Muster_unveraendert_gross_geschrieben() =>
        LicensePlateFormatter.Normalize("sonderfall 12/34").ShouldBe("SONDERFALL 12/34");

    [Theory]
    [InlineData("FDS-AB 123", "FDSAB123")]
    [InlineData("fds ab 123", "FDSAB123")]
    [InlineData("FDS AB123", "FDSAB123")]
    public void ToComparisonKey_ignoriert_Trennzeichen_und_Gross_Kleinschreibung(string input, string expected) =>
        LicensePlateFormatter.ToComparisonKey(input).ShouldBe(expected);

    [Fact]
    public void ToComparisonKey_erkennt_dasselbe_Kennzeichen_in_unterschiedlicher_Schreibweise() =>
        LicensePlateFormatter.ToComparisonKey("fds-ab 123")
            .ShouldBe(LicensePlateFormatter.ToComparisonKey("FDS AB123"));

    [Theory]
    [InlineData("FDS-AB 123", true)]
    [InlineData("M-A 1", true)]
    [InlineData("nicht plausibel", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsPlausible_erkennt_deutsche_Kennzeichen(string? input, bool expected) =>
        LicensePlateFormatter.IsPlausible(input).ShouldBe(expected);
}

public class DueDateCalculatorTests
{
    private static readonly DateTime Today = new(2026, 3, 14);

    [Fact]
    public void Evaluate_meldet_kritisch_wenn_der_Termin_abgelaufen_ist() =>
        DueDateCalculator.Evaluate(Today.AddDays(-1), Today, DueDateCalculator.DefaultThresholds)
            .ShouldBe(WarningLevel.Kritisch);

    [Fact]
    public void Evaluate_meldet_bald_faellig_am_Tag_der_Faelligkeit() =>
        DueDateCalculator.Evaluate(Today, Today, DueDateCalculator.DefaultThresholds)
            .ShouldBe(WarningLevel.BaldFaellig);

    [Theory]
    [InlineData(1, WarningLevel.BaldFaellig)]
    [InlineData(14, WarningLevel.BaldFaellig)]
    [InlineData(15, WarningLevel.Hinweis)]
    [InlineData(30, WarningLevel.Hinweis)]
    [InlineData(31, WarningLevel.Ok)]
    [InlineData(365, WarningLevel.Ok)]
    public void Evaluate_haelt_die_Standardschwellen_ein(int days, WarningLevel expected) =>
        DueDateCalculator.Evaluate(Today.AddDays(days), Today, DueDateCalculator.DefaultThresholds)
            .ShouldBe(expected);

    [Fact]
    public void Evaluate_meldet_inaktiv_wenn_kein_Termin_hinterlegt_ist() =>
        DueDateCalculator.Evaluate(null, Today, DueDateCalculator.DefaultThresholds)
            .ShouldBe(WarningLevel.Inaktiv);

    [Fact]
    public void Evaluate_beachtet_abweichende_Schwellen() =>
        DueDateCalculator.Evaluate(Today.AddDays(20), Today, new DueDateThresholds(30, 60))
            .ShouldBe(WarningLevel.BaldFaellig);

    [Fact]
    public void Evaluate_ignoriert_die_Uhrzeit() =>
        DueDateCalculator.Evaluate(Today.AddHours(23), Today.AddHours(1), DueDateCalculator.DefaultThresholds)
            .ShouldBe(WarningLevel.BaldFaellig);

    [Fact]
    public void DaysUntil_zaehlt_ueberfaellige_Tage_negativ() =>
        DueDateCalculator.DaysUntil(Today.AddDays(-5), Today).ShouldBe(-5);

    [Fact]
    public void DaysUntil_liefert_null_ohne_Termin() =>
        DueDateCalculator.DaysUntil(null, Today).ShouldBeNull();

    [Theory]
    [InlineData(-3, "abgelaufen seit 3 Tag(en)")]
    [InlineData(0, "heute faellig")]
    [InlineData(1, "morgen faellig")]
    [InlineData(9, "faellig in 9 Tagen")]
    public void Describe_formuliert_den_Termin_verstaendlich(int offset, string expected) =>
        DueDateCalculator.Describe(Today.AddDays(offset), Today).ShouldBe(expected);

    [Fact]
    public void Describe_nennt_fehlende_Termine_ausdruecklich() =>
        DueDateCalculator.Describe(null, Today).ShouldBe("Kein Termin hinterlegt");
}

public class DateRangeTests
{
    private static readonly DateTime Start = new(2026, 1, 1);

    [Fact]
    public void Contains_erkennt_den_Beginn_als_enthalten() =>
        new DateRange(Start, Start.AddDays(10)).Contains(Start).ShouldBeTrue();

    [Fact]
    public void Contains_erkennt_das_Ende_als_enthalten() =>
        new DateRange(Start, Start.AddDays(10)).Contains(Start.AddDays(10)).ShouldBeTrue();

    [Fact]
    public void Contains_schliesst_Zeitpunkte_davor_aus() =>
        new DateRange(Start, Start.AddDays(10)).Contains(Start.AddDays(-1)).ShouldBeFalse();

    [Fact]
    public void Ein_offener_Zeitraum_enthaelt_jeden_spaeteren_Zeitpunkt()
    {
        var range = new DateRange(Start, null);

        range.IsOpen.ShouldBeTrue();
        range.Contains(Start.AddYears(10)).ShouldBeTrue();
    }

    [Fact]
    public void Overlaps_erkennt_ueberschneidende_Zeitraeume() =>
        new DateRange(Start, Start.AddDays(10))
            .Overlaps(new DateRange(Start.AddDays(5), Start.AddDays(15)))
            .ShouldBeTrue();

    [Fact]
    public void Overlaps_erkennt_getrennte_Zeitraeume() =>
        new DateRange(Start, Start.AddDays(10))
            .Overlaps(new DateRange(Start.AddDays(11), Start.AddDays(15)))
            .ShouldBeFalse();

    [Fact]
    public void Overlaps_erkennt_die_Ueberschneidung_mit_einem_offenen_Zeitraum() =>
        new DateRange(Start, null)
            .Overlaps(new DateRange(Start.AddYears(5), null))
            .ShouldBeTrue();
}
