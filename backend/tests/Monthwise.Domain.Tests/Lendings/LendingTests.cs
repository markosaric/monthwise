using Monthwise.Domain.Common;
using Monthwise.Domain.Lendings;

namespace Monthwise.Domain.Tests.Lendings;

public class LendingTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly LendDate = new(2026, 11, 2);
    private static readonly DateOnly ReturnDate = new(2026, 12, 15);
    private static readonly Guid Account = Guid.NewGuid();
    private static readonly Guid OtherAccount = Guid.NewGuid();

    private static LendingDetails Lent(long cents = 50_000) =>
        new(Guid.NewGuid(), Money.FromCents(cents), LendDate, Account, ReturnDate, "For the car repair");

    private static RepaymentDetails Repaid(long cents, DateOnly? on = null, Guid? to = null) =>
        new(Money.FromCents(cents), to ?? Account, on ?? new DateOnly(2026, 12, 1), null);

    private static Lending NewLending(long cents = 50_000) => Lending.Create(Lent(cents), Now).Value;

    [Fact]
    public void A_lending_needs_a_positive_amount_an_account_and_a_return_date_not_before_the_lend_date()
    {
        var result = Lending.Create(
            new LendingDetails(Guid.NewGuid(), Money.Zero, LendDate, Guid.Empty, LendDate.AddDays(-1), new string('x', 201)), Now);

        Assert.Equal(
            [LendingErrors.AmountNotPositive, LendingErrors.FromAccountRequired, LendingErrors.ReturnBeforeLend, LendingErrors.DescriptionTooLong],
            result.Errors);
    }

    [Fact]
    public void A_new_lending_is_open_with_everything_outstanding()
    {
        var lending = NewLending();

        Assert.Equal(50_000, lending.Outstanding.Cents);
        Assert.Equal(0, lending.Returned.Cents);
        Assert.Equal(LendingState.Open, lending.StateOn(ReturnDate));
        Assert.Equal("For the car repair", lending.Description);
    }

    [Fact]
    public void Several_repayments_lower_the_outstanding_amount_until_the_lending_is_returned()
    {
        var lending = NewLending();

        Assert.True(lending.AddRepayment(Repaid(20_000), Now).IsSuccess);
        Assert.Equal(30_000, lending.Outstanding.Cents);
        Assert.Equal(LendingState.Open, lending.StateOn(ReturnDate));

        Assert.True(lending.AddRepayment(Repaid(30_000, to: OtherAccount), Now).IsSuccess);
        Assert.Equal(0, lending.Outstanding.Cents);
        Assert.Equal(LendingState.Returned, lending.StateOn(ReturnDate.AddYears(1)));
        Assert.Equal(2, lending.Repayments.Count);
    }

    [Fact]
    public void A_repayment_cannot_be_more_than_the_outstanding_amount_or_before_the_lend_date()
    {
        var lending = NewLending();
        lending.AddRepayment(Repaid(40_000), Now);

        var tooMuch = lending.AddRepayment(Repaid(10_001), Now);
        var tooEarly = lending.AddRepayment(Repaid(100, LendDate.AddDays(-1)), Now);
        var empty = lending.AddRepayment(new RepaymentDetails(Money.Zero, Guid.Empty, LendDate, new string('x', 201)), Now);

        Assert.Equal(LendingErrors.RepaymentAboveOutstanding, Assert.Single(tooMuch.Errors));
        Assert.Equal(LendingErrors.ReceivedBeforeLend, Assert.Single(tooEarly.Errors));
        Assert.Equal(
            [LendingErrors.RepaymentAmountNotPositive, LendingErrors.ToAccountRequired, LendingErrors.NoteTooLong],
            empty.Errors);
        Assert.Equal(10_000, lending.Outstanding.Cents);
    }

    [Fact]
    public void Editing_a_repayment_counts_its_own_amount_as_available()
    {
        var lending = NewLending();
        var repayment = lending.AddRepayment(Repaid(20_000), Now).Value;

        Assert.True(lending.UpdateRepayment(repayment.Id, Repaid(50_000), Now).IsSuccess);
        Assert.Equal(LendingErrors.RepaymentAboveOutstanding, Assert.Single(lending.UpdateRepayment(repayment.Id, Repaid(50_001), Now).Errors));
        Assert.Equal(0, lending.Outstanding.Cents);

        Assert.True(lending.RemoveRepayment(repayment.Id, Now).IsSuccess);
        Assert.Equal(50_000, lending.Outstanding.Cents);
        Assert.Equal(LendingErrors.RepaymentNotFound, Assert.Single(lending.RemoveRepayment(repayment.Id, Now).Errors));
    }

    [Fact]
    public void The_amount_cannot_go_below_what_was_returned_and_the_lend_date_not_after_a_repayment()
    {
        var lending = NewLending();
        lending.AddRepayment(Repaid(20_000, new DateOnly(2026, 11, 20)), Now);

        var belowReturned = lending.Update(Lent(19_999), Now);
        var afterRepayment = lending.Update(Lent() with { LendDate = new DateOnly(2026, 11, 21) }, Now);
        var sameAsReturned = lending.Update(Lent(20_000) with { LendDate = new DateOnly(2026, 11, 20) }, Now);

        Assert.Equal(LendingErrors.AmountBelowReturned, Assert.Single(belowReturned.Errors));
        Assert.Equal(LendingErrors.LendAfterRepayment, Assert.Single(afterRepayment.Errors));
        Assert.True(sameAsReturned.IsSuccess);
        Assert.Equal(LendingState.Returned, lending.StateOn(ReturnDate));
    }

    [Fact]
    public void A_lending_is_overdue_after_its_expected_return_date_while_something_is_outstanding()
    {
        var lending = NewLending();

        Assert.Equal(LendingState.Open, lending.StateOn(ReturnDate));
        Assert.Equal(LendingState.Overdue, lending.StateOn(ReturnDate.AddDays(1)));

        // A later expected return date applies to the outstanding amount (FR-4).
        lending.Update(Lent() with { ExpectedReturnDate = ReturnDate.AddMonths(1) }, Now);
        Assert.Equal(LendingState.Open, lending.StateOn(ReturnDate.AddDays(1)));
    }
}
