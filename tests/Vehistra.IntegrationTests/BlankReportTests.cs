using System.Text;
using Vehistra.Application.Abstractions;

namespace Vehistra.IntegrationTests;

/// <summary>
/// Blankoformulare: Werkstattbericht und Unfallbericht ohne Datenbezug. Sie
/// werden gebraucht, bevor ueberhaupt etwas erfasst ist - das Formular fuers
/// Handschuhfach. Geprueft wird, dass dabei wirklich eine PDF-Datei entsteht.
/// </summary>
public class BlankReportTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Der_Blanko_Werkstattbericht_entsteht()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.SignInAsAdministrator();

        var builder = database.Service<IReportBuilder>();
        var bericht = await builder.BuildWorkshopReportAsync(cancellationToken: Token);

        bericht.IsBlankForm.ShouldBeTrue();
        bericht.Content.Length.ShouldBeGreaterThan(1000);
        Encoding.ASCII.GetString(bericht.Content, 0, 5).ShouldBe("%PDF-");
        bericht.SuggestedFileName.ShouldEndWith(".pdf");
        bericht.VehicleId.ShouldBeNull("Ein Blankoformular gehoert zu keinem Fahrzeug.");
    }

    [Fact]
    public async Task Der_Blanko_Unfallbericht_entsteht()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.SignInAsAdministrator();

        var builder = database.Service<IReportBuilder>();
        var bericht = await builder.BuildAccidentReportAsync(cancellationToken: Token);

        bericht.IsBlankForm.ShouldBeTrue();
        bericht.Content.Length.ShouldBeGreaterThan(1000);
        Encoding.ASCII.GetString(bericht.Content, 0, 5).ShouldBe("%PDF-");
        bericht.SuggestedFileName.ShouldEndWith(".pdf");
    }

    [Fact]
    public async Task Ohne_Druckrecht_entsteht_kein_Bericht()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.SignInWith(Vehistra.Domain.Security.Permissions.WorkshopView);

        var builder = database.Service<IReportBuilder>();

        await Should.ThrowAsync<Vehistra.Domain.Exceptions.PermissionDeniedException>(() =>
            builder.BuildWorkshopReportAsync(cancellationToken: Token));
    }
}
