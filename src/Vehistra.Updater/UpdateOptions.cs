namespace Vehistra.Updater;

/// <summary>Aufrufparameter der Updatekomponente.</summary>
public sealed class UpdateOptions
{
    /// <summary>Pfad zum Update-Installer (Vehistra-Update-x.x.x.exe).</summary>
    public string InstallerPath { get; init; } = string.Empty;

    public string TargetVersion { get; init; } = string.Empty;

    /// <summary>Installationsverzeichnis der Anwendung.</summary>
    public string ApplicationDirectory { get; init; } = string.Empty;

    /// <summary>Prozesskennung der Hauptanwendung, auf deren Ende gewartet wird.</summary>
    public int? WaitForProcessId { get; init; }

    public bool CreateBackupBeforeMigration { get; init; } = true;

    public string? UserName { get; init; }

    /// <summary>Wird nach erfolgreichem Update automatisch gestartet.</summary>
    public string MainExecutable => Path.Combine(ApplicationDirectory, "Vehistra.exe");

    /// <summary>Liest die Befehlszeilenargumente.</summary>
    public static UpdateOptions Parse(string[] args)
    {
        string? installer = null;
        string? version = null;
        string? appDirectory = null;
        string? user = null;
        int? processId = null;
        var backup = true;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--installer" when i + 1 < args.Length:
                    installer = args[++i].Trim('"');
                    break;

                case "--version" when i + 1 < args.Length:
                    version = args[++i].Trim('"');
                    break;

                case "--appdir" when i + 1 < args.Length:
                    appDirectory = args[++i].Trim('"');
                    break;

                case "--pid" when i + 1 < args.Length:
                    processId = int.TryParse(args[++i], out var parsed) ? parsed : null;
                    break;

                case "--user" when i + 1 < args.Length:
                    user = args[++i].Trim('"');
                    break;

                case "--backup":
                    backup = true;
                    break;

                case "--no-backup":
                    backup = false;
                    break;
            }
        }

        return new UpdateOptions
        {
            InstallerPath = installer ?? string.Empty,
            TargetVersion = version ?? string.Empty,
            ApplicationDirectory = string.IsNullOrWhiteSpace(appDirectory)
                ? Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory
                : appDirectory,
            WaitForProcessId = processId,
            CreateBackupBeforeMigration = backup,
            UserName = user
        };
    }
}
