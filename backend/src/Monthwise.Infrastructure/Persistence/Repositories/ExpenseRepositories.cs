using Microsoft.EntityFrameworkCore;
using Monthwise.Application.Expenses;
using Monthwise.Domain.Common;
using Monthwise.Domain.Expenses;

namespace Monthwise.Infrastructure.Persistence.Repositories;

internal sealed class ExpenseCategoryRepository(MonthwiseDbContext db) : IExpenseCategoryRepository
{
    public async Task<IReadOnlyList<ExpenseCategory>> ListAsync(CancellationToken cancellationToken) =>
        await db.Loaded.AllAsync<ExpenseCategory>(cancellationToken);

    public Task<ExpenseCategory?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.ExpenseCategories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<bool> NameInUseAsync(string name, Guid? exceptId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        return db.ExpenseCategories.AnyAsync(c => c.Name == name && c.Id != exceptId, cancellationToken);
    }

    public async Task<bool> IsUsedAsync(Guid id, CancellationToken cancellationToken) =>
        await db.CardPurchases.AnyAsync(p => p.CategoryId == id, cancellationToken)
        || await db.Bills.AnyAsync(b => b.CategoryId == id, cancellationToken)
        || await db.RecurringBills.AnyAsync(r => r.CategoryId == id, cancellationToken)
        || await db.Expenses.AnyAsync(e => e.CategoryId == id, cancellationToken);

    public void Add(ExpenseCategory category) => db.ExpenseCategories.Add(category);

    public void Remove(ExpenseCategory category) => db.ExpenseCategories.Remove(category);
}

internal sealed class RecurringBillRepository(MonthwiseDbContext db) : IRecurringBillRepository
{
    public async Task<IReadOnlyList<RecurringBill>> ListAsync(CancellationToken cancellationToken) =>
        await db.Loaded.AllAsync<RecurringBill>(cancellationToken);

    public Task<RecurringBill?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.RecurringBills.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public Task<bool> NameInUseAsync(string name, Guid? exceptId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        return db.RecurringBills.AnyAsync(
            r => r.StoppedOn == null && r.Name == name && r.Id != exceptId, cancellationToken);
    }

    public void Add(RecurringBill recurringBill) => db.RecurringBills.Add(recurringBill);

    public void Remove(RecurringBill recurringBill) => db.RecurringBills.Remove(recurringBill);
}

internal sealed class BillRepository(MonthwiseDbContext db) : IBillRepository
{
    public async Task<IReadOnlyList<Bill>> ListAsync(CancellationToken cancellationToken) =>
        await db.Loaded.AllAsync<Bill>(cancellationToken);

    public async Task<IReadOnlyList<Bill>> ListByMonthAsync(YearMonth month, CancellationToken cancellationToken) =>
        await db.Loaded.WhereAsync<Bill>(b => b.Month == month, cancellationToken);

    public Task<Bill?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.Bills.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

    public Task<bool> AnyForRecurringBillAsync(Guid recurringBillId, CancellationToken cancellationToken) =>
        db.Bills.AnyAsync(b => b.RecurringBillId == recurringBillId, cancellationToken);

    public void Add(Bill bill) => db.Bills.Add(bill);

    public void Remove(Bill bill) => db.Bills.Remove(bill);
}

internal sealed class ExpenseRepository(MonthwiseDbContext db) : IExpenseRepository
{
    public async Task<IReadOnlyList<Expense>> ListAsync(CancellationToken cancellationToken) =>
        await db.Loaded.AllAsync<Expense>(cancellationToken);

    public async Task<IReadOnlyList<Expense>> ListBetweenAsync(DateOnly first, DateOnly last, CancellationToken cancellationToken) =>
        await db.Loaded.WhereAsync<Expense>(e => e.Date >= first && e.Date <= last, cancellationToken);

    public Task<Expense?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.Expenses.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public Task<Expense?> LatestAsync(CancellationToken cancellationToken) =>
        db.Expenses
            .Where(e => e.CategoryId != ExpenseCategorySeedData.CashWithdrawalId)
            .OrderByDescending(e => e.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public void Add(Expense expense) => db.Expenses.Add(expense);

    public void Remove(Expense expense) => db.Expenses.Remove(expense);
}
