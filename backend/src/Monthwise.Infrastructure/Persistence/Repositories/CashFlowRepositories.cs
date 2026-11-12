using Microsoft.EntityFrameworkCore;
using Monthwise.Application.CashFlow;
using Monthwise.Domain.CashFlow;
using Monthwise.Domain.SharedCosts;

namespace Monthwise.Infrastructure.Persistence.Repositories;

internal sealed class CashFlowInputRepository(MonthwiseDbContext db) : ICashFlowInputRepository
{
    public Task<CashFlowInput?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.CashFlowInputs.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

    public Task<CashFlowInput?> GetOccurrenceAsync(OccurrenceKey key, CancellationToken cancellationToken) =>
        db.CashFlowInputs.FirstOrDefaultAsync(
            i => i.RecurringInputId == key.RecurringInputId && i.OccurrenceDate == key.OccurrenceDate, cancellationToken);

    public async Task<IReadOnlyList<CashFlowInput>> ListByMonthDateAsync(DateOnly first, DateOnly last, CancellationToken cancellationToken) =>
        await db.Loaded.WhereAsync<CashFlowInput>(
            i => (i.ReceivedDate ?? i.ExpectedDate) >= first && (i.ReceivedDate ?? i.ExpectedDate) <= last, cancellationToken);

    public async Task<IReadOnlyList<CashFlowInput>> ListReceivedUpToAsync(DateOnly last, CancellationToken cancellationToken) =>
        await db.Loaded.WhereAsync<CashFlowInput>(i => i.ReceivedDate != null && i.ReceivedDate <= last, cancellationToken);

    public async Task<IReadOnlyList<CashFlowInput>> ListExpectedUpToAsync(DateOnly last, CancellationToken cancellationToken) =>
        await db.Loaded.WhereAsync<CashFlowInput>(i => i.ReceivedDate == null && i.ExpectedDate <= last, cancellationToken);

    public async Task<IReadOnlyList<CashFlowInput>> ListOccurrencesAsync(Guid recurringInputId, CancellationToken cancellationToken) =>
        await db.Loaded.WhereAsync<CashFlowInput>(i => i.RecurringInputId == recurringInputId, cancellationToken);

    public async Task<IReadOnlySet<OccurrenceKey>> ListOccurrenceKeysAsync(CancellationToken cancellationToken)
    {
        var occurrences = await db.Loaded.WhereAsync<CashFlowInput>(i => i.RecurringInputId != null, cancellationToken);
        return occurrences.Select(i => new OccurrenceKey(i.RecurringInputId!.Value, i.OccurrenceDate!.Value)).ToHashSet();
    }

    public async Task<IReadOnlyList<CashFlowInput>> ListContributionsAsync(CancellationToken cancellationToken) =>
        await db.Loaded.WhereAsync<CashFlowInput>(
            i => i.ContributionBillId != null || i.ContributionLoanId != null, cancellationToken);

    public Task<CashFlowInput?> GetContributionAsync(ContributionSource source, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.Kind == ContributionSourceKind.Bill
            ? db.CashFlowInputs.FirstOrDefaultAsync(i => i.ContributionBillId == source.BillId, cancellationToken)
            : db.CashFlowInputs.FirstOrDefaultAsync(
                i => i.ContributionLoanId == source.LoanId
                    && i.ContributionMonth == source.Month
                    && i.ContributionPaymentKind == source.PaymentKind,
                cancellationToken);
    }

    public void Add(CashFlowInput input) => db.CashFlowInputs.Add(input);

    public void Remove(CashFlowInput input) => db.CashFlowInputs.Remove(input);
}

internal sealed class RecurringInputRepository(MonthwiseDbContext db) : IRecurringInputRepository
{
    public async Task<IReadOnlyList<RecurringInput>> ListAsync(CancellationToken cancellationToken) =>
        await db.Loaded.AllAsync<RecurringInput>(cancellationToken);

    public Task<RecurringInput?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.RecurringInputs.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<IReadOnlySet<OccurrenceKey>> ListSkippedAsync(CancellationToken cancellationToken)
    {
        var skipped = await db.Loaded.AllAsync<SkippedOccurrence>(cancellationToken);
        return skipped.Select(s => new OccurrenceKey(s.RecurringInputId, s.OccurrenceDate)).ToHashSet();
    }

    public void Add(RecurringInput recurringInput) => db.RecurringInputs.Add(recurringInput);

    public async Task AddSkipAsync(SkippedOccurrence skipped, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(skipped);
        var tracked = db.SkippedOccurrences.Local.Any(
            s => s.RecurringInputId == skipped.RecurringInputId && s.OccurrenceDate == skipped.OccurrenceDate);
        if (!tracked && !await db.SkippedOccurrences.AnyAsync(
                s => s.RecurringInputId == skipped.RecurringInputId && s.OccurrenceDate == skipped.OccurrenceDate,
                cancellationToken))
        {
            db.SkippedOccurrences.Add(skipped);
        }
    }
}

internal sealed class PayerRepository(MonthwiseDbContext db) : IPayerRepository
{
    public async Task<IReadOnlyList<Payer>> ListAsync(CancellationToken cancellationToken) =>
        await db.Loaded.AllAsync<Payer>(cancellationToken);

    public Task<Payer?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.Payers.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<Payer?> FindByNameAsync(string name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        // A payer added in this request is not in the database yet; the database compares with the
        // name collation, and the in-memory check follows the same rule.
        return db.Payers.Local.FirstOrDefault(p => NameCollation.AreSame(p.Name, name))
            ?? await db.Payers.FirstOrDefaultAsync(p => p.Name == name, cancellationToken);
    }

    public void Add(Payer payer) => db.Payers.Add(payer);
}

internal sealed class InputCategoryRepository(MonthwiseDbContext db) : IInputCategoryRepository
{
    public async Task<IReadOnlyList<InputCategory>> ListAsync(CancellationToken cancellationToken) =>
        await db.Loaded.AllAsync<InputCategory>(cancellationToken);

    public Task<InputCategory?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.InputCategories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<bool> NameInUseAsync(string name, Guid? exceptId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        return db.InputCategories.AnyAsync(c => c.Name == name && c.Id != exceptId, cancellationToken);
    }

    public async Task<bool> IsUsedAsync(Guid id, CancellationToken cancellationToken) =>
        await db.CashFlowInputs.AnyAsync(i => i.CategoryId == id, cancellationToken)
        || await db.RecurringInputs.AnyAsync(r => r.CategoryId == id, cancellationToken);

    public void Add(InputCategory category) => db.InputCategories.Add(category);

    public void Remove(InputCategory category) => db.InputCategories.Remove(category);
}

internal sealed class BalanceEntryRepository(MonthwiseDbContext db) : IBalanceEntryRepository
{
    public async Task<IReadOnlyList<BalanceEntry>> ListAsync(Guid? accountId, CancellationToken cancellationToken) =>
        accountId is null
            ? await db.Loaded.AllAsync<BalanceEntry>(cancellationToken)
            : await db.Loaded.WhereAsync<BalanceEntry>(e => e.AccountId == accountId, cancellationToken);

    public Task<BalanceEntry?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.BalanceEntries.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public void Add(BalanceEntry entry) => db.BalanceEntries.Add(entry);

    public void Remove(BalanceEntry entry) => db.BalanceEntries.Remove(entry);
}
