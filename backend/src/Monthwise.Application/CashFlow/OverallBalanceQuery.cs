using Monthwise.Application.Cards;
using Monthwise.Application.Common;
using Monthwise.Domain.CashFlow;
using Monthwise.Domain.Common;

namespace Monthwise.Application.CashFlow;

/// <summary>
/// The overall balance of a month (overall balance tile FR-2 to FR-5): the balance of all primary accounts
/// at the end of the month once everything is received and paid. A past month shows the actual balance at
/// its end; the current and future months show the expected one.
/// </summary>
public sealed class OverallBalanceQuery(ICardRepository cards, AccountBalanceQuery balances, TimeProvider timeProvider)
{
    public async Task<OverallBalanceResponse> ForMonthAsync(YearMonth month, CancellationToken cancellationToken)
    {
        var today = timeProvider.Today();
        var monthEnd = month.LastDay;
        var isExpected = monthEnd >= today;
        var primary = (await cards.ListAsync(cancellationToken)).Where(c => c.IsPrimary).ToList();
        if (primary.Count == 0)
        {
            return new OverallBalanceResponse(month.ToString(), isExpected, null, 0, 0, false);
        }

        var calculators = await balances.CalculatorsAsync(isExpected ? today : monthEnd, cancellationToken);
        var expected = isExpected ? await balances.ExpectedMovementsAsync(monthEnd, cancellationToken) : null;
        var total = Money.Zero;
        var missingStartingBalance = false;
        foreach (var account in primary)
        {
            var balance = calculators.TryGetValue(account.Id, out var calculator) ? calculator : new AccountBalance([], []);
            missingStartingBalance |= !balance.HasCheckpoint;
            total += expected is null
                ? balance.BalanceOn(monthEnd)
                : balance.ExpectedBalance(today, expected.ForAccount(account.Id));
        }

        // Outputs without an account count as paid from the only primary account; with several primary
        // accounts it is not known which one pays them, so they are only counted (FR-2, FR-3).
        var withoutAccount = expected?.OutputsWithoutAccount ?? [];
        var withoutAccountCount = 0;
        if (primary.Count == 1)
        {
            total -= Money.Sum(withoutAccount);
        }
        else
        {
            withoutAccountCount = withoutAccount.Count;
        }

        return new OverallBalanceResponse(
            month.ToString(),
            isExpected,
            total.Cents,
            primary.Count,
            withoutAccountCount,
            missingStartingBalance);
    }
}
