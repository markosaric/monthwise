using Monthwise.Domain.Common;
using Monthwise.Domain.Expenses;

namespace Monthwise.Application.Expenses;

public interface IExpenseCategoryRepository
{
    Task<IReadOnlyList<ExpenseCategory>> ListAsync(CancellationToken cancellationToken);
    Task<ExpenseCategory?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Case-insensitive.</summary>
    Task<bool> NameInUseAsync(string name, Guid? exceptId, CancellationToken cancellationToken);

    /// <summary>Whether a card purchase, bill, recurring bill or expense uses the category.</summary>
    Task<bool> IsUsedAsync(Guid id, CancellationToken cancellationToken);

    void Add(ExpenseCategory category);
    void Remove(ExpenseCategory category);
}

public interface IRecurringBillRepository
{
    Task<IReadOnlyList<RecurringBill>> ListAsync(CancellationToken cancellationToken);
    Task<RecurringBill?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Case-insensitive, among recurring bills that are not stopped.</summary>
    Task<bool> NameInUseAsync(string name, Guid? exceptId, CancellationToken cancellationToken);

    void Add(RecurringBill recurringBill);
    void Remove(RecurringBill recurringBill);
}

public interface IBillRepository
{
    /// <summary>All bills.</summary>
    Task<IReadOnlyList<Bill>> ListAsync(CancellationToken cancellationToken);

    /// <summary>The bills added to <paramref name="month"/>.</summary>
    Task<IReadOnlyList<Bill>> ListByMonthAsync(YearMonth month, CancellationToken cancellationToken);

    Task<Bill?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Whether any bill was added from the recurring bill.</summary>
    Task<bool> AnyForRecurringBillAsync(Guid recurringBillId, CancellationToken cancellationToken);

    void Add(Bill bill);
    void Remove(Bill bill);
}

public interface IExpenseRepository
{
    /// <summary>All expenses, cash withdrawals included.</summary>
    Task<IReadOnlyList<Expense>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Expenses dated between the dates (inclusive).</summary>
    Task<IReadOnlyList<Expense>> ListBetweenAsync(DateOnly first, DateOnly last, CancellationToken cancellationToken);

    Task<Expense?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>The expense (not a cash withdrawal) recorded last, for the *Paid from* prefill (E4).</summary>
    Task<Expense?> LatestAsync(CancellationToken cancellationToken);

    void Add(Expense expense);
    void Remove(Expense expense);
}
