using Monthwise.Domain.CardPurchases;
using Monthwise.Domain.Common;

namespace Monthwise.Application.CardPurchases;

public interface ICardPurchaseRepository
{
    /// <summary>Purchases of one card, or of all cards when <paramref name="cardId"/> is null, with their reversals.</summary>
    Task<IReadOnlyList<CardPurchase>> ListAsync(Guid? cardId, CancellationToken cancellationToken);

    /// <summary>One purchase with its reversals.</summary>
    Task<CardPurchase?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Whether any purchase was recorded on the card.</summary>
    Task<bool> AnyForCardAsync(Guid cardId, CancellationToken cancellationToken);

    void Add(CardPurchase purchase);
    void Remove(CardPurchase purchase);
}

public interface ICardPaymentRepository
{
    /// <summary>Stored card payments of one card, or of all cards when <paramref name="cardId"/> is null.</summary>
    Task<IReadOnlyList<CardPayment>> ListAsync(Guid? cardId, CancellationToken cancellationToken);

    Task<CardPayment?> GetAsync(Guid cardId, YearMonth month, CancellationToken cancellationToken);

    void Add(CardPayment payment);
    void Remove(CardPayment payment);
}

public interface IPurchaseCategoryRepository
{
    Task<IReadOnlyList<PurchaseCategory>> ListAsync(CancellationToken cancellationToken);
    Task<PurchaseCategory?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Case-insensitive.</summary>
    Task<bool> NameInUseAsync(string name, Guid? exceptId, CancellationToken cancellationToken);

    /// <summary>Whether any purchase uses the category.</summary>
    Task<bool> IsUsedAsync(Guid id, CancellationToken cancellationToken);
    void Add(PurchaseCategory category);
    void Remove(PurchaseCategory category);
}
