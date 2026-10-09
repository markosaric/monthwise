using Microsoft.EntityFrameworkCore;
using Monthwise.Application.CardPurchases;
using Monthwise.Application.Cards;
using Monthwise.Application.Loans;
using Monthwise.Domain.Cards;
using Monthwise.Infrastructure.Persistence.Configurations;
using Monthwise.Infrastructure.Security;

namespace Monthwise.Infrastructure.Persistence.Repositories;

internal sealed class CardRepository(MonthwiseDbContext db, ISensitiveDataCipher cipher) : ICardRepository
{
    public async Task<IReadOnlyList<Card>> ListAsync(CancellationToken cancellationToken) =>
        await db.Cards.ToListAsync(cancellationToken);

    public Task<Card?> GetAsync(Guid id, bool includeSensitiveData, CancellationToken cancellationToken)
    {
        IQueryable<Card> query = includeSensitiveData ? db.Cards.Include(c => c.SensitiveData) : db.Cards;
        return query.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public Task<bool> NameInUseAsync(string name, Guid? exceptCardId, CancellationToken cancellationToken)
    {
        var lower = name.ToLowerInvariant();
#pragma warning disable CA1304, CA1311, CA1862 // ToLower() is translated to SQL lower(), matching the unique index
        return db.Cards.AnyAsync(c => c.Name.ToLower() == lower && c.Id != exceptCardId, cancellationToken);
#pragma warning restore CA1304, CA1311, CA1862
    }

    public Task<bool> CardNumberInUseAsync(CardNumber cardNumber, Guid? exceptCardId, CancellationToken cancellationToken) =>
        HashInUseAsync(
            CardSensitiveDataConfiguration.CardNumberHash,
            MonthwiseDbContext.HashCardNumber(cipher, cardNumber),
            exceptCardId,
            cancellationToken);

    public Task<bool> IbanInUseAsync(Iban iban, Guid? exceptCardId, CancellationToken cancellationToken) =>
        HashInUseAsync(
            CardSensitiveDataConfiguration.IbanHash,
            MonthwiseDbContext.HashIban(cipher, iban),
            exceptCardId,
            cancellationToken);

    public void Add(Card card) => db.Cards.Add(card);

    // Compares hashes in the database, so no stored number is decrypted.
    private Task<bool> HashInUseAsync(string hashProperty, string hash, Guid? exceptCardId, CancellationToken cancellationToken) =>
        db.Cards.AnyAsync(
            c => c.ArchivedOn == null && c.Id != exceptCardId && EF.Property<string?>(c.SensitiveData, hashProperty) == hash,
            cancellationToken);
}

internal sealed class CardListRepository<TEntry>(MonthwiseDbContext db) : ICardListRepository<TEntry>
    where TEntry : CardListEntry
{
    public async Task<IReadOnlyList<TEntry>> ListAsync(CancellationToken cancellationToken) =>
        await db.Set<TEntry>().ToListAsync(cancellationToken);

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken) =>
        db.Set<TEntry>().AnyAsync(e => e.Id == id, cancellationToken);

    public Task<bool> NameInUseAsync(string name, CancellationToken cancellationToken)
    {
        var lower = name.ToLowerInvariant();
#pragma warning disable CA1304, CA1311, CA1862 // ToLower() is translated to SQL lower(), matching the unique index
        return db.Set<TEntry>().AnyAsync(e => e.Name.ToLower() == lower, cancellationToken);
#pragma warning restore CA1304, CA1311, CA1862
    }

    public void Add(TEntry entry) => db.Set<TEntry>().Add(entry);
}

/// <summary>
/// Ongoing transactions are standing order loans with expected payments (loans FR-3), cards with
/// installments left, and the paying accounts of connected cards with expected card payments
/// (installments, card settings Appendix 2).
/// </summary>
internal sealed class CardActivityQuery(LoanLedger loans, CardPaymentLedger cardPayments) : ICardActivityQuery
{
    public async Task<IReadOnlySet<Guid>> CardsWithOngoingTransactionsAsync(CancellationToken cancellationToken)
    {
        var standingOrders = await loans.AccountsWithOngoingStandingOrdersAsync(cancellationToken);
        var cards = await cardPayments.CardsWithOngoingTransactionsAsync(cancellationToken);
        return standingOrders.Concat(cards).ToHashSet();
    }
}
