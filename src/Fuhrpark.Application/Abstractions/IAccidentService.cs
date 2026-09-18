using Fuhrpark.Application.Dtos;
using Fuhrpark.Domain.Entities;

namespace Fuhrpark.Application.Abstractions;

/// <summary>Unfallmanagement.</summary>
public interface IAccidentService
{
    Task<IReadOnlyList<AccidentListItem>> GetListAsync(
        int? vehicleId = null,
        bool onlyOpen = false,
        CancellationToken cancellationToken = default);

    Task<AccidentReport?> GetAsync(int id, CancellationToken cancellationToken = default);

    Task<int> CreateAsync(AccidentReport accident, CancellationToken cancellationToken = default);

    Task UpdateAsync(AccidentReport accident, CancellationToken cancellationToken = default);

    Task CloseAsync(int accidentId, CancellationToken cancellationToken = default);

    Task<int> AddParticipantAsync(AccidentParticipant participant, CancellationToken cancellationToken = default);

    Task RemoveParticipantAsync(int participantId, CancellationToken cancellationToken = default);

    Task<int> AddWitnessAsync(AccidentWitness witness, CancellationToken cancellationToken = default);

    Task RemoveWitnessAsync(int witnessId, CancellationToken cancellationToken = default);

    /// <summary>Legt zu einem Unfall automatisch einen Schadensdatensatz an.</summary>
    Task<int> CreateDamageFromAccidentAsync(int accidentId, string description, CancellationToken cancellationToken = default);
}
