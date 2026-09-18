namespace Fuhrpark.Application.Abstractions;

/// <summary>Systemdiagnose fuer Hilfe- und Supportbereich.</summary>
public interface IDiagnosticsService
{
    Task<DiagnosticsReport> RunAsync(CancellationToken cancellationToken = default);

    /// <summary>Erzeugt ein Supportpaket (Logs + Diagnose) ohne personenbezogene Daten.</summary>
    Task<string> CreateSupportPackageAsync(string targetDirectory, CancellationToken cancellationToken = default);

    string FormatForClipboard(DiagnosticsReport report);
}

public sealed record DiagnosticsCheck(string Name, bool IsSuccessful, string Result, string? Hint = null);

public sealed class DiagnosticsReport
{
    public DateTime CreatedAt { get; set; }

    public string ApplicationVersion { get; set; } = string.Empty;

    public string? DatabaseSchemaVersion { get; set; }

    public string? Server { get; set; }

    public string? SqlInstance { get; set; }

    public string? Database { get; set; }

    public string? DocumentsPath { get; set; }

    public string? UpdatePath { get; set; }

    public string? BackupPath { get; set; }

    public string ComputerName { get; set; } = string.Empty;

    public string OperatingSystem { get; set; } = string.Empty;

    public string RuntimeVersion { get; set; } = string.Empty;

    public string LogDirectory { get; set; } = string.Empty;

    public List<DiagnosticsCheck> Checks { get; } = [];

    public bool IsHealthy => Checks.All(c => c.IsSuccessful);
}
