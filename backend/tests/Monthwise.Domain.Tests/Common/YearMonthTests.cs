using Monthwise.Domain.Common;

namespace Monthwise.Domain.Tests.Common;

public class YearMonthTests
{
    [Theory]
    [InlineData(2026, 10, 2026, 11)]
    [InlineData(2026, 12, 2027, 1)]
    public void Next_moves_to_following_month(int year, int month, int expectedYear, int expectedMonth)
    {
        Assert.Equal(new YearMonth(expectedYear, expectedMonth), new YearMonth(year, month).Next());
    }

    [Fact]
    public void Previous_crosses_year_boundary()
    {
        Assert.Equal(new YearMonth(2025, 12), new YearMonth(2026, 1).Previous());
    }

    [Theory]
    [InlineData(2026, 1, 2026, 1, 0)]
    [InlineData(2026, 1, 2026, 4, 3)]
    [InlineData(2025, 11, 2026, 2, 3)]
    [InlineData(2026, 4, 2026, 1, -3)]
    public void MonthsUntil_counts_calendar_months(int y1, int m1, int y2, int m2, int expected)
    {
        Assert.Equal(expected, new YearMonth(y1, m1).MonthsUntil(new YearMonth(y2, m2)));
    }

    [Fact]
    public void AddMonths_handles_large_offsets_both_ways()
    {
        var start = new YearMonth(2026, 3);

        Assert.Equal(new YearMonth(2028, 3), start.AddMonths(24));
        Assert.Equal(new YearMonth(2024, 2), start.AddMonths(-25));
    }

    [Fact]
    public void First_and_last_day()
    {
        var february = new YearMonth(2028, 2);

        Assert.Equal(new DateOnly(2028, 2, 1), february.FirstDay);
        Assert.Equal(new DateOnly(2028, 2, 29), february.LastDay);
    }

    [Fact]
    public void Ordering()
    {
        Assert.True(new YearMonth(2025, 12) < new YearMonth(2026, 1));
        Assert.True(new YearMonth(2026, 2) > new YearMonth(2026, 1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    public void Rejects_invalid_month(int month)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new YearMonth(2026, month));
    }

    [Fact]
    public void Formats_and_parses_iso_form()
    {
        var value = new YearMonth(2026, 3);

        Assert.Equal("2026-03", value.ToString());
        Assert.Equal(value, YearMonth.Parse("2026-03"));
    }

    [Theory]
    [InlineData("2026-3")]
    [InlineData("2026-13")]
    [InlineData("2026/03")]
    [InlineData("")]
    public void TryParse_rejects_invalid_input(string input)
    {
        Assert.False(YearMonth.TryParse(input, out _));
    }
}
