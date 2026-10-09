using Monthwise.Domain.Common;
using Monthwise.Domain.Expenses;

namespace Monthwise.Domain.Tests.Expenses;

public class BillTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly YearMonth November = new(2026, 11);
    private static readonly Guid Electricity = Guid.NewGuid();
    private static readonly Guid Account = Guid.NewGuid();

    private static RecurringBill Recurring(Guid? paidFrom = null) =>
        RecurringBill.Create(new RecurringBillDetails("Electricity, apartment", Electricity, "HEP", paidFrom, null), Now).Value;

    private static BillDetails Issued(long amountCents = 4_250, DateOnly? issue = null, DateOnly? due = null, Guid? paidFrom = null) =>
        new("Electricity, apartment", Electricity, "HEP", Money.FromCents(amountCents),
            issue ?? new DateOnly(2026, 11, 3), due ?? new DateOnly(2026, 11, 15), paidFrom, null);

    [Fact]
    public void A_bill_from_a_recurring_bill_starts_not_issued_with_amount_zero_and_no_dates()
    {
        var bill = Bill.CreateFromRecurring(Recurring(Account), November, Now);

        Assert.Equal(BillState.NotIssued, bill.State);
        Assert.True(bill.Amount.IsZero);
        Assert.Null(bill.IssueDate);
        Assert.Null(bill.DueDate);
        Assert.Equal(November, bill.Month);
        Assert.Equal(Account, bill.PaidFromId);
        Assert.Equal("HEP", bill.Payee);
    }

    [Fact]
    public void A_not_issued_bill_cannot_be_paid()
    {
        var bill = Bill.CreateFromRecurring(Recurring(), November, Now);

        var result = bill.MarkPaid(new DateOnly(2026, 11, 10), Account, Now);

        Assert.Equal(ExpenseErrors.BillNotIssued, Assert.Single(result.Errors));
        Assert.False(bill.IsPaid);
    }

    [Fact]
    public void Entering_amount_and_dates_issues_the_bill()
    {
        var bill = Bill.CreateFromRecurring(Recurring(), November, Now);

        Assert.True(bill.Update(Issued(), paidDate: null, Now).IsSuccess);

        Assert.Equal(BillState.Expected, bill.State);
        Assert.Equal(4_250, bill.Amount.Cents);
    }

    [Fact]
    public void An_issued_bill_needs_amount_issue_date_and_a_due_date_not_before_it()
    {
        var bill = Bill.CreateFromRecurring(Recurring(), November, Now);

        var noDates = bill.Update(Issued() with { IssueDate = null, DueDate = null }, null, Now);
        var dueBeforeIssue = bill.Update(Issued(due: new DateOnly(2026, 11, 1)), null, Now);

        Assert.Contains(ExpenseErrors.IssueDateRequired, noDates.Errors);
        Assert.Contains(ExpenseErrors.DueDateRequired, noDates.Errors);
        Assert.Equal(ExpenseErrors.DueBeforeIssue, Assert.Single(dueBeforeIssue.Errors));
        Assert.Equal(BillState.NotIssued, bill.State);
    }

    [Fact]
    public void A_bill_from_a_recurring_bill_can_go_back_to_not_issued_but_a_one_off_bill_cannot()
    {
        var fromRecurring = Bill.CreateFromRecurring(Recurring(), November, Now);
        fromRecurring.Update(Issued(), null, Now);
        var oneOff = Bill.CreateOneOff(November, Issued(), null, Now).Value;
        var empty = Issued(amountCents: 0) with { IssueDate = null, DueDate = null };

        Assert.True(fromRecurring.Update(empty, null, Now).IsSuccess);
        Assert.Equal(BillState.NotIssued, fromRecurring.State);
        Assert.Contains(ExpenseErrors.AmountNotPositive, oneOff.Update(empty, null, Now).Errors);
    }

    [Fact]
    public void Paying_records_the_date_and_what_it_was_paid_with_and_unticking_returns_it_to_expected()
    {
        var bill = Bill.CreateOneOff(November, Issued(), null, Now).Value;

        Assert.True(bill.MarkPaid(new DateOnly(2026, 11, 20), Account, Now).IsSuccess);
        Assert.Equal(BillState.Paid, bill.State);
        Assert.Equal(new DateOnly(2026, 11, 20), bill.CashFlowDate);
        Assert.Equal(Account, bill.PaidFromId);

        bill.MarkUnpaid(Now);
        Assert.Equal(BillState.Expected, bill.State);
        Assert.Null(bill.PaidDate);
        Assert.Equal(new DateOnly(2026, 11, 15), bill.CashFlowDate);
    }

    [Fact]
    public void A_bill_cannot_be_paid_before_it_was_issued()
    {
        var bill = Bill.CreateOneOff(November, Issued(), null, Now).Value;

        Assert.Equal(ExpenseErrors.PaidBeforeIssue, Assert.Single(bill.MarkPaid(new DateOnly(2026, 11, 2), Account, Now).Errors));
        Assert.Contains(ExpenseErrors.PaidFromRequired, Bill.CreateOneOff(November, Issued(), new DateOnly(2026, 11, 5), Now).Errors);
    }

    [Fact]
    public void An_expected_bill_is_overdue_after_its_due_date()
    {
        var bill = Bill.CreateOneOff(November, Issued(), null, Now).Value;

        Assert.False(bill.IsOverdue(new DateOnly(2026, 11, 15)));
        Assert.True(bill.IsOverdue(new DateOnly(2026, 11, 16)));
        bill.MarkPaid(new DateOnly(2026, 11, 16), Account, Now);
        Assert.False(bill.IsOverdue(new DateOnly(2026, 11, 17)));
    }

    [Fact]
    public void Bill_and_recurring_bill_fields_are_checked()
    {
        var bill = Bill.CreateOneOff(
            November, new BillDetails(" ", Guid.Empty, new string('p', 101), Money.FromCents(-1), null, null, null, new string('d', 201)),
            null, Now);
        var recurring = RecurringBill.Create(new RecurringBillDetails(new string('n', 51), Guid.Empty, null, null, null), Now);

        Assert.Equal(
            [ExpenseErrors.NameRequired, ExpenseErrors.CategoryRequired, ExpenseErrors.PayeeTooLong, ExpenseErrors.BillDescriptionTooLong,
                ExpenseErrors.AmountNotPositive, ExpenseErrors.IssueDateRequired, ExpenseErrors.DueDateRequired],
            bill.Errors);
        Assert.Equal([ExpenseErrors.NameTooLong, ExpenseErrors.CategoryRequired], recurring.Errors);
    }

    [Fact]
    public void A_stopped_recurring_bill_is_not_active_until_restarted()
    {
        var recurring = Recurring();

        recurring.Stop(new DateOnly(2026, 10, 9), Now);
        Assert.False(recurring.IsActive);
        Assert.Equal(new DateOnly(2026, 10, 9), recurring.StoppedOn);

        recurring.Restart(Now);
        Assert.True(recurring.IsActive);
    }
}
