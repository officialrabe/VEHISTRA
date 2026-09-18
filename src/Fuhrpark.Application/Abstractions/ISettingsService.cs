using Fuhrpark.Domain.Common;
using Fuhrpark.Domain.Entities;

namespace Fuhrpark.Application.Abstractions;

/// <summary>Liest und schreibt Systemeinstellungen (mit Zwischenspeicher).</summary>
public interface ISettingsService
{
    Task<string?> GetAsync(string key, CancellationToken cancellationToken = default);

    Task<string> GetOrDefaultAsync(string key, string defaultValue, CancellationToken cancellationToken = default);

    Task<int> GetIntAsync(string key, int defaultValue, CancellationToken cancellationToken = default);

    Task<bool> GetBoolAsync(string key, bool defaultValue, CancellationToken cancellationToken = default);

    Task SetAsync(string key, string? value, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SystemSetting>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<CompanyProfile> GetCompanyProfileAsync(CancellationToken cancellationToken = default);

    Task<DueDateThresholds> GetInspectionThresholdsAsync(CancellationToken cancellationToken = default);

    void InvalidateCache();
}

/// <summary>Unternehmensangaben fuer Berichte und Oberflaeche (konfigurierbar, nicht hart codiert).</summary>
public sealed class CompanyProfile
{
    public string Name { get; init; } = string.Empty;

    public string? Street { get; init; }

    public string? PostalCode { get; init; }

    public string? City { get; init; }

    public string? Phone { get; init; }

    public string? Email { get; init; }

    public string? LogoPath { get; init; }

    public string AddressLine =>
        string.Join(" · ", new[]
        {
            Street,
            string.Join(" ", new[] { PostalCode, City }.Where(s => !string.IsNullOrWhiteSpace(s))),
            Phone,
            Email
        }.Where(s => !string.IsNullOrWhiteSpace(s)));
}
