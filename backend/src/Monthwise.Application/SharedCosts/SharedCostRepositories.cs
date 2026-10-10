using Monthwise.Domain.SharedCosts;

namespace Monthwise.Application.SharedCosts;

public interface IParticipantRepository
{
    /// <summary>All participants, archived ones included.</summary>
    Task<IReadOnlyList<Participant>> ListAsync(CancellationToken cancellationToken);

    Task<Participant?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Case-insensitive.</summary>
    Task<bool> NameInUseAsync(string name, Guid? exceptId, CancellationToken cancellationToken);

    /// <summary>Whether a recurring bill, bill, loan or loan payment is shared with the participant.</summary>
    Task<bool> IsUsedAsync(Guid id, CancellationToken cancellationToken);

    void Add(Participant participant);
    void Remove(Participant participant);
}
