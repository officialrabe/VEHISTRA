namespace Fuhrpark.Domain.Common;

/// <summary>
/// Zeitraum mit optionalem Ende. Wird fuer Historisierungen (Fahrer, Kennzeichen, Zulassung) verwendet.
/// </summary>
public readonly record struct DateRange(DateTime From, DateTime? To)
{
    public bool IsOpen => To is null;

    public bool Contains(DateTime moment) => moment >= From && (To is null || moment <= To.Value);

    public bool Overlaps(DateRange other)
    {
        var thisEnd = To ?? DateTime.MaxValue;
        var otherEnd = other.To ?? DateTime.MaxValue;
        return From <= otherEnd && other.From <= thisEnd;
    }
}
