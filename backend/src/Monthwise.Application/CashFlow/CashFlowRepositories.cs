using Monthwise.Domain.CashFlow;

namespace Monthwise.Application.CashFlow;

/// <summary>Identifies one occurrence of a recurring input.</summary>
public readonly record struct OccurrenceKey(Guid RecurringInputId, DateOnly OccurrenceDate);

public interface ICashFlowInputRepository
{
    Task<CashFlowInput?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<CashFlowInput?> GetOccurrenceAsync(OccurrenceKey key, CancellationToken cancellationToken);

    /// <summary>Inputs whose month date (received date, or expected date while not received) is between <paramref name="first"/> and <paramref name="last"/>, inclusive.</summary>
    Task<IReadOnlyList<CashFlowInput>> ListByMonthDateAsync(DateOnly first, DateOnly last, CancellationToken cancellationToken);

    /// <summary>Inputs received on or before <paramref name="last"/>.</summary>
    Task<IReadOnlyList<CashFlowInput>> ListReceivedUpToAsync(DateOnly last, CancellationToken cancellationToken);

    /// <summary>Inputs not received yet with an expected date on or before <paramref name="last"/>.</summary>
    Task<IReadOnlyList<CashFlowInput>> ListExpectedUpToAsync(DateOnly last, CancellationToken cancellationToken);

    /// <summary>The stored occurrences of one recurring input.</summary>
    Task<IReadOnlyList<CashFlowInput>> ListOccurrencesAsync(Guid recurringInputId, CancellationToken cancellationToken);

    /// <summary>Keys of all stored occurrences, so they are not calculated a second time.</summary>
    Task<IReadOnlySet<OccurrenceKey>> ListOccurrenceKeysAsync(CancellationToken cancellationToken);

    void Add(CashFlowInput input);
    void Remove(CashFlowInput input);
}

public interface IRecurringInputRepository
{
    Task<IReadOnlyList<RecurringInput>> ListAsync(CancellationToken cancellationToken);
    Task<RecurringInput?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlySet<OccurrenceKey>> ListSkippedAsync(CancellationToken cancellationToken);
    void Add(RecurringInput recurringInput);
    void AddSkip(SkippedOccurrence skipped);
}

public interface IPayerRepository
{
    /// <summary>All payers, including removed ones.</summary>
    Task<IReadOnlyList<Payer>> ListAsync(CancellationToken cancellationToken);
    Task<Payer?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Case-insensitive; removed payers count too.</summary>
    Task<Payer?> FindByNameAsync(string name, CancellationToken cancellationToken);
    void Add(Payer payer);
}

public interface IInputCategoryRepository
{
    Task<IReadOnlyList<InputCategory>> ListAsync(CancellationToken cancellationToken);
    Task<InputCategory?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> NameInUseAsync(string name, Guid? exceptId, CancellationToken cancellationToken);

    /// <summary>Whether any input or recurring input uses the category.</summary>
    Task<bool> IsUsedAsync(Guid id, CancellationToken cancellationToken);
    void Add(InputCategory category);
    void Remove(InputCategory category);
}

public interface IBalanceEntryRepository
{
    /// <summary>Entries of one account, or of all accounts when <paramref name="accountId"/> is null.</summary>
    Task<IReadOnlyList<BalanceEntry>> ListAsync(Guid? accountId, CancellationToken cancellationToken);
    Task<BalanceEntry?> GetAsync(Guid id, CancellationToken cancellationToken);
    void Add(BalanceEntry entry);
    void Remove(BalanceEntry entry);
}
