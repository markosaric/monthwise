using Microsoft.EntityFrameworkCore;
using Monthwise.Application.CashFlow;
using Monthwise.Domain.CashFlow;

namespace Monthwise.Infrastructure.Persistence.Repositories;

internal sealed class CashFlowInputRepository(MonthwiseDbContext db) : ICashFlowInputRepository
{
    public Task<CashFlowInput?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.CashFlowInputs.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

    public Task<CashFlowInput?> GetOccurrenceAsync(OccurrenceKey key, CancellationToken cancellationToken) =>
        db.CashFlowInputs.FirstOrDefaultAsync(
            i => i.RecurringInputId == key.RecurringInputId && i.OccurrenceDate == key.OccurrenceDate, cancellationToken);

    public async Task<IReadOnlyList<CashFlowInput>> ListByMonthDateAsync(DateOnly first, DateOnly last, CancellationToken cancellationToken) =>
        await db.CashFlowInputs
            .Where(i => (i.ReceivedDate ?? i.ExpectedDate) >= first && (i.ReceivedDate ?? i.ExpectedDate) <= last)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<CashFlowInput>> ListReceivedUpToAsync(DateOnly last, CancellationToken cancellationToken) =>
        await db.CashFlowInputs.Where(i => i.ReceivedDate != null && i.ReceivedDate <= last).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<CashFlowInput>> ListExpectedUpToAsync(DateOnly last, CancellationToken cancellationToken) =>
        await db.CashFlowInputs.Where(i => i.ReceivedDate == null && i.ExpectedDate <= last).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<CashFlowInput>> ListOccurrencesAsync(Guid recurringInputId, CancellationToken cancellationToken) =>
        await db.CashFlowInputs.Where(i => i.RecurringInputId == recurringInputId).ToListAsync(cancellationToken);

    public async Task<IReadOnlySet<OccurrenceKey>> ListOccurrenceKeysAsync(CancellationToken cancellationToken)
    {
        var keys = await db.CashFlowInputs
            .Where(i => i.RecurringInputId != null)
            .Select(i => new { RecurringInputId = i.RecurringInputId!.Value, OccurrenceDate = i.OccurrenceDate!.Value })
            .ToListAsync(cancellationToken);
        return keys.Select(k => new OccurrenceKey(k.RecurringInputId, k.OccurrenceDate)).ToHashSet();
    }

    public void Add(CashFlowInput input) => db.CashFlowInputs.Add(input);

    public void Remove(CashFlowInput input) => db.CashFlowInputs.Remove(input);
}

internal sealed class RecurringInputRepository(MonthwiseDbContext db) : IRecurringInputRepository
{
    public async Task<IReadOnlyList<RecurringInput>> ListAsync(CancellationToken cancellationToken) =>
        await db.RecurringInputs.ToListAsync(cancellationToken);

    public Task<RecurringInput?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.RecurringInputs.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<IReadOnlySet<OccurrenceKey>> ListSkippedAsync(CancellationToken cancellationToken)
    {
        var skipped = await db.SkippedOccurrences.ToListAsync(cancellationToken);
        return skipped.Select(s => new OccurrenceKey(s.RecurringInputId, s.OccurrenceDate)).ToHashSet();
    }

    public void Add(RecurringInput recurringInput) => db.RecurringInputs.Add(recurringInput);

    public void AddSkip(SkippedOccurrence skipped)
    {
        ArgumentNullException.ThrowIfNull(skipped);
        var tracked = db.SkippedOccurrences.Local.Any(
            s => s.RecurringInputId == skipped.RecurringInputId && s.OccurrenceDate == skipped.OccurrenceDate);
        if (!tracked && !db.SkippedOccurrences.Any(
                s => s.RecurringInputId == skipped.RecurringInputId && s.OccurrenceDate == skipped.OccurrenceDate))
        {
            db.SkippedOccurrences.Add(skipped);
        }
    }
}

internal sealed class PayerRepository(MonthwiseDbContext db) : IPayerRepository
{
    public async Task<IReadOnlyList<Payer>> ListAsync(CancellationToken cancellationToken) =>
        await db.Payers.ToListAsync(cancellationToken);

    public Task<Payer?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.Payers.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<Payer?> FindByNameAsync(string name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        var lower = name.ToLowerInvariant();
        // A payer added in this request is not in the database yet.
#pragma warning disable CA1304, CA1311, CA1862 // ToLower() is translated to SQL lower(), matching the unique index
        return db.Payers.Local.FirstOrDefault(p => p.Name.ToLowerInvariant() == lower)
            ?? await db.Payers.FirstOrDefaultAsync(p => p.Name.ToLower() == lower, cancellationToken);
#pragma warning restore CA1304, CA1311, CA1862
    }

    public void Add(Payer payer) => db.Payers.Add(payer);
}

internal sealed class InputCategoryRepository(MonthwiseDbContext db) : IInputCategoryRepository
{
    public async Task<IReadOnlyList<InputCategory>> ListAsync(CancellationToken cancellationToken) =>
        await db.InputCategories.ToListAsync(cancellationToken);

    public Task<InputCategory?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.InputCategories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<bool> NameInUseAsync(string name, Guid? exceptId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        var lower = name.ToLowerInvariant();
#pragma warning disable CA1304, CA1311, CA1862 // ToLower() is translated to SQL lower(), matching the unique index
        return db.InputCategories.AnyAsync(c => c.Name.ToLower() == lower && c.Id != exceptId, cancellationToken);
#pragma warning restore CA1304, CA1311, CA1862
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
        await db.BalanceEntries.Where(e => accountId == null || e.AccountId == accountId).ToListAsync(cancellationToken);

    public Task<BalanceEntry?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.BalanceEntries.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public void Add(BalanceEntry entry) => db.BalanceEntries.Add(entry);

    public void Remove(BalanceEntry entry) => db.BalanceEntries.Remove(entry);
}
