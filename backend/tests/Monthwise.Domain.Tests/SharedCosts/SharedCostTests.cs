using Monthwise.Domain.CashFlow;
using Monthwise.Domain.Common;
using Monthwise.Domain.Expenses;
using Monthwise.Domain.Loans;
using Monthwise.Domain.SharedCosts;
using static Monthwise.Domain.Tests.Loans.LoanTestData;

namespace Monthwise.Domain.Tests.SharedCosts;

public class SharedCostTests
{
    private static readonly DateOnly Today = new(2026, 10, 20);
    private static readonly YearMonth November = new(2026, 11);
    private static readonly Guid Ana = Guid.NewGuid();
    private static readonly CostShare Half = new(Ana, 2, 1);
    private static readonly CostShare Third = new(Ana, 3, 1);

    private static RecurringBill Recurring(CostShare? share)
    {
        var recurring = RecurringBill.Create(new RecurringBillDetails("Electricity, apartment", Guid.NewGuid(), null, null, null), Now).Value;
        recurring.SetShare(share, Now);
        return recurring;
    }

    [Fact]
    public void A_bill_added_from_a_shared_recurring_bill_takes_its_share()
    {
        var bill = Bill.CreateFromRecurring(Recurring(Half), November, Now);

        Assert.Equal(Half, bill.Share);
        Assert.False(bill.IsShareChanged);
    }

    [Fact]
    public void Changing_the_share_of_one_bill_marks_it_as_changed_and_leaves_the_recurring_bill()
    {
        var recurring = Recurring(Half);
        var bill = Bill.CreateFromRecurring(recurring, November, Now);

        bill.SetShare(Third, recurring.Share, Now);

        Assert.Equal(Third, bill.Share);
        Assert.True(bill.IsShareChanged);
        Assert.Equal(Half, recurring.Share);

        bill.SetShare(Half, recurring.Share, Now);
        Assert.False(bill.IsShareChanged);
    }

    [Fact]
    public void A_one_off_bill_can_be_shared_without_being_marked_as_changed()
    {
        var bill = Bill.CreateOneOff(
            November,
            new BillDetails("Plumber", Guid.NewGuid(), null, Money.FromCents(12_000), new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 9), null, null),
            null,
            Now).Value;

        bill.SetShare(Half, null, Now);

        Assert.Equal(Half, bill.Share);
        Assert.False(bill.IsShareChanged);
    }

    [Fact]
    public void The_loans_share_applies_to_every_payment_the_residual_value_included()
    {
        var loan = NewLoan(Details(method: PaymentMethod.Bill, residualCents: 300_000));
        loan.SetShare(Half, Now);

        var payments = loan.Schedule([]).Payments(Today);

        Assert.All(payments, p => Assert.Equal(Half, p.Share));
        Assert.Contains(payments, p => p.Kind == LoanPaymentKind.Residual);
    }

    [Fact]
    public void A_payment_with_its_own_share_or_not_shared_keeps_it_when_the_loans_share_changes()
    {
        var loan = NewLoan(Details(method: PaymentMethod.Bill));
        loan.SetShare(Half, Now);
        var october = new YearMonth(2026, 10);
        var november = new YearMonth(2026, 11);
        var thirds = LoanPayment.Create(loan.Id, october, LoanPaymentKind.Regular, Now);
        thirds.ChangeShare(Third, Now);
        var notShared = LoanPayment.Create(loan.Id, november, LoanPaymentKind.Regular, Now);
        notShared.ChangeShare(null, Now);

        loan.SetShare(new CostShare(Ana, 4, 1), Now);
        var schedule = loan.Schedule([thirds, notShared]);

        Assert.Equal(Third, schedule.Find(october, LoanPaymentKind.Regular, Today)!.Share);
        Assert.True(schedule.Find(october, LoanPaymentKind.Regular, Today)!.IsShareChanged);
        Assert.Null(schedule.Find(november, LoanPaymentKind.Regular, Today)!.Share);
        Assert.Equal(4, schedule.Find(new YearMonth(2026, 12), LoanPaymentKind.Regular, Today)!.Share!.Shares);
    }

    [Fact]
    public void A_paid_payment_keeps_the_share_it_was_paid_with()
    {
        var loan = NewLoan(Details(method: PaymentMethod.Bill));
        loan.SetShare(Half, Now);
        var october = new YearMonth(2026, 10);
        var stored = LoanPayment.Create(loan.Id, october, LoanPaymentKind.Regular, Now);
        stored.MarkPaid(new DateOnly(2026, 10, 15), AccountId, Money.FromCents(50_000), new DateOnly(2026, 10, 15), Now, Half);

        loan.SetShare(Third, Now);

        Assert.Equal(Half, loan.Schedule([stored]).Find(october, LoanPaymentKind.Regular, Today)!.Share);
        Assert.False(stored.IsShareChanged);

        // Unticking a bill makes it expected as calculated again (LoanPaymentHandler).
        stored.MarkUnpaid(Now);
        stored.ClearPaidState(Now);
        Assert.True(stored.IsEmpty);
        Assert.Equal(Third, loan.Schedule([stored]).Find(october, LoanPaymentKind.Regular, Today)!.Share);
    }

    [Fact]
    public void Resetting_the_share_of_a_payment_makes_it_follow_the_loan_again()
    {
        var loan = NewLoan(Details(method: PaymentMethod.Bill));
        loan.SetShare(Half, Now);
        var october = new YearMonth(2026, 10);
        var stored = LoanPayment.Create(loan.Id, october, LoanPaymentKind.Regular, Now);
        stored.ChangeShare(null, Now);

        stored.ResetShare(Now);

        Assert.True(stored.IsEmpty);
        Assert.Equal(Half, loan.Schedule([stored]).Find(october, LoanPaymentKind.Regular, Today)!.Share);
    }

    [Fact]
    public void A_contribution_is_the_part_with_the_costs_dates_and_the_first_account_that_applies()
    {
        var payer = Guid.NewGuid();
        var chosen = Guid.NewGuid();
        var paidFrom = Guid.NewGuid();
        var primary = Guid.NewGuid();
        var issue = new DateOnly(2026, 11, 3);
        var due = new DateOnly(2026, 11, 15);

        var values = ContributionValues.Calculate(Half, payer, Money.FromCents(8_501), issue, due, paidFrom, primary, "Electricity");

        Assert.Equal(4_251, values.Amount.Cents);
        Assert.Equal(issue, values.IssueDate);
        Assert.Equal(due, values.ExpectedDate);
        Assert.Equal(paidFrom, values.AccountId);
        Assert.Equal("Electricity", values.Description);
        Assert.Equal(chosen, ContributionValues.Calculate(Half with { ContributionAccountId = chosen }, payer, Money.FromCents(100), issue, due, paidFrom, primary, "x").AccountId);
        Assert.Equal(primary, ContributionValues.Calculate(Half, payer, Money.FromCents(100), issue, due, null, primary, "x").AccountId);
        Assert.Null(ContributionValues.Calculate(Half, payer, Money.FromCents(100), issue, due, null, null, "x").AccountId);
    }

    [Fact]
    public void A_stored_contribution_knows_its_cost_until_it_is_detached()
    {
        var bill = Guid.NewGuid();
        var details = new InputDetails(
            Guid.NewGuid(), ContributionValues.AgreedContributionsCategoryId, AccountId, Money.FromCents(4_250),
            new DateOnly(2026, 11, 3), new DateOnly(2026, 11, 15), new DateOnly(2026, 11, 16), "Electricity");

        var input = CashFlowInput.CreateContribution(details, ContributionSource.ForBill(bill), isEdited: false, Now).Value;
        Assert.Equal(ContributionSource.ForBill(bill), input.Contribution);

        input.Detach(Now);
        Assert.Null(input.Contribution);
        Assert.True(input.IsReceived);
    }

    [Fact]
    public void A_loan_payment_contribution_is_identified_by_loan_month_and_kind()
    {
        var loan = Guid.NewGuid();
        var source = ContributionSource.ForLoanPayment(loan, November, LoanPaymentKind.Residual);
        var details = new InputDetails(
            Guid.NewGuid(), ContributionValues.AgreedContributionsCategoryId, AccountId, Money.FromCents(1_000),
            new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 15), null, "Car lease, residual value");

        var input = CashFlowInput.CreateContribution(details, source, isEdited: true, Now).Value;

        Assert.Equal(source, input.Contribution);
        Assert.True(input.IsEdited);
    }
}
