using Monthwise.Domain.Common;
using Monthwise.Domain.Revolving;
using static Monthwise.Domain.Tests.Revolving.RevolvingTestData;

namespace Monthwise.Domain.Tests.Revolving;

public class RevolvingMathTests
{
    [Theory]
    [InlineData(100_000, 8_330)] // 8.33 % of 1,000.00 EUR
    [InlineData(10_000, 1_327)] // 8.33 EUR is less than 13.27 EUR
    [InlineData(1_000, 1_000)] // never more than the whole debt
    [InlineData(0, 0)]
    public void The_minimum_payment_is_the_greater_of_the_percentage_and_the_amount_but_not_more_than_the_debt(long debt, long minimum)
    {
        var terms = RevolvingCard().RevolvingTermVersions[0];

        Assert.Equal(minimum, RevolvingMath.MinimumPayment(Money.FromCents(debt), terms).Cents);
    }

    [Theory]
    [InlineData(1, 5_000, 1, 1)] // half a cent rounds away from zero
    [InlineData(-1, 5_000, 1, -1)]
    [InlineData(1, 4_999, 1, 0)]
    [InlineData(100_000, 1_275, 12, 1_063)] // 12.75 % a year on 1,000.00 EUR for one month is 10.625 EUR
    [InlineData(3_000_000, 1_275, 365, 1_048)] // 30 days of 1,000.00 EUR at 12.75 % a year is 10.479 EUR
    public void Percentages_are_rounded_to_the_cent_half_away_from_zero(long cents, long basisPoints, long divisor, long expected)
    {
        Assert.Equal(expected, RevolvingMath.PercentOf(Money.FromCents(cents), basisPoints, divisor).Cents);
    }

    [Theory]
    [InlineData(3_000_000, 1_275, 1_048)] // 30 days of 1,000.00 EUR at 12.75 % a year
    [InlineData(1_825, 1_000, 1)] // 1,825 cent-days at 10 % a year is half a cent, rounded up
    [InlineData(1_824, 1_000, 0)]
    [InlineData(0, 1_275, 0)] // no debt, no interest
    [InlineData(-100_000, 1_275, 0)]
    public void Daily_interest_is_the_sum_of_the_daily_debt_times_the_yearly_rate_divided_by_365(long centDays, int basisPoints, long expected)
    {
        var terms = RevolvingCard(Terms(interestBasisPoints: basisPoints)).RevolvingTermVersions[0];

        Assert.Equal(expected, RevolvingMath.DailyInterest(centDays, terms).Cents);
    }
}
