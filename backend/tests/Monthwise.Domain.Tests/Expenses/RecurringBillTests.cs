using Monthwise.Domain.Common;
using Monthwise.Domain.Expenses;

namespace Monthwise.Domain.Tests.Expenses;

public class RecurringBillTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly Guid Category = Guid.NewGuid();

    private static RecurringBillDetails Details(long? amountCents = null, int? issueDay = null, int? dueDay = null) =>
        new("Internet", Category, "Made-up provider", null, null,
            amountCents is { } cents ? Money.FromCents(cents) : null, issueDay, dueDay);

    [Fact]
    public void Amount_issue_day_and_due_day_are_optional()
    {
        var empty = RecurringBill.Create(Details(), Now);
        var full = RecurringBill.Create(Details(2_999, 5, 20), Now);

        Assert.True(empty.IsSuccess);
        Assert.Null(empty.Value.Amount);
        Assert.Null(empty.Value.IssueDay);
        Assert.Null(empty.Value.DueDay);
        Assert.True(full.IsSuccess);
        Assert.Equal(2_999, full.Value.Amount?.Cents);
        Assert.Equal(5, full.Value.IssueDay);
        Assert.Equal(20, full.Value.DueDay);
    }

    [Theory]
    [InlineData(0L, null, null, "recurringBill.amount.notPositive")]
    [InlineData(-5L, null, null, "recurringBill.amount.notPositive")]
    [InlineData(null, 0, null, "recurringBill.issueDay.outOfRange")]
    [InlineData(null, 32, null, "recurringBill.issueDay.outOfRange")]
    [InlineData(null, null, 0, "recurringBill.dueDay.outOfRange")]
    [InlineData(null, null, 32, "recurringBill.dueDay.outOfRange")]
    public void Amount_must_be_positive_and_days_from_1_to_31(long? amountCents, int? issueDay, int? dueDay, string code)
    {
        var created = RecurringBill.Create(Details(amountCents, issueDay, dueDay), Now);

        Assert.Equal(code, Assert.Single(created.Errors).Code);
    }

    [Fact]
    public void Editing_can_clear_the_amount_and_days()
    {
        var bill = RecurringBill.Create(Details(2_999, 5, 20), Now).Value;

        Assert.True(bill.Update(Details(), Now).IsSuccess);

        Assert.Null(bill.Amount);
        Assert.Null(bill.IssueDay);
        Assert.Null(bill.DueDay);
    }

    [Fact]
    public void Days_fall_on_the_last_day_of_a_shorter_month()
    {
        var bill = RecurringBill.Create(Details(issueDay: 30, dueDay: 31), Now).Value;

        var (issue, due) = bill.DatesIn(new YearMonth(2027, 2));

        Assert.Equal(new DateOnly(2027, 2, 28), issue);
        Assert.Equal(new DateOnly(2027, 2, 28), due);
    }

    [Fact]
    public void A_due_day_earlier_than_the_issue_day_is_in_the_following_month()
    {
        var bill = RecurringBill.Create(Details(issueDay: 25, dueDay: 10), Now).Value;

        var (issue, due) = bill.DatesIn(new YearMonth(2026, 12));

        Assert.Equal(new DateOnly(2026, 12, 25), issue);
        Assert.Equal(new DateOnly(2027, 1, 10), due);
    }

    [Fact]
    public void Only_the_days_that_are_set_give_dates()
    {
        var bill = RecurringBill.Create(Details(dueDay: 15), Now).Value;

        var (issue, due) = bill.DatesIn(new YearMonth(2026, 11));

        Assert.Null(issue);
        Assert.Equal(new DateOnly(2026, 11, 15), due);
    }
}
