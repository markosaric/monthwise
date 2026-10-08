using Monthwise.Domain.Common;
using Monthwise.Domain.Loans;

namespace Monthwise.Domain.Tests.Loans;

public class DueDateRuleTests
{
    [Theory]
    [InlineData(PaymentMethod.StandingOrder)]
    [InlineData(PaymentMethod.Bill)]
    public void Payment_day_31_is_the_last_day_of_a_shorter_month(PaymentMethod method)
    {
        Assert.Equal(new DateOnly(2026, 4, 30), DueDateRule.DueDate(new YearMonth(2026, 4), 31, method));
        // 28 February 2026 is a Saturday; the following Monday is in March, so the bill stays.
        Assert.Equal(new DateOnly(2026, 2, 28), DueDateRule.DueDate(new YearMonth(2026, 2), 31, method));
    }

    [Theory]
    [InlineData(10)]
    [InlineData(11)]
    public void A_bill_on_a_weekend_is_due_the_following_Monday(int day)
    {
        Assert.Equal(new DateOnly(2026, 10, 12), DueDateRule.DueDate(new YearMonth(2026, 10), day, PaymentMethod.Bill));
    }

    [Theory]
    [InlineData(30)]
    [InlineData(31)]
    public void A_bill_on_the_last_weekend_of_the_month_stays_on_its_day(int day)
    {
        Assert.Equal(new DateOnly(2026, 5, day), DueDateRule.DueDate(new YearMonth(2026, 5), day, PaymentMethod.Bill));
    }

    [Fact]
    public void A_standing_order_on_a_weekend_keeps_its_day()
    {
        Assert.Equal(new DateOnly(2026, 10, 10), DueDateRule.DueDate(new YearMonth(2026, 10), 10, PaymentMethod.StandingOrder));
    }

    [Fact]
    public void The_bill_date_is_the_first_day_of_the_month()
    {
        Assert.Equal(new DateOnly(2026, 10, 1), DueDateRule.BillDate(new YearMonth(2026, 10)));
    }
}
