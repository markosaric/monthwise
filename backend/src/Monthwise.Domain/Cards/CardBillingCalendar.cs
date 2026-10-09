using Monthwise.Domain.Common;
using Monthwise.Domain.Loans;

namespace Monthwise.Domain.Cards;

/// <summary>
/// The statement and payment dates of a card for every payment month (installments FR-1, FR-2).
/// The terms of a month are the newest ones that start in or before it. The billing period of payment
/// month <c>m</c> runs from the day after the statement date of <c>m − 1</c> up to and including the
/// statement date of <c>m</c>, so a change of days leaves no gap and no overlap.
/// </summary>
public sealed class CardBillingCalendar
{
    private readonly List<CardBillingTerms> _terms;

    public CardBillingCalendar(IEnumerable<CardBillingTerms> terms)
    {
        ArgumentNullException.ThrowIfNull(terms);
        _terms = terms.OrderBy(t => t.FromMonth).ToList();
        if (_terms.Count == 0)
        {
            throw new ArgumentException("A billing calendar needs at least one version of the terms.", nameof(terms));
        }
    }

    /// <summary>The terms that apply to payment month <paramref name="month"/>.</summary>
    public CardBillingTerms TermsFor(YearMonth month) => _terms.LastOrDefault(t => t.FromMonth <= month) ?? _terms[0];

    /// <summary>The statement date of <paramref name="month"/>: the statement day, clamped to the end of a shorter month.</summary>
    public DateOnly StatementDate(YearMonth month) => DayIn(month, TermsFor(month).StatementDay);

    /// <summary>
    /// The payment date of <paramref name="month"/>, in the same month as its statement. A Saturday or
    /// Sunday moves to the following Monday, unless that Monday is in the next month (FR-1).
    /// </summary>
    public DateOnly PaymentDate(YearMonth month) =>
        DueDateRule.DueDate(month, TermsFor(month).PaymentDay, PaymentMethod.Bill);

    /// <summary>The first and last day of the billing period of payment month <paramref name="month"/>.</summary>
    public (DateOnly From, DateOnly To) Period(YearMonth month) => (StatementDate(month.Previous()).AddDays(1), StatementDate(month));

    /// <summary>The payment month whose billing period contains <paramref name="date"/>.</summary>
    public YearMonth PaymentMonthOf(DateOnly date)
    {
        var month = YearMonth.FromDate(date);
        return date <= StatementDate(month) ? month : month.Next();
    }

    private static DateOnly DayIn(YearMonth month, int day) => new(month.Year, month.Month, Math.Min(day, month.LastDay.Day));
}
