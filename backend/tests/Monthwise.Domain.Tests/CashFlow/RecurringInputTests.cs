using Monthwise.Domain.CashFlow;
using Monthwise.Domain.Common;

namespace Monthwise.Domain.Tests.CashFlow;

public class RecurringInputTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 6, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 10, 6);

    private static RecurringInputDetails Details(
        DateOnly? issue = null, DateOnly? expected = null, DateOnly? end = null,
        RecurrenceFrequency frequency = RecurrenceFrequency.Monthly) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Money.FromCents(200_000), "Salary",
            new RecurrenceSchedule(issue ?? new DateOnly(2026, 1, 1), expected ?? new DateOnly(2026, 1, 10), frequency, end));

    [Fact]
    public void Occurrence_details_copy_the_template()
    {
        var recurring = RecurringInput.Create(Details(), Now).Value;

        var occurrence = recurring.OccurrenceDetails(recurring.Schedule.Occurrence(9));

        Assert.Equal(new DateOnly(2026, 10, 1), occurrence.IssueDate);
        Assert.Equal(new DateOnly(2026, 10, 10), occurrence.ExpectedDate);
        Assert.Equal(recurring.Amount, occurrence.Amount);
        Assert.Null(occurrence.ReceivedDate);
    }

    [Fact]
    public void End_date_cannot_be_before_the_first_expected_date()
    {
        var result = RecurringInput.Create(Details(end: new DateOnly(2026, 1, 9)), Now);

        Assert.Contains(CashFlowErrors.EndDateBeforeStart, result.Errors);
    }

    [Fact]
    public void A_changed_schedule_must_start_after_the_last_received_occurrence()
    {
        var recurring = RecurringInput.Create(Details(), Now).Value;
        var lastReceived = new DateOnly(2026, 10, 10);

        var tooEarly = recurring.Update(Details(issue: new DateOnly(2026, 10, 5), expected: new DateOnly(2026, 10, 10)), lastReceived, Now);
        var fine = recurring.Update(Details(issue: new DateOnly(2026, 11, 5), expected: new DateOnly(2026, 11, 15)), lastReceived, Now);

        Assert.Contains(CashFlowErrors.ScheduleBeforeReceived, tooEarly.Errors);
        Assert.True(fine.IsSuccess);
    }

    [Fact]
    public void Changing_only_the_amount_keeps_the_schedule_check_off()
    {
        var recurring = RecurringInput.Create(Details(), Now).Value;
        var changed = recurring.Details with { Amount = Money.FromCents(210_000) };

        Assert.True(recurring.Update(changed, new DateOnly(2026, 10, 10), Now).IsSuccess);
        Assert.Equal(Money.FromCents(210_000), recurring.Amount);
    }

    [Fact]
    public void Stop_ends_it_today_and_only_once()
    {
        var recurring = RecurringInput.Create(Details(), Now).Value;

        Assert.True(recurring.Stop(Today, Now).IsSuccess);
        Assert.Equal(Today, recurring.EndDate);
        Assert.Contains(CashFlowErrors.RecurringStopped, recurring.Stop(Today, Now).Errors);
        Assert.Empty(recurring.Schedule.OccurrencesBetween(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30)));
    }

    [Fact]
    public void Stopping_before_the_first_occurrence_leaves_none()
    {
        var recurring = RecurringInput.Create(Details(issue: new DateOnly(2026, 12, 1), expected: new DateOnly(2026, 12, 10)), Now).Value;

        recurring.Stop(Today, Now);

        Assert.True(recurring.IsStoppedOn(Today));
        Assert.Empty(recurring.Schedule.OccurrencesBetween(new DateOnly(2026, 1, 1), new DateOnly(2027, 12, 31)));
    }
}
