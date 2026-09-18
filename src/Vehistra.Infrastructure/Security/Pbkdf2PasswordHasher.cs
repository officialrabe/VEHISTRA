using System.Security.Cryptography;
using Vehistra.Application.Abstractions;

namespace Vehistra.Infrastructure.Security;

/// <summary>
/// Passwort-Hashing mit PBKDF2 (HMAC-SHA256), zufaelligem Salt und hoher Iterationszahl.
/// Format: <c>PBKDF2$SHA256$&lt;Iterationen&gt;$&lt;Salt-Base64&gt;$&lt;Hash-Base64&gt;</c>.
/// </summary>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const int CurrentIterations = 210_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const string Prefix = "PBKDF2";
    private const string Algorithm = "SHA256";

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, CurrentIterations, HashAlgorithmName.SHA256, HashSize);

        return string.Join('$', Prefix, Algorithm, CurrentIterations.ToString(),
            Convert.ToBase64String(salt), Convert.ToBase64String(hash));
    }

    public bool Verify(string password, string hash)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(hash))
        {
            return false;
        }

        if (!TryParse(hash, out var iterations, out var salt, out var expected))
        {
            return false;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    public bool NeedsRehash(string hash) =>
        !TryParse(hash, out var iterations, out _, out _) || iterations < CurrentIterations;

    private static bool TryParse(string value, out int iterations, out byte[] salt, out byte[] hash)
    {
        iterations = 0;
        salt = [];
        hash = [];

        var parts = value.Split('$');
        if (parts.Length != 5 || parts[0] != Prefix || parts[1] != Algorithm)
        {
            return false;
        }

        if (!int.TryParse(parts[2], out iterations) || iterations <= 0)
        {
            return false;
        }

        try
        {
            salt = Convert.FromBase64String(parts[3]);
            hash = Convert.FromBase64String(parts[4]);
        }
        catch (FormatException)
        {
            return false;
        }

        return salt.Length > 0 && hash.Length > 0;
    }
}
