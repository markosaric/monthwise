using Microsoft.EntityFrameworkCore;
using Monthwise.Application.Revolving;
using Monthwise.Domain.Common;
using Monthwise.Domain.Revolving;

namespace Monthwise.Infrastructure.Persistence.Repositories;

internal sealed class DebtEntryRepository(MonthwiseDbContext db) : IDebtEntryRepository
{
    public async Task<IReadOnlyList<DebtEntry>> ListAsync(Guid? cardId, CancellationToken cancellationToken) =>
        await db.DebtEntries.Where(e => cardId == null || e.CardId == cardId).ToListAsync(cancellationToken);

    public Task<DebtEntry?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.DebtEntries.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public void Add(DebtEntry entry) => db.DebtEntries.Add(entry);

    public void Remove(DebtEntry entry) => db.DebtEntries.Remove(entry);
}

internal sealed class ExtraPaymentRepository(MonthwiseDbContext db) : IExtraPaymentRepository
{
    public async Task<IReadOnlyList<ExtraPayment>> ListAsync(Guid? cardId, CancellationToken cancellationToken) =>
        await db.ExtraPayments.Where(p => cardId == null || p.CardId == cardId).ToListAsync(cancellationToken);

    public Task<ExtraPayment?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.ExtraPayments.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public void Add(ExtraPayment payment) => db.ExtraPayments.Add(payment);

    public void Remove(ExtraPayment payment) => db.ExtraPayments.Remove(payment);
}

internal sealed class RevolvingBillRepository(MonthwiseDbContext db) : IRevolvingBillRepository
{
    public async Task<IReadOnlyList<RevolvingBill>> ListAsync(Guid? cardId, CancellationToken cancellationToken) =>
        await db.RevolvingBills.Where(b => cardId == null || b.CardId == cardId).ToListAsync(cancellationToken);

    public async Task<RevolvingBill?> GetAsync(Guid cardId, YearMonth month, CancellationToken cancellationToken) =>
        // A bill stored in this request is not in the database yet.
        db.RevolvingBills.Local.FirstOrDefault(b => b.CardId == cardId && b.Month == month)
        ?? await db.RevolvingBills.FirstOrDefaultAsync(b => b.CardId == cardId && b.Month == month, cancellationToken);

    public void Add(RevolvingBill bill) => db.RevolvingBills.Add(bill);

    public void Remove(RevolvingBill bill) => db.RevolvingBills.Remove(bill);
}
