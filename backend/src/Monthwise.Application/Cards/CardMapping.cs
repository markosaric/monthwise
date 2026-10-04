using Monthwise.Domain.Cards;
using Monthwise.Domain.Common;

namespace Monthwise.Application.Cards;

/// <summary>Builds <see cref="CardResponse"/>s; needs the issuer and bank lists and all cards (for replacement links).</summary>
internal sealed class CardMapping(
    IReadOnlyList<CardIssuer> issuers,
    IReadOnlyList<Bank> banks,
    IReadOnlyList<Card> allCards,
    DateOnly today)
{
    private readonly Dictionary<Guid, CardIssuer> _issuers = issuers.ToDictionary(i => i.Id);
    private readonly Dictionary<Guid, Bank> _banks = banks.ToDictionary(b => b.Id);
    private readonly Dictionary<Guid, Card> _cards = allCards.ToDictionary(c => c.Id);

    public static async Task<CardMapping> LoadAsync(
        ICardListRepository<CardIssuer> issuers,
        ICardListRepository<Bank> banks,
        ICardRepository cards,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var issuerList = await issuers.ListAsync(cancellationToken);
        var bankList = await banks.ListAsync(cancellationToken);
        var cardList = await cards.ListAsync(cancellationToken);
        return new CardMapping(issuerList, bankList, cardList, today);
    }

    public IReadOnlyList<Card> AllCards => _cards.Values.ToList();

    public CardResponse ToResponse(Card card)
    {
        var replaces = _cards.Values.FirstOrDefault(c => c.ReplacedByCardId == card.Id);
        return new CardResponse(
            card.Id,
            card.Name,
            ToResponse(_issuers[card.IssuerId]),
            ToResponse(_banks[card.BankId]),
            card.CardNumberMasked,
            card.IbanMasked,
            card.ValidFrom?.ToString(),
            card.ValidThrough.ToString(),
            card.Type,
            card.IsPrimary,
            card.DebtLimit?.Cents,
            card.AllowsInstallments,
            card.StatusOn(today),
            card.CanBeUsedOn(today),
            card.BlockedOn,
            card.BlockReason,
            card.ExpiredOn,
            card.ExpiryReason,
            card.ArchivedOn,
            card.ReplacedByCardId is { } id && _cards.TryGetValue(id, out var replacement) ? new CardReference(id, replacement.Name) : null,
            replaces is null ? null : new CardReference(replaces.Id, replaces.Name));
    }

    /// <summary>Primary accounts first, then by name.</summary>
    public IReadOnlyList<CardResponse> ToSortedResponses(IEnumerable<Card> cards) =>
        cards
            .OrderByDescending(c => c.IsPrimary)
            .ThenBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(ToResponse)
            .ToList();

    public static CardListEntryResponse ToResponse(CardListEntry entry) => new(entry.Id, entry.Name, entry.IsPredefined);

    public static CardDetails ToDetails(CardRequest request) => new(
        request.Name ?? string.Empty,
        request.IssuerId,
        request.BankId,
        string.IsNullOrEmpty(request.ValidFrom) ? null : YearMonth.Parse(request.ValidFrom),
        YearMonth.Parse(request.ValidThrough),
        request.Type,
        request.IsPrimary,
        request.DebtLimitCents is { } cents ? Money.FromCents(cents) : null,
        request.AllowsInstallments);
}
