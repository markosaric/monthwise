using Monthwise.Domain.Cards;
using Monthwise.Domain.Common;

namespace Monthwise.Domain.Expenses;

/// <summary>
/// What a bill or an expense can be paid with (FR-5, FR-7, FR-8). Accounts and cards are both
/// <see cref="Card"/>s: a Credit or Charge card is a credit card, so what it pays becomes a line of its
/// card payment; any other card or account (Debit cards included, also when they allow installments) is
/// an account the amount is paid from directly.
/// </summary>
public static class PaidFromRules
{
    /// <summary>Whether what is paid with <paramref name="card"/> goes into its card payment (FR-6).</summary>
    public static bool IsCreditCard(Card card)
    {
        ArgumentNullException.ThrowIfNull(card);
        return card.Type is CardType.Credit or CardType.Charge;
    }

    /// <summary>
    /// Checks <paramref name="card"/> as what a bill or expense dated <paramref name="date"/> is paid
    /// with. <paramref name="primaryOnly"/> allows only primary accounts (cash withdrawals, FR-8).
    /// <paramref name="unchanged"/> keeps a choice that was valid when saved, so an entry can still be
    /// edited after its card or account was blocked or expired. Returns null when it can be used.
    /// </summary>
    public static DomainError? Check(Card? card, DateOnly date, DateOnly today, bool primaryOnly, bool unchanged)
    {
        if (card is null)
        {
            return ExpenseErrors.PaidFromUnknown;
        }
        if (IsCreditCard(card))
        {
            if (primaryOnly)
            {
                return ExpenseErrors.CashWithdrawalNotPrimary;
            }
            if (!card.HasBillingDetails)
            {
                return ExpenseErrors.PaidFromNeedsBillingDetails;
            }
            if (card.ValidFrom is { } validFrom && date < validFrom.FirstDay)
            {
                return ExpenseErrors.PaidFromNotValidYet;
            }
        }
        else if (primaryOnly && !card.IsPrimary && !unchanged)
        {
            return ExpenseErrors.CashWithdrawalNotPrimary;
        }
        return unchanged || card.CanBeUsedOn(today) ? null : ExpenseErrors.PaidFromUnusable;
    }
}
