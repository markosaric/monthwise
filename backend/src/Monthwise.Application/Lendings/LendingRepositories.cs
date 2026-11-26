using Monthwise.Domain.Lendings;

namespace Monthwise.Application.Lendings;

public interface ILendingRepository
{
    /// <summary>All lendings with their repayments.</summary>
    Task<IReadOnlyList<Lending>> ListAsync(CancellationToken cancellationToken);

    /// <summary>One lending with its repayments.</summary>
    Task<Lending?> GetAsync(Guid id, CancellationToken cancellationToken);

    void Add(Lending lending);
    void Remove(Lending lending);
}
