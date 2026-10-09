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
            replaces is null ? null : new CardReference(replaces.Id, replaces.Name),
            Billing(card),
            card.NeedsBillingDetails);
    }

    private CardBillingResponse? Billing(Card card)
    {
        if (card.BillingFor(YearMonth.FromDate(today).Next()) is not { } billing)
        {
            return null;
        }
        var account = _cards.TryGetValue(billing.PayingAccountId, out var payingAccount)
            ? new CardReference(payingAccount.Id, payingAccount.Name)
            : new CardReference(billing.PayingAccountId, "Unknown");
        var terms = card.BillingTerms
            .Select(t => new BillingTermsResponse(
                t.FromMonth == CardBillingTerms.Beginning ? null : t.FromMonth.ToString(), t.StatementDay, t.PaymentDay))
            .ToList();
        return new CardBillingResponse(
            billing.StatementDay, billing.PaymentDay, account, billing.PaysFromAccountDirectly, billing.MaxInstallments, terms);
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
        request.AllowsInstallments,
        ToBilling(request));

    /// <summary>
    /// The billing details, or null when none were entered. Missing values become invalid ones, so the
    /// card rules report each missing field.
    /// </summary>
    private static CardBillingDetails? ToBilling(CardRequest request) =>
        request.StatementDay is null && request.PaymentDay is null && request.PayingAccountId is null && request.MaxInstallments is null
            ? null
            : new CardBillingDetails(
                request.StatementDay ?? 0,
                request.PaymentDay ?? 0,
                request.PayingAccountId ?? Guid.Empty,
                request.PaysFromAccountDirectly,
                request.MaxInstallments ?? 0);
}
