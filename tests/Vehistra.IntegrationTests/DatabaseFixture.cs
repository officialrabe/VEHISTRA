using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Vehistra.Application;
using Vehistra.Application.Abstractions;
using Vehistra.Application.Dtos;
using Vehistra.Domain.Security;
using Vehistra.Infrastructure;
using Vehistra.Infrastructure.Persistence;
using Vehistra.Infrastructure.Persistence.Seeding;
using Vehistra.Infrastructure.Services;

namespace Vehistra.IntegrationTests;

/// <summary>
/// Stellt fuer jeden Test eine eigene, frisch aufgebaute Datenbank bereit.
/// Verwendet wird SQLite im Arbeitsspeicher; das Modell ist dasselbe wie im Betrieb
/// gegen Microsoft SQL Server.
/// </summary>
public sealed class TestDatabase : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;

    private TestDatabase(SqliteConnection connection, ServiceProvider provider)
    {
        _connection = connection;
        _provider = provider;
    }

    public IServiceProvider Services => _provider;

    public VehistraDbContext Db => _provider.GetRequiredService<VehistraDbContext>();

    public TestClock Clock => (TestClock)_provider.GetRequiredService<IClock>();

    public ICurrentUserService CurrentUser => _provider.GetRequiredService<ICurrentUserService>();

    public T Service<T>() where T : notnull => _provider.GetRequiredService<T>();

    /// <summary>Baut eine leere Datenbank mit Rollen, Berechtigungen und Stammdaten auf.</summary>
    public static async Task<TestDatabase> CreateAsync(
        bool seedSystemData = true,
        string applicationVersion = "1.0.0-test")
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var services = new ServiceCollection();

        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        services.AddVehistraApplication();
        services.AddVehistraInfrastructureServices();

        services.AddSingleton<IClock>(new TestClock(new DateTime(2026, 3, 14, 9, 0, 0)));
        services.AddSingleton<IPasswordHasher, Vehistra.Infrastructure.Security.Pbkdf2PasswordHasher>();
        services.AddSingleton(new ApplicationVersionProvider(applicationVersion));
        services.AddSingleton<IDocumentStorage, InMemoryDocumentStorage>();
        services.AddSingleton<IReportService, Vehistra.Reporting.QuestPdfReportService>();
        services.AddSingleton<IConnectionSettingsStore, TestConnectionSettingsStore>();

        services.AddDbContext<VehistraDbContext>((sp, options) =>
        {
            options.UseSqlite(connection);
            options.AddInterceptors(
                sp.GetRequiredService<Vehistra.Infrastructure.Persistence.Interceptors.AuditSaveChangesInterceptor>(),
                sp.GetRequiredService<Vehistra.Infrastructure.Persistence.Interceptors.ConcurrencyTokenInterceptor>());
        }, ServiceLifetime.Scoped, ServiceLifetime.Scoped);

        services.AddScoped<IVehistraDbContext>(sp => sp.GetRequiredService<VehistraDbContext>());

        var provider = services.BuildServiceProvider();
        var database = new TestDatabase(connection, provider);

        await database.Db.Database.EnsureCreatedAsync();

        if (seedSystemData)
        {
            await provider.GetRequiredService<DatabaseSeeder>().SeedSystemDataAsync();
        }

        return database;
    }

    /// <summary>Meldet einen Benutzer mit allen Rechten an (fuer Tests der Fachlogik).</summary>
    public void SignInAsAdministrator(int userId = 1) =>
        CurrentUser.SetUser(new CurrentUser(
            userId,
            "testadmin",
            "Test",
            "Administrator",
            [RoleNames.Administrator],
            Permissions.All.Select(p => p.Name).ToList(),
            false));

    /// <summary>Meldet einen Benutzer mit genau den angegebenen Rechten an.</summary>
    public void SignInWith(params string[] permissions) =>
        CurrentUser.SetUser(new CurrentUser(
            99,
            "testuser",
            "Test",
            "Benutzer",
            [RoleNames.Mitarbeiter],
            permissions,
            false));

    public void SignOut() => CurrentUser.SetUser(null);

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        await _connection.DisposeAsync();
    }
}

/// <summary>Anhaltbare Uhr, damit Fristen und Sperrzeiten pruefbar sind.</summary>
public sealed class TestClock : IClock
{
    public TestClock(DateTime now) => Now = now;

    public DateTime Now { get; private set; }

    public DateTime Today => Now.Date;

    public DateTime UtcNow => Now.ToUniversalTime();

    public void Advance(TimeSpan amount) => Now = Now.Add(amount);

    public void SetTo(DateTime moment) => Now = moment;
}

/// <summary>Dokumentenablage im Arbeitsspeicher - beruehrt kein Dateisystem.</summary>
public sealed class InMemoryDocumentStorage : IDocumentStorage
{
    private readonly Dictionary<string, byte[]> _files = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, byte[]> Files => _files;

    public string RootPath => @"\\TEST\Dokumente";

    public bool IsConfigured => true;

    public async Task<DocumentStorageResult> StoreAsync(
        Stream content,
        string originalFileName,
        string targetFolder,
        CancellationToken cancellationToken = default)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);

        var data = buffer.ToArray();
        var path = $"{targetFolder}/{Guid.NewGuid():N}_{originalFileName}".Replace('\\', '/');

        _files[path] = data;

        var hash = System.Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(data)).ToLowerInvariant();
        var contentType = Path.GetExtension(originalFileName).ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            _ => "application/octet-stream"
        };

        return new DocumentStorageResult(path, data.LongLength, hash, contentType);
    }

    public Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default) =>
        _files.TryGetValue(Key(relativePath), out var data)
            ? Task.FromResult<Stream>(new MemoryStream(data))
            : throw new FileNotFoundException(relativePath);

    public string GetFullPath(string relativePath) => Path.Combine(RootPath, relativePath);

    public bool Exists(string relativePath) => _files.ContainsKey(Key(relativePath));

    public Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        _files.Remove(Key(relativePath));
        return Task.CompletedTask;
    }

    public Task<StorageProbeResult> ProbeAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new StorageProbeResult(true, true, true, "Testablage im Arbeitsspeicher."));

    private static string Key(string relativePath) => relativePath.Replace('\\', '/');
}

/// <summary>
/// Verbindungseinstellungen im Arbeitsspeicher. Es wird weder eine Datei geschrieben
/// noch DPAPI verwendet - beides waere ausserhalb von Windows nicht moeglich.
/// </summary>
public sealed class TestConnectionSettingsStore : IConnectionSettingsStore
{
    private ServerConnectionSettings? _settings;

    public string ConfigFilePath => @"\\TEST\connection.config";

    public bool IsConfigured => _settings is not null;

    public ServerConnectionSettings? Load() => _settings;

    public void Save(ServerConnectionSettings settings) => _settings = settings;

    public string BuildConnectionString(ServerConnectionSettings settings) =>
        $"Server={settings.Server};Database={settings.Database};Trusted_Connection=True";

    public void ExportClientConfiguration(ServerConnectionSettings settings, string targetPath)
    {
        // Passwoerter werden ausdruecklich nicht exportiert.
        var copy = settings.Clone();
        copy.ProtectedSqlPassword = null;

        File.WriteAllText(targetPath, System.Text.Json.JsonSerializer.Serialize(copy));
    }

    public ServerConnectionSettings ImportClientConfiguration(string sourcePath) =>
        System.Text.Json.JsonSerializer.Deserialize<ServerConnectionSettings>(File.ReadAllText(sourcePath))
        ?? throw new InvalidOperationException("Die Konfigurationsdatei konnte nicht gelesen werden.");
}

/// <summary>
/// Datenbankdatei in einem eigenen Verzeichnis - fuer Tests, die die Datei
/// wirklich brauchen, etwa die Sicherung des Solo-Platzes.
/// </summary>
public sealed class TemporaryDatabaseFile : IDisposable
{
    private readonly string _directory;

    public TemporaryDatabaseFile()
    {
        _directory = Path.Combine(Path.GetTempPath(), "vehistra-db-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);

        Path_ = Path.Combine(_directory, "Vehistra.db");
        BackupDirectory = Path.Combine(_directory, "Backups");
        Directory.CreateDirectory(BackupDirectory);
    }

    public string Path_ { get; }

    public string BackupDirectory { get; }

    public ServerConnectionSettings Settings => new()
    {
        Provider = DatabaseProvider.Sqlite,
        DatabaseFile = Path_,
        BackupPath = BackupDirectory
    };

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        try
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }
        catch (IOException)
        {
            // Aufraeumen ist Kür, nicht Pflicht.
        }
    }
}
