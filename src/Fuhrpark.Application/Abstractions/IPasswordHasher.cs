namespace Fuhrpark.Application.Abstractions;

/// <summary>Sicheres Hashen und Pruefen von Passwoertern.</summary>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string hash);

    /// <summary>Gibt an, ob der Hash mit veralteten Parametern erzeugt wurde und erneuert werden sollte.</summary>
    bool NeedsRehash(string hash);
}
