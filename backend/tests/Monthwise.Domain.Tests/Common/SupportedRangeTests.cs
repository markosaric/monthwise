using Monthwise.Domain.Common;

namespace Monthwise.Domain.Tests.Common;

/// <summary>The supported range of months and dates (month-range FR-1, FR-2).</summary>
public class SupportedRangeTests
{
    [Theory]
    [InlineData("2000-01", true)]
    [InlineData("2100-12", true)]
    [InlineData("2026-11", true)]
    [InlineData("1999-12", false)]
    [InlineData("2101-01", false)]
    [InlineData("0001-01", false)]
    [InlineData("9999-12", false)]
    [InlineData("2026-13", false)]
    [InlineData("2026-1", false)]
    [InlineData(null, false)]
    public void TryParseSupported_accepts_only_months_in_the_range(string? value, bool expected)
    {
        Assert.Equal(expected, YearMonth.TryParseSupported(value, out var month));
        if (!expected)
        {
            Assert.Equal(default, month);
        }
    }

    [Fact]
    public void TryParse_still_accepts_months_outside_the_range_for_stored_data()
    {
        Assert.True(YearMonth.TryParse("1990-05", out var month));
        Assert.False(SupportedRange.Contains(month));
    }

    [Theory]
    [InlineData(2000, 1, 1, true)]
    [InlineData(2100, 12, 31, true)]
    [InlineData(1999, 12, 31, false)]
    [InlineData(2101, 1, 1, false)]
    public void Contains_checks_dates_at_the_edges(int year, int month, int day, bool expected)
    {
        Assert.Equal(expected, SupportedRange.Contains(new DateOnly(year, month, day)));
    }

    [Fact]
    public void Messages_name_the_range()
    {
        Assert.Equal("Enter a month from 2000-01 to 2100-12.", SupportedRange.MonthMessage);
        Assert.Equal("Enter a date from 2000-01-01 to 2100-12-31.", SupportedRange.DateMessage);
    }
}
