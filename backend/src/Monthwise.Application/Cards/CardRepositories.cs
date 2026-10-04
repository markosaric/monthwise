using Monthwise.Domain.Cards;

namespace Monthwise.Application.Cards;

public interface ICardRepository
{
    /// <summary>All cards, including archived ones, without their sensitive data.</summary>
    Task<IReadOnlyList<Card>> ListAsync(CancellationToken cancellationToken);

    /// <summary>One card; <paramref name="includeSensitiveData"/> also loads and decrypts its numbers.</summary>
    Task<Card?> GetAsync(Guid id, bool includeSensitiveData, CancellationToken cancellationToken);

    /// <summary>Case-insensitive; archived cards count too, because names stay unique.</summary>
    Task<bool> NameInUseAsync(string name, Guid? exceptCardId, CancellationToken cancellationToken);

    /// <summary>Only cards that are not archived count.</summary>
    Task<bool> CardNumberInUseAsync(CardNumber cardNumber, Guid? exceptCardId, CancellationToken cancellationToken);

    /// <summary>Only cards that are not archived count.</summary>
    Task<bool> IbanInUseAsync(Iban iban, Guid? exceptCardId, CancellationToken cancellationToken);

    void Add(Card card);
}

/// <summary>Issuer or bank list.</summary>
public interface ICardListRepository<TEntry> where TEntry : CardListEntry
{
    Task<IReadOnlyList<TEntry>> ListAsync(CancellationToken cancellationToken);
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> NameInUseAsync(string name, CancellationToken cancellationToken);
    void Add(TEntry entry);
}

/// <summary>
/// Which cards still have ongoing transactions (e.g. installment plans with installments left).
/// No feature creates such transactions yet; the installments feature fills this in.
/// </summary>
public interface ICardActivityQuery
{
    Task<IReadOnlySet<Guid>> CardsWithOngoingTransactionsAsync(CancellationToken cancellationToken);
}
