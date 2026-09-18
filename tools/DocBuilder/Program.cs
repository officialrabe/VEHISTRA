using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using Vehistra.DocBuilder;

// -----------------------------------------------------------------------------
// VehistraDocBuilder - erzeugt aus den Markdown-Anleitungen echte Vektor-PDFs.
//
//   VehistraDocBuilder <Quellordner> <Zielordner> [Version]
//
// Entwickelt von LSP Virtual Services - vehistra.dev
// -----------------------------------------------------------------------------

QuestPDF.Settings.License = LicenseType.Community;

// Die Schrift fuer Codebloecke wird mitgeliefert, damit Diagramme auf jedem
// Rechner gleich ausgerichtet sind - unabhaengig davon, was installiert ist.
using (var font = typeof(GuideDocument).Assembly.GetManifestResourceStream("DejaVuSansMono.ttf")
                  ?? throw new InvalidOperationException("Die eingebettete Schriftdatei fehlt."))
{
    QuestPDF.Drawing.FontManager.RegisterFontFromStream(font);
}

var source = args.Length > 0 ? args[0] : "docs";
var target = args.Length > 1 ? args[1] : Path.Combine("artifacts", "docs");
var version = args.Length > 2 ? args[2] : "1.0.0";

if (!Directory.Exists(source))
{
    Console.Error.WriteLine($"Der Quellordner wurde nicht gefunden: {source}");
    return 1;
}

Directory.CreateDirectory(target);

var files = Directory.GetFiles(source, "*.md", SearchOption.TopDirectoryOnly)
    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
    .ToList();

if (files.Count == 0)
{
    Console.Error.WriteLine($"Im Quellordner liegen keine Markdown-Dateien: {source}");
    return 1;
}

Console.WriteLine($"Anleitungen werden erzeugt (Version {version}):");

foreach (var file in files)
{
    var markdown = await File.ReadAllTextAsync(file).ConfigureAwait(false);
    var blocks = MarkdownDocument.Parse(markdown);

    var title = blocks.FirstOrDefault(b => b.Kind == BlockKind.Heading1)?.Text
                ?? Path.GetFileNameWithoutExtension(file);

    var name = Path.GetFileNameWithoutExtension(file);

    // Markdown unveraendert mitliefern
    File.Copy(file, Path.Combine(target, Path.GetFileName(file)), overwrite: true);

    // PDF erzeugen
    var pdfPath = Path.Combine(target, name + ".pdf");
    new GuideDocument(title, version, blocks).GeneratePdf(pdfPath);

    var size = new FileInfo(pdfPath).Length / 1024;
    Console.WriteLine($"  {name}.md -> {name}.pdf ({size} KB, {blocks.Count} Abschnitte)");
}

Console.WriteLine($"Fertig: {files.Count} Anleitung(en) in {Path.GetFullPath(target)}");
return 0;
