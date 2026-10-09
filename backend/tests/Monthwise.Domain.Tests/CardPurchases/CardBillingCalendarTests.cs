using Monthwise.Domain.Cards;
using Monthwise.Domain.Common;
using static Monthwise.Domain.Tests.CardPurchases.CardPurchaseTestData;

namespace Monthwise.Domain.Tests.CardPurchases;

public class CardBillingCalendarTests
{
    private static readonly YearMonth October = new(2026, 10);

    [Fact]
    public void Statement_and_payment_dates_are_in_the_same_month()
    {
        var calendar = CreditCard().BillingCalendar()!;

        Assert.Equal(new DateOnly(2026, 10, 5), calendar.StatementDate(October));
        Assert.Equal(new DateOnly(2026, 10, 20), calendar.PaymentDate(October));
    }

    [Fact]
    public void A_payment_date_on_a_weekend_moves_to_monday()
    {
        var calendar = CreditCard().BillingCalendar()!;

        // 20 September and 20 December 2026 are Sundays.
        Assert.Equal(new DateOnly(2026, 9, 21), calendar.PaymentDate(new YearMonth(2026, 9)));
        Assert.Equal(new DateOnly(2026, 12, 21), calendar.PaymentDate(new YearMonth(2026, 12)));
    }

    [Fact]
    public void A_payment_date_on_the_last_weekend_of_the_month_stays_in_its_month()
    {
        var calendar = CreditCard(Billing(statementDay: 25, paymentDay: 31)).BillingCalendar()!;

        // 31 October 2026 is a Saturday; the Monday after is in November.
        Assert.Equal(new DateOnly(2026, 10, 31), calendar.PaymentDate(October));
    }

    [Fact]
    public void Days_29_to_31_fall_on_the_last_day_of_a_shorter_month()
    {
        var calendar = CreditCard(Billing(statementDay: 30, paymentDay: 31)).BillingCalendar()!;

        Assert.Equal(new DateOnly(2026, 2, 28), calendar.StatementDate(new YearMonth(2026, 2)));
        Assert.Equal(new DateOnly(2026, 2, 28), calendar.PaymentDate(new YearMonth(2026, 2)));
        Assert.Equal(new DateOnly(2028, 2, 29), calendar.StatementDate(new YearMonth(2028, 2)));
        Assert.Equal(new DateOnly(2026, 9, 30), calendar.PaymentDate(new YearMonth(2026, 9)));
    }

    [Fact]
    public void The_billing_period_ends_on_the_statement_date()
    {
        var calendar = CreditCard().BillingCalendar()!;

        Assert.Equal((new DateOnly(2026, 9, 6), new DateOnly(2026, 10, 5)), calendar.Period(October));
        Assert.Equal(October, calendar.PaymentMonthOf(new DateOnly(2026, 10, 5)));
        Assert.Equal(new YearMonth(2026, 11), calendar.PaymentMonthOf(new DateOnly(2026, 10, 6)));
        Assert.Equal(October, calendar.PaymentMonthOf(new DateOnly(2026, 9, 6)));
    }

    [Fact]
    public void Changed_days_apply_from_next_month_without_a_gap_or_an_overlap()
    {
        var card = CreditCard();
        var details = new CardDetails(
            card.Name, card.IssuerId, card.BankId, null, card.ValidThrough, card.Type, false, null, false,
            Billing(statementDay: 10, paymentDay: 25));

        Assert.True(card.Update(details, null, null, new DateOnly(2026, 10, 20), Now).IsSuccess);
        var calendar = card.BillingCalendar()!;

        Assert.Equal(new DateOnly(2026, 10, 5), calendar.StatementDate(October));
        Assert.Equal(new DateOnly(2026, 10, 20), calendar.PaymentDate(October));
        Assert.Equal((new DateOnly(2026, 10, 6), new DateOnly(2026, 11, 10)), calendar.Period(new YearMonth(2026, 11)));
        Assert.Equal(new DateOnly(2026, 11, 25), calendar.PaymentDate(new YearMonth(2026, 11)));
        Assert.Equal(new YearMonth(2026, 11), calendar.PaymentMonthOf(new DateOnly(2026, 11, 8)));
        Assert.Equal(2, card.BillingTerms.Count);
    }

    [Fact]
    public void Changing_the_days_back_in_the_same_month_keeps_one_version()
    {
        var card = CreditCard();
        var today = new DateOnly(2026, 10, 20);
        CardDetails With(CardBillingDetails billing) =>
            new(card.Name, card.IssuerId, card.BankId, null, card.ValidThrough, card.Type, false, null, false, billing);

        card.Update(With(Billing(statementDay: 10, paymentDay: 25)), null, null, today, Now);
        card.Update(With(Billing()), null, null, today, Now);

        Assert.Single(card.BillingTerms);
        Assert.Equal(5, card.BillingFor(new YearMonth(2026, 11))!.StatementDay);
    }
}
