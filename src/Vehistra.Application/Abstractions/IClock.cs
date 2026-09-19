namespace Vehistra.Application.Abstractions;

/// <summary>Abstraktion der Systemzeit, damit Fristenlogik testbar bleibt.</summary>
public interface IClock
{
    DateTime Now { get; }

    DateTime Today { get; }

    DateTime UtcNow { get; }
}

/// <summary>Standardimplementierung auf Basis der lokalen Systemzeit.</summary>
public sealed class SystemClock : IClock
{
    public DateTime Now => DateTime.Now;

    public DateTime Today => DateTime.Today;

    public DateTime UtcNow => DateTime.UtcNow;
}
