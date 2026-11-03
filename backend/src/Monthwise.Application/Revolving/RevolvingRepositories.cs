using Monthwise.Domain.Common;
using Monthwise.Domain.Revolving;

namespace Monthwise.Application.Revolving;

public interface IDebtEntryRepository
{
    /// <summary>The debt entries of one card, or of all cards when <paramref name="cardId"/> is null.</summary>
    Task<IReadOnlyList<DebtEntry>> ListAsync(Guid? cardId, CancellationToken cancellationToken);

    Task<DebtEntry?> GetAsync(Guid id, CancellationToken cancellationToken);

    void Add(DebtEntry entry);

    void Remove(DebtEntry entry);
}

public interface IExtraPaymentRepository
{
    /// <summary>The extra payments to one card, or to all cards when <paramref name="cardId"/> is null.</summary>
    Task<IReadOnlyList<ExtraPayment>> ListAsync(Guid? cardId, CancellationToken cancellationToken);

    Task<ExtraPayment?> GetAsync(Guid id, CancellationToken cancellationToken);

    void Add(ExtraPayment payment);

    void Remove(ExtraPayment payment);
}

public interface IRevolvingBillRepository
{
    /// <summary>The stored monthly bills of one card, or of all cards when <paramref name="cardId"/> is null.</summary>
    Task<IReadOnlyList<RevolvingBill>> ListAsync(Guid? cardId, CancellationToken cancellationToken);

    Task<RevolvingBill?> GetAsync(Guid cardId, YearMonth month, CancellationToken cancellationToken);

    void Add(RevolvingBill bill);

    void Remove(RevolvingBill bill);
}
