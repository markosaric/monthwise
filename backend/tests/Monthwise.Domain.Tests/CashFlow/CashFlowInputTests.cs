using Monthwise.Domain.CashFlow;
using Monthwise.Domain.Common;

namespace Monthwise.Domain.Tests.CashFlow;

public class CashFlowInputTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 6, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 10, 6);

    internal static InputDetails Details(
        long amountCents = 150_000,
        DateOnly? issue = null,
        DateOnly? expected = null,
        DateOnly? received = null,
        string? description = null) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Money.FromCents(amountCents),
            issue ?? new DateOnly(2026, 10, 1), expected ?? new DateOnly(2026, 10, 10), received, description);

    [Fact]
    public void Create_stores_the_details()
    {
        var input = CashFlowInput.Create(Details(description: "  September salary  "), Now).Value;

        Assert.Equal(Money.FromCents(150_000), input.Amount);
        Assert.Equal("September salary", input.Description);
        Assert.False(input.IsReceived);
        Assert.Null(input.RecurringInputId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Amount_must_be_positive(long cents)
    {
        var result = CashFlowInput.Create(Details(amountCents: cents), Now);

        Assert.Contains(CashFlowErrors.AmountNotPositive, result.Errors);
    }

    [Fact]
    public void Expected_and_received_dates_cannot_be_before_the_issue_date()
    {
        var result = CashFlowInput.Create(
            Details(issue: new DateOnly(2026, 10, 5), expected: new DateOnly(2026, 10, 4), received: new DateOnly(2026, 10, 3)), Now);

        Assert.Contains(CashFlowErrors.ExpectedBeforeIssue, result.Errors);
        Assert.Contains(CashFlowErrors.ReceivedBeforeIssue, result.Errors);
    }

    [Fact]
    public void Missing_payer_category_and_account_are_reported()
    {
        var details = Details() with { PayerId = Guid.Empty, CategoryId = Guid.Empty, AccountId = Guid.Empty };

        var result = CashFlowInput.Create(details, Now);

        Assert.Equal(
            [CashFlowErrors.PayerRequired, CashFlowErrors.CategoryRequired, CashFlowErrors.AccountRequired],
            result.Errors);
    }

    [Fact]
    public void Description_is_limited()
    {
        var result = CashFlowInput.Create(Details(description: new string('x', CashFlowInput.DescriptionMaxLength + 1)), Now);

        Assert.Contains(CashFlowErrors.DescriptionTooLong, result.Errors);
    }

    [Fact]
    public void Ticking_received_sets_only_the_received_date_and_unticking_clears_it()
    {
        var input = CashFlowInput.Create(Details(), Now).Value;

        Assert.True(input.MarkReceived(Today, Now).IsSuccess);
        Assert.Equal(Today, input.ReceivedDate);
        Assert.Equal(new DateOnly(2026, 10, 1), input.IssueDate);
        Assert.Equal(new DateOnly(2026, 10, 10), input.ExpectedDate);

        input.MarkExpected(Now);
        Assert.Null(input.ReceivedDate);
        Assert.Equal(new DateOnly(2026, 10, 10), input.ExpectedDate);
    }

    [Fact]
    public void Ticking_received_before_the_issue_date_fails()
    {
        var input = CashFlowInput.Create(Details(issue: new DateOnly(2026, 11, 1), expected: new DateOnly(2026, 11, 5)), Now).Value;

        Assert.Contains(CashFlowErrors.ReceivedBeforeIssue, input.MarkReceived(Today, Now).Errors);
    }

    [Fact]
    public void The_month_follows_the_received_date_once_received()
    {
        var input = CashFlowInput.Create(Details(expected: new DateOnly(2026, 10, 30)), Now).Value;
        Assert.Equal(new YearMonth(2026, 10), input.Month);

        input.Update(input.Details with { ReceivedDate = new DateOnly(2026, 11, 2) }, Now);
        Assert.Equal(new YearMonth(2026, 11), input.Month);
    }
}
