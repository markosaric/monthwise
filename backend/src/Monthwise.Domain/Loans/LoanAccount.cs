using Monthwise.Domain.Cards;

namespace Monthwise.Domain.Loans;

/// <summary>Whether the account of a standing order can still be charged (FR-3).</summary>
public static class LoanAccount
{
    /// <summary>
    /// The first day standing order payments are no longer charged automatically from
    /// <paramref name="account"/>, or null while it is a usable primary account. A blocked, expired or
    /// archived account stops on that day; an account that lost its primary mark stops today, so
    /// payments charged before stay paid.
    /// </summary>
    public static DateOnly? ChargesStopOn(Card? account, DateOnly today)
    {
        if (account is null)
        {
            return today;
        }
        if (account.CanBeUsedOn(today))
        {
            return account.IsPrimary ? null : today;
        }
        DateOnly?[] stops =
        [
            account.ArchivedOn,
            account.BlockedOn,
            account.ExpiredOn,
            today > account.ValidThrough.LastDay ? account.ValidThrough.LastDay.AddDays(1) : null,
            account.ValidFrom is { } from && today < from.FirstDay ? today : null,
        ];
        return stops.Where(d => d is not null).Min() ?? today;
    }

    /// <summary>Whether <paramref name="account"/> can be chosen for a standing order today (FR-3).</summary>
    public static bool CanBeChosen(Card account, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(account);
        return account.IsPrimary && account.CanBeUsedOn(today);
    }
}
