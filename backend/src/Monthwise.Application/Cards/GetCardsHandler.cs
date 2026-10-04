using Monthwise.Application.Common;
using Monthwise.Domain.Cards;
using Monthwise.Domain.Common;

namespace Monthwise.Application.Cards;

/// <summary>Settings list (FR-1), with the Default, Expired and Archived filters.</summary>
public sealed class GetCardsHandler(
    ICardRepository cards,
    ICardListRepository<CardIssuer> issuers,
    ICardListRepository<Bank> banks,
    ICardActivityQuery activity,
    TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<CardResponse>> HandleAsync(CardListFilter filter, CancellationToken cancellationToken)
    {
        var today = timeProvider.Today();
        var mapping = await CardMapping.LoadAsync(issuers, banks, cards, today, cancellationToken);
        var ongoing = await activity.CardsWithOngoingTransactionsAsync(cancellationToken);

        var selected = mapping.AllCards.Where(card => filter switch
        {
            CardListFilter.Expired => card.StatusOn(today) == CardStatus.Expired,
            CardListFilter.Archived => card.StatusOn(today) == CardStatus.Archived,
            _ => card.IsListedByDefault(today, ongoing.Contains(card.Id)),
        });
        return mapping.ToSortedResponses(selected);
    }
}

/// <summary>Card details (FR-3), with masked numbers only.</summary>
public sealed class GetCardHandler(
    ICardRepository cards,
    ICardListRepository<CardIssuer> issuers,
    ICardListRepository<Bank> banks,
    TimeProvider timeProvider)
{
    public async Task<Result<CardResponse>> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        var mapping = await CardMapping.LoadAsync(issuers, banks, cards, timeProvider.Today(), cancellationToken);
        var card = mapping.AllCards.FirstOrDefault(c => c.Id == id);
        return card is null
            ? Result.Failure<CardResponse>(CardErrors.NotFound)
            : Result.Success(mapping.ToResponse(card));
    }
}

/// <summary>Dashboard widget (FR-9, FR-10).</summary>
public sealed class GetDashboardCardsHandler(
    ICardRepository cards,
    ICardListRepository<CardIssuer> issuers,
    ICardListRepository<Bank> banks,
    ICardActivityQuery activity,
    TimeProvider timeProvider)
{
    public async Task<DashboardCardsResponse> HandleAsync(CancellationToken cancellationToken)
    {
        var today = timeProvider.Today();
        var mapping = await CardMapping.LoadAsync(issuers, banks, cards, today, cancellationToken);
        var ongoing = await activity.CardsWithOngoingTransactionsAsync(cancellationToken);

        var shown = mapping.AllCards.Where(card => card.IsShownOnDashboard(today, ongoing.Contains(card.Id)));
        return new DashboardCardsResponse(mapping.AllCards.Count > 0, mapping.ToSortedResponses(shown));
    }
}

/// <summary>Returns one full number when the user presses Reveal (FR-3).</summary>
public sealed class RevealCardNumberHandler(ICardRepository cards)
{
    public async Task<Result<RevealedNumberResponse>> HandleAsync(
        Guid id, SensitiveNumberKind kind, CancellationToken cancellationToken)
    {
        var card = await cards.GetAsync(id, includeSensitiveData: true, cancellationToken);
        if (card is null)
        {
            return Result.Failure<RevealedNumberResponse>(CardErrors.NotFound);
        }

        var value = kind switch
        {
            SensitiveNumberKind.CardNumber => card.SensitiveData.CardNumber is { } number ? GroupsOfFour(number.Digits) : null,
            _ => card.SensitiveData.Iban?.Formatted(),
        };
        return value is null
            ? Result.Failure<RevealedNumberResponse>(CardErrors.NotFound)
            : Result.Success(new RevealedNumberResponse(value));
    }

    private static string GroupsOfFour(string digits) =>
        string.Join(' ', digits.Chunk(4).Select(chunk => new string(chunk)));
}
