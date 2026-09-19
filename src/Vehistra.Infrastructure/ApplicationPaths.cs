using System.Runtime.InteropServices;

namespace Vehistra.Infrastructure;

/// <summary>
/// Zentrale Verzeichnisse der Anwendung. Veraenderliche Daten liegen niemals
/// unterhalb von "Program Files".
/// </summary>
public static class ApplicationPaths
{
    public const string CompanyFolder = "LSP Virtual Services";
    public const string ProductFolder = "Vehistra";

    /// <summary>Maschinenweite Konfiguration: C:\ProgramData\LSP Virtual Services\Vehistra.</summary>
    public static string MachineData => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        CompanyFolder, ProductFolder);

    /// <summary>Benutzerbezogene Einstellungen: %LocalAppData%\LSP Virtual Services\Vehistra.</summary>
    public static string UserData => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        CompanyFolder, ProductFolder);

    /// <summary>Logverzeichnis unterhalb der maschinenweiten Daten.</summary>
    public static string Logs => Path.Combine(MachineData, "Logs");

    /// <summary>Zwischenspeicher fuer erzeugte Berichte.</summary>
    public static string ReportCache => Path.Combine(UserData, "Berichte");

    /// <summary>Sicherung der Programmdateien vor einem Update.</summary>
    public static string UpdateBackup => Path.Combine(MachineData, "UpdateBackup");

    /// <summary>Datei der maschinenweiten Serverkonfiguration.</summary>
    public static string ConnectionFile => Path.Combine(MachineData, "connection.config");

    /// <summary>Installationsverzeichnis der Anwendung.</summary>
    public static string InstallDirectory =>
        Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;

    /// <summary>Legt alle benoetigten Verzeichnisse an, sofern sie fehlen.</summary>
    public static void EnsureCreated()
    {
        Directory.CreateDirectory(MachineData);
        Directory.CreateDirectory(UserData);
        Directory.CreateDirectory(Logs);
        Directory.CreateDirectory(ReportCache);
    }

    /// <summary>Kurzbeschreibung des Betriebssystems fuer Diagnose und Support.</summary>
    public static string DescribeOperatingSystem() =>
        $"{RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})";
}
