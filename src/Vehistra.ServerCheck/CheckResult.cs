namespace Vehistra.ServerCheck;

/// <summary>Bewertung eines Pruefpunkts.</summary>
public enum CheckState
{
    /// <summary>Alles in Ordnung.</summary>
    Ok = 0,

    /// <summary>Funktioniert, sollte aber angesehen werden.</summary>
    Warning = 1,

    /// <summary>Muss behoben werden, sonst arbeitet das Programm nicht.</summary>
    Problem = 2,

    /// <summary>Wurde nicht geprueft, weil eine Voraussetzung fehlt.</summary>
    Skipped = 3
}

/// <summary>
/// Ergebnis eines einzelnen Pruefpunkts. Die Meldung ist bewusst in einfacher
/// Sprache formuliert - niemals "SQL Error 53", sondern eine konkrete Anweisung.
/// </summary>
public sealed record CheckResult(
    string Group,
    string Name,
    CheckState State,
    string Message,
    IReadOnlyList<string>? Hints = null,
    string? TechnicalDetails = null)
{
    /// <summary>Kurzbezeichnung fuer Badge und Textbericht - Farbe ist nie die einzige Information.</summary>
    public string StateCaption => State switch
    {
        CheckState.Ok => "IN ORDNUNG",
        CheckState.Warning => "HINWEIS",
        CheckState.Problem => "PROBLEM",
        _ => "ÜBERSPRUNGEN"
    };

    public bool HasHints => Hints is { Count: > 0 };

    public string HintText => Hints is null
        ? string.Empty
        : string.Join(Environment.NewLine, Hints.Select(h => "• " + h));

    public static CheckResult Ok(string group, string name, string message, string? details = null)
        => new(group, name, CheckState.Ok, message, null, details);

    public static CheckResult Warning(string group, string name, string message, params string[] hints)
        => new(group, name, CheckState.Warning, message, hints);

    public static CheckResult Problem(string group, string name, string message, params string[] hints)
        => new(group, name, CheckState.Problem, message, hints);

    public static CheckResult Skipped(string group, string name, string message)
        => new(group, name, CheckState.Skipped, message);
}
