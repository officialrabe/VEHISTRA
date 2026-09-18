using Microsoft.EntityFrameworkCore;
using Vehistra.Application.Abstractions;
using Vehistra.Application.Dtos;

namespace Vehistra.IntegrationTests;

/// <summary>
/// Der Import darf niemals stillschweigend ueberschreiben und bei Fehlern nichts
/// halb Geschriebenes hinterlassen.
/// </summary>
public class ImportExportTests : IDisposable
{
    private readonly string _workingDirectory =
        Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "vehistra-tests-" + Guid.NewGuid().ToString("N"))).FullName;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        if (Directory.Exists(_workingDirectory))
        {
            Directory.Delete(_workingDirectory, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    private string WriteCsv(string name, string content)
    {
        var path = Path.Combine(_workingDirectory, name);
        File.WriteAllText(path, content, System.Text.Encoding.UTF8);
        return path;
    }

    private const string VehicleCsv =
        "Interne Fahrzeugnummer;Kennzeichen;Hersteller;Modell;Baujahr\n" +
        "T-01;FDS-AB 123;Mercedes-Benz;E-Klasse;2022\n" +
        "T-02;FDS-AB 124;Volkswagen;Caddy;2021\n" +
        "T-03;FDS-AB 125;Skoda;Octavia;2023\n";

    [Fact]
    public async Task Die_Vorschau_erkennt_Spalten_und_Zeilenzahl()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.SignInAsAdministrator();

        var path = WriteCsv("fahrzeuge.csv", VehicleCsv);
        var preview = await database.Service<IImportService>().PreviewAsync(path, Token);

        preview.Columns.ShouldContain("Kennzeichen");
        preview.Columns.ShouldContain("Hersteller");
        preview.TotalRows.ShouldBe(3);
        preview.SampleRows.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Der_Assistent_schlaegt_eine_Spaltenzuordnung_vor()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.SignInAsAdministrator();

        var importer = database.Service<IImportService>();
        var mapping = importer.SuggestMapping(
            ExportArea.Vehicles,
            ["Interne Fahrzeugnummer", "Kennzeichen", "Hersteller", "Modell", "Baujahr"]);

        mapping.ShouldNotBeEmpty();
        mapping.Count(m => m.TargetField is not null).ShouldBeGreaterThan(2);
    }

    [Fact]
    public async Task Ein_gueltiger_Import_legt_alle_Fahrzeuge_an()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.SignInAsAdministrator();

        var path = WriteCsv("fahrzeuge.csv", VehicleCsv);
        var importer = database.Service<IImportService>();

        var mapping = importer.SuggestMapping(
            ExportArea.Vehicles,
            ["Interne Fahrzeugnummer", "Kennzeichen", "Hersteller", "Modell", "Baujahr"]);

        var result = await importer.ImportAsync(path, ExportArea.Vehicles, mapping, false,
            cancellationToken: Token);

        result.Imported.ShouldBe(3);
        result.Failed.ShouldBe(0);
        (await database.Db.Vehicles.CountAsync(Token)).ShouldBe(3);
    }

    [Fact]
    public async Task Ohne_ausdrueckliche_Erlaubnis_wird_nichts_ueberschrieben()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.SignInAsAdministrator();

        var path = WriteCsv("fahrzeuge.csv", VehicleCsv);
        var importer = database.Service<IImportService>();

        var mapping = importer.SuggestMapping(
            ExportArea.Vehicles,
            ["Interne Fahrzeugnummer", "Kennzeichen", "Hersteller", "Modell", "Baujahr"]);

        await importer.ImportAsync(path, ExportArea.Vehicles, mapping, false, cancellationToken: Token);

        // Dieselbe Datei erneut - ohne "Bestehende aktualisieren".
        var second = await importer.ImportAsync(path, ExportArea.Vehicles, mapping, false,
            cancellationToken: Token);

        second.Imported.ShouldBe(0);
        second.Skipped.ShouldBe(3);
        (await database.Db.Vehicles.CountAsync(Token)).ShouldBe(3);
    }

    [Fact]
    public async Task Mit_ausdruecklicher_Erlaubnis_werden_Datensaetze_aktualisiert()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.SignInAsAdministrator();

        var importer = database.Service<IImportService>();
        var columns = new[] { "Interne Fahrzeugnummer", "Kennzeichen", "Hersteller", "Modell", "Baujahr" };
        var mapping = importer.SuggestMapping(ExportArea.Vehicles, columns);

        await importer.ImportAsync(WriteCsv("erst.csv", VehicleCsv), ExportArea.Vehicles, mapping, false,
            cancellationToken: Token);

        var changed = WriteCsv("zweit.csv",
            "Interne Fahrzeugnummer;Kennzeichen;Hersteller;Modell;Baujahr\n" +
            "T-01;FDS-AB 123;Mercedes-Benz;C-Klasse;2022\n");

        var result = await importer.ImportAsync(changed, ExportArea.Vehicles, mapping, true,
            cancellationToken: Token);

        result.Imported.ShouldBe(1);

        var vehicle = await database.Db.Vehicles
            .AsNoTracking()
            .SingleAsync(v => v.InternalNumber == "T-01", Token);

        vehicle.Model.ShouldBe("C-Klasse");
    }

    [Fact]
    public async Task Fehlende_Pflichtfelder_werden_vor_dem_Schreiben_gemeldet()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.SignInAsAdministrator();

        var path = WriteCsv("luecken.csv",
            "Interne Fahrzeugnummer;Kennzeichen;Hersteller;Modell\n" +
            "T-01;FDS-AB 123;Mercedes-Benz;E-Klasse\n" +
            ";FDS-AB 124;Volkswagen;Caddy\n");

        var importer = database.Service<IImportService>();
        var mapping = importer.SuggestMapping(
            ExportArea.Vehicles, ["Interne Fahrzeugnummer", "Kennzeichen", "Hersteller", "Modell"]);

        var validation = await importer.ValidateAsync(path, ExportArea.Vehicles, mapping, Token);

        validation.HasErrors.ShouldBeTrue();
        validation.Issues.ShouldContain(i => i.RowNumber == 3 && i.IsError);
        validation.InvalidRows.ShouldBe(1);

        // Die Pruefung allein darf nichts schreiben.
        (await database.Db.Vehicles.CountAsync(Token)).ShouldBe(0);
    }

    [Fact]
    public async Task Ohne_Importberechtigung_wird_nichts_geschrieben()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.SignInWith(Domain.Security.Permissions.VehicleView);

        var path = WriteCsv("fahrzeuge.csv", VehicleCsv);
        var importer = database.Service<IImportService>();
        var mapping = new List<ImportColumnMapping>();

        await Should.ThrowAsync<Domain.Exceptions.PermissionDeniedException>(() =>
            importer.ImportAsync(path, ExportArea.Vehicles, mapping, false, cancellationToken: Token));
    }

    [Fact]
    public async Task Der_CSV_Export_enthaelt_Kopfzeile_und_alle_Datensaetze()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.SignInAsAdministrator();

        await TestData.AddVehicleAsync(database.Db, "T-01", "FDS-AB 123", cancellationToken: Token);
        await TestData.AddVehicleAsync(database.Db, "T-02", "FDS-AB 124", cancellationToken: Token);

        var target = Path.Combine(_workingDirectory, "export.csv");
        var path = await database.Service<IExportService>()
            .ExportAsync(ExportArea.Vehicles, ExportFormat.Csv, target, Token);

        File.Exists(path).ShouldBeTrue();

        var lines = await File.ReadAllLinesAsync(path, Token);

        lines.Length.ShouldBe(3);
        lines[0].ShouldContain("Kennzeichen");
        string.Join('\n', lines).ShouldContain("FDS-AB 124");
    }

    [Fact]
    public async Task Der_Excel_Export_erzeugt_eine_gueltige_Arbeitsmappe()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.SignInAsAdministrator();

        await TestData.AddVehicleAsync(database.Db, cancellationToken: Token);

        var target = Path.Combine(_workingDirectory, "export.xlsx");
        var path = await database.Service<IExportService>()
            .ExportAsync(ExportArea.Vehicles, ExportFormat.Xlsx, target, Token);

        var bytes = await File.ReadAllBytesAsync(path, Token);

        // XLSX ist ein ZIP-Archiv und beginnt mit "PK".
        bytes[0].ShouldBe((byte)'P');
        bytes[1].ShouldBe((byte)'K');
    }

    [Fact]
    public async Task Ohne_Exportberechtigung_wird_nichts_geschrieben()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.SignInWith(Domain.Security.Permissions.VehicleView);

        var target = Path.Combine(_workingDirectory, "export.csv");

        await Should.ThrowAsync<Domain.Exceptions.PermissionDeniedException>(() =>
            database.Service<IExportService>().ExportAsync(ExportArea.Vehicles, ExportFormat.Csv, target, Token));

        File.Exists(target).ShouldBeFalse();
    }
}
