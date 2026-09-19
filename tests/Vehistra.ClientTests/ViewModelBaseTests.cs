using Vehistra.Client.ViewModels;

namespace Vehistra.ClientTests;

/// <summary>
/// RunAsync haelt den Ladezustand und faengt Fehler ab. Der Fallstrick sitzt in
/// der Verschachtelung: sehr viele Befehle erledigen ihre Arbeit und laden danach
/// die Liste neu - und dieses Neuladen geht selbst wieder ueber RunAsync. Wird
/// die innere Arbeit dabei verworfen, aendert sich die Anzeige nicht, bis jemand
/// von Hand auf "Aktualisieren" klickt. Genau das war der Fehler.
/// </summary>
public class ViewModelBaseTests
{
    /// <summary>Kleinstes Ansichtsmodell, das RundAsync nach aussen zugaenglich macht.</summary>
    private sealed class Probe : ViewModelBase
    {
        public int Laeufe { get; private set; }

        public Task<bool> RunPublicAsync(Func<Task> action, string? successMessage = null) =>
            RunAsync(action, successMessage);

        /// <summary>Steht fuer das "Liste neu laden" der echten Ansichtsmodelle.</summary>
        public Task<bool> NeuLadenAsync() =>
            RunAsync(async () =>
            {
                Laeufe++;
                await Task.Yield();
            });
    }

    [Fact]
    public async Task Verschachtelte_Arbeit_wird_ausgefuehrt_und_nicht_verworfen()
    {
        var probe = new Probe();

        var ergebnis = await probe.RunPublicAsync(async () => await probe.NeuLadenAsync());

        ergebnis.ShouldBeTrue();
        probe.Laeufe.ShouldBe(1, "Die innere Arbeit muss laufen, sonst bleibt die Liste leer.");
        probe.IsBusy.ShouldBeFalse();
    }

    [Fact]
    public async Task Auch_mehrere_Ebenen_tief_wird_gearbeitet()
    {
        var probe = new Probe();

        await probe.RunPublicAsync(async () =>
            await probe.RunPublicAsync(async () =>
                await probe.NeuLadenAsync()));

        probe.Laeufe.ShouldBe(1);
    }

    [Fact]
    public async Task Ein_zweiter_Klick_waehrend_des_Wartens_wird_abgewiesen()
    {
        var probe = new Probe();
        var tor = new TaskCompletionSource();

        // Laeuft noch, wartet auf das Tor.
        var laufend = probe.RunPublicAsync(() => tor.Task);

        probe.IsBusy.ShouldBeTrue();

        // Der zweite Klick kommt von aussen, nicht aus der laufenden Aktion -
        // er darf nicht durchgelassen werden.
        var zweiter = await probe.RunPublicAsync(() => probe.NeuLadenAsync());

        zweiter.ShouldBeFalse();
        probe.Laeufe.ShouldBe(0);

        tor.SetResult();
        await laufend;

        probe.IsBusy.ShouldBeFalse();
    }

    [Fact]
    public async Task Nach_dem_Lauf_ist_die_Sperre_wieder_offen()
    {
        var probe = new Probe();

        await probe.RunPublicAsync(() => Task.CompletedTask);
        var zweiter = await probe.NeuLadenAsync();

        zweiter.ShouldBeTrue();
        probe.Laeufe.ShouldBe(1);
    }

    [Fact]
    public async Task Ein_Fehler_aus_der_inneren_Arbeit_erreicht_die_Fehlerbehandlung()
    {
        var probe = new Probe();

        var ergebnis = await probe.RunPublicAsync(async () =>
            await probe.RunPublicAsync(() => throw new InvalidOperationException("kaputt")));

        ergebnis.ShouldBeFalse();
        probe.HasError.ShouldBeTrue();
        probe.ErrorMessage.ShouldNotBeNull().ShouldContain("kaputt");
        probe.IsBusy.ShouldBeFalse("Auch nach einem Fehler darf der Ladezustand nicht haengen bleiben.");
    }

    [Fact]
    public async Task Ein_zweites_Ansichtsmodell_behaelt_seinen_eigenen_Ladezustand()
    {
        var aeusseres = new Probe();
        var inneres = new Probe();

        await aeusseres.RunPublicAsync(async () =>
        {
            await inneres.NeuLadenAsync();
            inneres.IsBusy.ShouldBeFalse();
        });

        inneres.Laeufe.ShouldBe(1);
    }

    [Fact]
    public async Task Eine_Erfolgsmeldung_wird_auch_verschachtelt_gesetzt()
    {
        var probe = new Probe();

        await probe.RunPublicAsync(
            async () => await probe.RunPublicAsync(() => Task.CompletedTask, "innen fertig"));

        probe.StatusMessage.ShouldBe("innen fertig");
    }
}
