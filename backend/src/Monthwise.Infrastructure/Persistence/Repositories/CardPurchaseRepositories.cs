using Microsoft.EntityFrameworkCore;
using Monthwise.Application.CardPurchases;
using Monthwise.Domain.CardPurchases;
using Monthwise.Domain.Common;

namespace Monthwise.Infrastructure.Persistence.Repositories;

internal sealed class CardPurchaseRepository(MonthwiseDbContext db) : ICardPurchaseRepository
{
    public async Task<IReadOnlyList<CardPurchase>> ListAsync(Guid? cardId, CancellationToken cancellationToken) =>
        await db.CardPurchases.Where(p => cardId == null || p.CardId == cardId).ToListAsync(cancellationToken);

    public Task<CardPurchase?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.CardPurchases.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public Task<bool> AnyForCardAsync(Guid cardId, CancellationToken cancellationToken) =>
        db.CardPurchases.AnyAsync(p => p.CardId == cardId, cancellationToken);

    public void Add(CardPurchase purchase) => db.CardPurchases.Add(purchase);

    public void Remove(CardPurchase purchase) => db.CardPurchases.Remove(purchase);
}

internal sealed class CardPaymentRepository(MonthwiseDbContext db) : ICardPaymentRepository
{
    public async Task<IReadOnlyList<CardPayment>> ListAsync(Guid? cardId, CancellationToken cancellationToken) =>
        await db.CardPayments.Where(p => cardId == null || p.CardId == cardId).ToListAsync(cancellationToken);

    public async Task<CardPayment?> GetAsync(Guid cardId, YearMonth month, CancellationToken cancellationToken) =>
        // A card payment stored in this request is not in the database yet.
        db.CardPayments.Local.FirstOrDefault(p => p.CardId == cardId && p.Month == month)
        ?? await db.CardPayments.FirstOrDefaultAsync(p => p.CardId == cardId && p.Month == month, cancellationToken);

    public void Add(CardPayment payment) => db.CardPayments.Add(payment);

    public void Remove(CardPayment payment) => db.CardPayments.Remove(payment);
}
