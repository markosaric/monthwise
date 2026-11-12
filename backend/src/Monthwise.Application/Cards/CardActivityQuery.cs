using Monthwise.Application.CardPurchases;
using Monthwise.Application.Loans;
using Monthwise.Application.Revolving;

namespace Monthwise.Application.Cards;

/// <summary>
/// Ongoing transactions are standing order loans with expected payments (loans FR-3), cards with
/// installments left, and the paying accounts of connected cards with expected card payments
/// (installments, card settings Appendix 2).
/// </summary>
internal sealed class CardActivityQuery(LoanLedger loans, CardPaymentLedger cardPayments, RevolvingLedger revolving) : ICardActivityQuery
{
    public async Task<IReadOnlySet<Guid>> CardsWithOngoingTransactionsAsync(CancellationToken cancellationToken)
    {
        var standingOrders = await loans.AccountsWithOngoingStandingOrdersAsync(cancellationToken);
        var cards = await cardPayments.CardsWithOngoingTransactionsAsync(cancellationToken);
        // A revolving card with debt keeps getting monthly bills (revolving FR-4).
        var withDebt = (await revolving.LoadAsync(cancellationToken)).CardsWithDebt;
        return standingOrders.Concat(cards).Concat(withDebt).ToHashSet();
    }
}
