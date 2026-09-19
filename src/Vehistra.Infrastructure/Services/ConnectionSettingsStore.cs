using System.Text.Json;
using Vehistra.Application.Abstractions;
using Vehistra.Infrastructure.Security;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Vehistra.Infrastructure.Services;

/// <summary>
/// Speichert die Serververbindung maschinenweit unter ProgramData.
/// Das SQL-Passwort wird ausschliesslich DPAPI-verschluesselt abgelegt -
/// niemals im Klartext und niemals in appsettings.json oder im Repository.
/// </summary>
public sealed class ConnectionSettingsStore : IConnectionSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ILogger<ConnectionSettingsStore> _logger;

    public ConnectionSettingsStore(ILogger<ConnectionSettingsStore> logger)
    {
        _logger = logger;
        ConfigFilePath = ApplicationPaths.ConnectionFile;
    }

    public string ConfigFilePath { get; }

    public bool IsConfigured => File.Exists(ConfigFilePath) && Load() is { Server.Length: > 0 };

    public ServerConnectionSettings? Load()
    {
        if (!File.Exists(ConfigFilePath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(ConfigFilePath);
            return JsonSerializer.Deserialize<ServerConnectionSettings>(json, JsonOptions);
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            _logger.LogError(exception, "Die Serverkonfiguration konnte nicht gelesen werden: {Path}", ConfigFilePath);
            return null;
        }
    }

    public void Save(ServerConnectionSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigFilePath)!);

        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(ConfigFilePath, json);

        _logger.LogInformation("Serverkonfiguration gespeichert: {Server}/{Database}", settings.Server, settings.Database);
    }

    public string BuildConnectionString(ServerConnectionSettings settings)
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = settings.Server,
            InitialCatalog = settings.Database,
            ConnectTimeout = settings.ConnectTimeoutSeconds,
            CommandTimeout = settings.CommandTimeoutSeconds,
            Encrypt = settings.Encrypt,
            TrustServerCertificate = settings.TrustServerCertificate,
            ApplicationName = "Vehistra",
            MultipleActiveResultSets = false,
            Pooling = true
        };

        if (settings.UseWindowsAuthentication)
        {
            builder.IntegratedSecurity = true;
        }
        else
        {
            builder.IntegratedSecurity = false;
            builder.UserID = settings.SqlUserName ?? string.Empty;

            if (SecretProtector.TryUnprotect(settings.ProtectedSqlPassword, out var password) && password is not null)
            {
                builder.Password = password;
            }
        }

        return builder.ConnectionString;
    }

    public void ExportClientConfiguration(ServerConnectionSettings settings, string targetPath)
    {
        // Die Firmenkonfiguration enthaelt bewusst keine Zugangsdaten.
        var export = new ClientConfigurationFile
        {
            Server = settings.Server,
            Database = settings.Database,
            UseWindowsAuthentication = settings.UseWindowsAuthentication,
            SqlUserName = settings.UseWindowsAuthentication ? null : settings.SqlUserName,
            DocumentsPath = settings.DocumentsPath,
            UpdatePath = settings.UpdatePath,
            CreatedAt = DateTime.Now,
            CreatedBy = Environment.UserName,
            CreatedOnComputer = Environment.MachineName
        };

        var directory = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(targetPath, JsonSerializer.Serialize(export, JsonOptions));
        _logger.LogInformation("Firmenkonfiguration exportiert nach {Path}", targetPath);
    }

    public ServerConnectionSettings ImportClientConfiguration(string sourcePath)
    {
        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException($"Die Konfigurationsdatei '{sourcePath}' wurde nicht gefunden.", sourcePath);
        }

        var json = File.ReadAllText(sourcePath);
        var import = JsonSerializer.Deserialize<ClientConfigurationFile>(json, JsonOptions)
            ?? throw new InvalidDataException(
                "Die Konfigurationsdatei konnte nicht gelesen werden. Bitte eine gueltige .fmcfg-Datei auswaehlen.");

        if (string.IsNullOrWhiteSpace(import.Server))
        {
            throw new InvalidDataException("Die Konfigurationsdatei enthaelt keinen Servernamen.");
        }

        return new ServerConnectionSettings
        {
            Server = import.Server,
            Database = string.IsNullOrWhiteSpace(import.Database) ? "VehistraDB" : import.Database,
            UseWindowsAuthentication = import.UseWindowsAuthentication,
            SqlUserName = import.SqlUserName,
            DocumentsPath = import.DocumentsPath,
            UpdatePath = import.UpdatePath,
            ConfiguredAt = DateTime.Now,
            ConfiguredBy = Environment.UserName
        };
    }

    /// <summary>Aufbau der Datei Vehistra-Firmenkonfiguration.fmcfg.</summary>
    private sealed class ClientConfigurationFile
    {
        public string FileType { get; set; } = "VehistraClientConfiguration";

        public int FormatVersion { get; set; } = 1;

        public string Server { get; set; } = string.Empty;

        public string Database { get; set; } = "VehistraDB";

        public bool UseWindowsAuthentication { get; set; } = true;

        public string? SqlUserName { get; set; }

        public string? DocumentsPath { get; set; }

        public string? UpdatePath { get; set; }

        public DateTime CreatedAt { get; set; }

        public string? CreatedBy { get; set; }

        public string? CreatedOnComputer { get; set; }
    }
}
