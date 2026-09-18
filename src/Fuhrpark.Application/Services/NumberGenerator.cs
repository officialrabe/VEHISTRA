using Fuhrpark.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Fuhrpark.Application.Services;

/// <summary>
/// Erzeugt fortlaufende fachliche Nummern (Schaden, Unfall, Werkstattauftrag) im Format
/// PREFIX-JAHR-00001. Die Eindeutigkeit wird zusaetzlich durch einen Datenbankindex abgesichert.
/// </summary>
public sealed class NumberGenerator
{
    private readonly IFuhrparkDbContext _db;
    private readonly IClock _clock;

    public NumberGenerator(IFuhrparkDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<string> NextDamageNumberAsync(CancellationToken cancellationToken = default)
    {
        var year = _clock.Today.Year;
        var prefix = $"SCH-{year}-";

        var last = await _db.DamageReports
            .AsNoTracking()
            .Where(d => d.DamageNumber.StartsWith(prefix))
            .OrderByDescending(d => d.DamageNumber)
            .Select(d => d.DamageNumber)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return Build(prefix, last);
    }

    public async Task<string> NextAccidentNumberAsync(CancellationToken cancellationToken = default)
    {
        var year = _clock.Today.Year;
        var prefix = $"UNF-{year}-";

        var last = await _db.AccidentReports
            .AsNoTracking()
            .Where(a => a.AccidentNumber.StartsWith(prefix))
            .OrderByDescending(a => a.AccidentNumber)
            .Select(a => a.AccidentNumber)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return Build(prefix, last);
    }

    public async Task<string> NextWorkshopOrderNumberAsync(CancellationToken cancellationToken = default)
    {
        var year = _clock.Today.Year;
        var prefix = $"WS-{year}-";

        var last = await _db.WorkshopOrders
            .AsNoTracking()
            .Where(w => w.OrderNumber.StartsWith(prefix))
            .OrderByDescending(w => w.OrderNumber)
            .Select(w => w.OrderNumber)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return Build(prefix, last);
    }

    private static string Build(string prefix, string? lastNumber)
    {
        var next = 1;

        if (!string.IsNullOrWhiteSpace(lastNumber) && lastNumber.Length > prefix.Length)
        {
            var tail = lastNumber[prefix.Length..];
            if (int.TryParse(tail, out var parsed))
            {
                next = parsed + 1;
            }
        }

        return $"{prefix}{next:D5}";
    }
}
