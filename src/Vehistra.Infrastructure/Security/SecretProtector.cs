using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace Vehistra.Infrastructure.Security;

/// <summary>
/// Verschluesselt vertrauliche Werte (SQL-Passwort, Reservierungs-PIN) mit der Windows-DPAPI.
/// Der Schluessel verlaesst dabei niemals den jeweiligen Computer.
/// </summary>
public static class SecretProtector
{
    /// <summary>Zusaetzlicher Kontext (Entropie) der DPAPI-Verschluesselung.</summary>
    private static readonly byte[] Entropy =
        Encoding.UTF8.GetBytes("LSP Virtual Services - Vehistra");

    /// <summary>Gibt an, ob auf diesem System DPAPI zur Verfuegung steht.</summary>
    public static bool IsSupported => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    /// <summary>
    /// Verschluesselt einen Wert fuer den lokalen Computer (LocalMachine-Scope), damit auch
    /// Dienste und andere Benutzer desselben Arbeitsplatzes den Wert lesen koennen.
    /// </summary>
    public static string? Protect(string? plainText)
    {
        if (string.IsNullOrEmpty(plainText))
        {
            return null;
        }

        if (!IsSupported)
        {
            throw new PlatformNotSupportedException(
                "Das Verschluesseln von Zugangsdaten setzt Windows voraus (DPAPI).");
        }

        var data = Encoding.UTF8.GetBytes(plainText);
        var encrypted = ProtectedData.Protect(data, Entropy, DataProtectionScope.LocalMachine);
        CryptographicOperations.ZeroMemory(data);

        return Convert.ToBase64String(encrypted);
    }

    /// <summary>Entschluesselt einen zuvor mit <see cref="Protect"/> gesicherten Wert.</summary>
    public static string? Unprotect(string? protectedValue)
    {
        if (string.IsNullOrEmpty(protectedValue))
        {
            return null;
        }

        if (!IsSupported)
        {
            throw new PlatformNotSupportedException(
                "Das Entschluesseln von Zugangsdaten setzt Windows voraus (DPAPI).");
        }

        var data = Convert.FromBase64String(protectedValue);
        var decrypted = ProtectedData.Unprotect(data, Entropy, DataProtectionScope.LocalMachine);

        return Encoding.UTF8.GetString(decrypted);
    }

    /// <summary>Versucht zu entschluesseln und meldet Fehler, statt eine Ausnahme auszuloesen.</summary>
    public static bool TryUnprotect(string? protectedValue, out string? plainText)
    {
        plainText = null;

        try
        {
            plainText = Unprotect(protectedValue);
            return plainText is not null;
        }
        catch (Exception exception) when (exception is CryptographicException or FormatException
                                              or PlatformNotSupportedException)
        {
            return false;
        }
    }
}
