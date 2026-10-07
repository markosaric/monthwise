using Monthwise.Domain.CashFlow;

namespace Monthwise.Domain.Tests.CashFlow;

public class RecurrenceScheduleTests
{
    private static RecurrenceSchedule Monthly(DateOnly issue, DateOnly expected, DateOnly? end = null) =>
        new(issue, expected, RecurrenceFrequency.Monthly, end);

    [Theory]
    [InlineData(RecurrenceFrequency.Weekly, "2026-01-15")]
    [InlineData(RecurrenceFrequency.Monthly, "2026-02-08")]
    [InlineData(RecurrenceFrequency.Quarterly, "2026-04-08")]
    [InlineData(RecurrenceFrequency.Yearly, "2027-01-08")]
    public void The_second_occurrence_is_one_step_after_the_anchor(RecurrenceFrequency frequency, string expected)
    {
        var schedule = new RecurrenceSchedule(new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 8), frequency, null);

        Assert.Equal(DateOnly.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), schedule.Occurrence(1).ExpectedDate);
        Assert.Equal(schedule.Occurrence(1).ExpectedDate.AddDays(-3), schedule.Occurrence(1).IssueDate);
    }

    [Fact]
    public void Month_ends_are_clamped_and_restored_from_the_anchor()
    {
        var schedule = Monthly(new DateOnly(2026, 1, 31), new DateOnly(2026, 1, 31));

        Assert.Equal(new DateOnly(2026, 2, 28), schedule.Occurrence(1).ExpectedDate);
        Assert.Equal(new DateOnly(2026, 3, 31), schedule.Occurrence(2).ExpectedDate);
        Assert.Equal(new DateOnly(2028, 2, 29), schedule.Occurrence(25).ExpectedDate);
    }

    [Fact]
    public void Occurrences_between_returns_those_expected_in_the_range()
    {
        var schedule = Monthly(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 10));

        var october = schedule.OccurrencesBetween(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31)).ToList();

        var single = Assert.Single(october);
        Assert.Equal(new DateOnly(2026, 10, 10), single.ExpectedDate);
        Assert.Equal(new DateOnly(2026, 10, 1), single.IssueDate);
    }

    [Fact]
    public void Nothing_before_the_anchor_or_after_the_end_date()
    {
        var schedule = Monthly(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 10), end: new DateOnly(2026, 5, 10));

        var all = schedule.OccurrencesBetween(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)).Select(o => o.ExpectedDate);

        Assert.Equal([new DateOnly(2026, 3, 10), new DateOnly(2026, 4, 10), new DateOnly(2026, 5, 10)], all);
    }

    [Fact]
    public void Weekly_has_four_or_five_occurrences_in_a_month()
    {
        var schedule = new RecurrenceSchedule(new DateOnly(2026, 1, 2), new DateOnly(2026, 1, 2), RecurrenceFrequency.Weekly, null);

        Assert.Equal(5, schedule.OccurrencesBetween(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31)).Count());
        Assert.Equal(4, schedule.OccurrencesBetween(new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 28)).Count());
    }

    [Fact]
    public void Occurrence_on_finds_only_scheduled_dates()
    {
        var schedule = Monthly(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 10));

        Assert.NotNull(schedule.OccurrenceOn(new DateOnly(2027, 6, 10)));
        Assert.Null(schedule.OccurrenceOn(new DateOnly(2027, 6, 11)));
        Assert.Null(schedule.OccurrenceOn(new DateOnly(2025, 12, 10)));
    }
}
