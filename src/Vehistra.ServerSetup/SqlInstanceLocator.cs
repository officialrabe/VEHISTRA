using System.Runtime.Versioning;
using Microsoft.Win32;

namespace Vehistra.ServerSetup;

/// <summary>Ermittelt die auf dem Computer installierten SQL-Server-Instanzen.</summary>
[SupportedOSPlatform("windows")]
public static class SqlInstanceLocator
{
    /// <summary>
    /// Liest die lokalen Instanznamen aus der Windows-Registrierung.
    /// Eine Standardinstanz erscheint als reiner Computername.
    /// </summary>
    public static IReadOnlyList<SqlInstanceInfo> FindLocalInstances()
    {
        var results = new List<SqlInstanceInfo>();

        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                using var key = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Microsoft SQL Server\Instance Names\SQL");

                if (key is null)
                {
                    continue;
                }

                foreach (var name in key.GetValueNames())
                {
                    var display = string.Equals(name, "MSSQLSERVER", StringComparison.OrdinalIgnoreCase)
                        ? Environment.MachineName
                        : $"{Environment.MachineName}\\{name}";

                    if (results.All(r => !string.Equals(r.DataSource, display, StringComparison.OrdinalIgnoreCase)))
                    {
                        results.Add(new SqlInstanceInfo(display, name, ReadVersion(baseKey, key, name)));
                    }
                }
            }
            catch (Exception)
            {
                // Fehlende Leseberechtigung auf die Registrierung ist kein Abbruchgrund.
            }
        }

        return results;
    }

    /// <summary>Liest die Produktversion der Instanz, sofern verfuegbar.</summary>
    private static string? ReadVersion(RegistryKey baseKey, RegistryKey instanceKey, string instanceName)
    {
        try
        {
            if (instanceKey.GetValue(instanceName) is not string internalName)
            {
                return null;
            }

            using var setupKey = baseKey.OpenSubKey(
                $@"SOFTWARE\Microsoft\Microsoft SQL Server\{internalName}\Setup");

            return setupKey?.GetValue("Version") as string;
        }
        catch (Exception)
        {
            return null;
        }
    }
}

/// <summary>Gefundene SQL-Server-Instanz.</summary>
public sealed record SqlInstanceInfo(string DataSource, string InstanceName, string? Version)
{
    public string Display => Version is null ? DataSource : $"{DataSource}  (Version {Version})";
}
