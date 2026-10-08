using Monthwise.Domain.Common;
using Monthwise.Domain.Loans;
using static Monthwise.Domain.Tests.Loans.LoanTestData;

namespace Monthwise.Domain.Tests.Loans;

public class LoanScheduleTests
{
    private static readonly DateOnly Today = new(2026, 10, 20);

    [Fact]
    public void There_is_one_payment_per_month_from_the_start_to_the_end_month()
    {
        var payments = NewLoan().Schedule([]).Payments(Today);

        Assert.Equal(12, payments.Count);
        Assert.All(payments, p => Assert.Equal(LoanPaymentKind.Regular, p.Kind));
        var first = payments[0];
        Assert.Equal(new YearMonth(2026, 8), first.Month);
        Assert.Equal(1, first.Number);
        Assert.Equal(12, first.Total);
        Assert.Equal(new DateOnly(2026, 8, 1), first.BillDate);
        Assert.Equal(new DateOnly(2026, 8, 15), first.DueDate);
        Assert.Equal(new YearMonth(2027, 7), payments[^1].Month);
    }

    [Fact]
    public void A_standing_order_payment_is_paid_after_its_due_date_and_not_on_it()
    {
        var schedule = NewLoan().Schedule([]);
        var month = new YearMonth(2026, 10);

        var onDueDate = schedule.Find(month, LoanPaymentKind.Regular, new DateOnly(2026, 10, 15))!;
        var dayAfter = schedule.Find(month, LoanPaymentKind.Regular, new DateOnly(2026, 10, 16))!;

        Assert.False(onDueDate.IsPaid);
        Assert.Equal(AccountId, onDueDate.AccountId);
        Assert.True(dayAfter.IsPaid);
        Assert.True(dayAfter.PaidAutomatically);
        Assert.Equal(new DateOnly(2026, 10, 15), dayAfter.PaidDate);
        Assert.Equal(AccountId, dayAfter.AccountId);
    }

    [Fact]
    public void A_bill_is_paid_only_when_ticked()
    {
        var loan = NewLoan(Details(method: PaymentMethod.Bill));
        var stored = LoanPayment.Create(loan.Id, new YearMonth(2026, 10), LoanPaymentKind.Regular, Now);
        var account = Guid.NewGuid();
        stored.MarkPaid(new DateOnly(2026, 10, 22), account, Money.FromCents(50_000), new DateOnly(2026, 10, 15), Now);

        var payments = loan.Schedule([stored]).Payments(Today);

        Assert.False(payments[1].IsPaid);
        Assert.True(payments[1].IsOverdue(Today));
        var october = payments[2];
        Assert.True(october.IsPaid);
        Assert.False(october.PaidAutomatically);
        Assert.Equal(new DateOnly(2026, 10, 22), october.MonthDate);
        Assert.Equal(account, october.AccountId);
        Assert.Null(payments[3].AccountId);
    }

    [Fact]
    public void A_bill_cannot_be_paid_before_its_bill_date_or_without_an_account()
    {
        var stored = LoanPayment.Create(Guid.NewGuid(), new YearMonth(2026, 10), LoanPaymentKind.Regular, Now);

        var result = stored.MarkPaid(new DateOnly(2026, 9, 30), Guid.Empty, Money.FromCents(1), new DateOnly(2026, 10, 15), Now);

        Assert.Equal(new[] { LoanErrors.PaidBeforeBillDate, LoanErrors.PaidAccountRequired }, result.Errors);
    }

    [Fact]
    public void An_unticked_standing_order_payment_stays_expected()
    {
        var loan = NewLoan();
        var stored = LoanPayment.Create(loan.Id, new YearMonth(2026, 9), LoanPaymentKind.Regular, Now);
        stored.MarkUnpaid(Now);

        var september = loan.Schedule([stored]).Find(new YearMonth(2026, 9), LoanPaymentKind.Regular, Today)!;

        Assert.False(september.IsPaid);
        Assert.True(september.IsOverdue(Today));
    }

    [Fact]
    public void A_residual_value_is_a_bill_in_the_month_after_the_end()
    {
        var lease = NewLoan(Details(residualCents: 1_000_000));

        var residual = lease.Schedule([]).Payments(Today)[^1];

        Assert.Equal(LoanPaymentKind.Residual, residual.Kind);
        Assert.Equal(PaymentMethod.Bill, residual.Method);
        Assert.Equal(new YearMonth(2027, 8), residual.Month);
        Assert.Equal(Money.FromCents(1_000_000), residual.Amount);
        // 15 August 2027 is a Sunday, so the bill is due on Monday.
        Assert.Equal(new DateOnly(2027, 8, 16), residual.DueDate);
        Assert.Null(residual.AccountId);
    }

    [Fact]
    public void Payment_changes_apply_from_their_own_month()
    {
        var loan = NewLoan();
        loan.AddChange(new PaymentChangeDetails(new YearMonth(2026, 11), Money.FromCents(52_000), null, null), null, Now);
        loan.AddChange(new PaymentChangeDetails(new YearMonth(2027, 1), null, 10, null), null, Now);

        var payments = loan.Schedule([]).Payments(Today);

        Assert.Equal(Money.FromCents(50_000), payments[2].Amount);
        Assert.Equal(Money.FromCents(52_000), payments[3].Amount);
        Assert.Equal(new DateOnly(2026, 12, 15), payments[4].DueDate);
        Assert.Equal(new DateOnly(2027, 1, 10), payments[5].DueDate);
        Assert.Equal(Money.FromCents(52_000), payments[5].Amount);
    }

    [Fact]
    public void A_changed_payment_keeps_its_values_and_is_marked()
    {
        var loan = NewLoan();
        var stored = LoanPayment.Create(loan.Id, new YearMonth(2026, 11), LoanPaymentKind.Regular, Now);
        stored.Change(Money.FromCents(51_000), new DateOnly(2026, 11, 16), Now);

        var payments = loan.Schedule([stored]).Payments(Today);

        Assert.True(payments[3].IsChanged);
        Assert.Equal(Money.FromCents(51_000), payments[3].Amount);
        Assert.Equal(new DateOnly(2026, 11, 16), payments[3].DueDate);
        Assert.False(payments[4].IsChanged);
        Assert.Equal(Money.FromCents(50_000), payments[4].Amount);
    }

    [Fact]
    public void A_changed_due_date_stays_in_its_month()
    {
        var stored = LoanPayment.Create(Guid.NewGuid(), new YearMonth(2026, 11), LoanPaymentKind.Regular, Now);

        var result = stored.Change(Money.FromCents(51_000), new DateOnly(2026, 12, 1), Now);

        Assert.Equal(new[] { LoanErrors.PaymentDueDateOutsideMonth }, result.Errors);
    }

    [Fact]
    public void The_summary_counts_paid_and_remaining_payments()
    {
        var summary = NewLoan().Schedule([]).Summary(Today);

        Assert.Equal(LoanStatus.Active, summary.Status);
        Assert.Equal(3, summary.PaymentsPaid);
        Assert.Equal(9, summary.PaymentsLeft);
        Assert.Equal(Money.FromCents(150_000), summary.PaidAmount);
        Assert.Equal(Money.FromCents(450_000), summary.RemainingAmount);
        Assert.Equal(new DateOnly(2026, 11, 15), summary.NextPaymentDate);
        Assert.False(summary.HasOverduePayment);
    }

    [Fact]
    public void The_remaining_amount_includes_an_unpaid_residual_value()
    {
        var lease = NewLoan(Details(residualCents: 1_000_000));

        Assert.Equal(Money.FromCents(450_000 + 1_000_000), lease.Schedule([]).RemainingAmount(Today));
    }

    [Fact]
    public void A_loan_with_all_payments_paid_is_paid_off()
    {
        var loan = NewLoan(Details(count: 2));

        Assert.Equal(LoanStatus.PaidOff, loan.Schedule([]).Summary(Today).Status);
    }

    [Fact]
    public void Closing_keeps_only_the_paid_payments()
    {
        var loan = NewLoan();
        loan.Close(Today, Now);

        var schedule = loan.Schedule([]);

        Assert.Equal(3, schedule.Payments(new DateOnly(2027, 3, 1)).Count);
        Assert.Equal(Money.Zero, schedule.RemainingAmount(Today));
        Assert.Equal(LoanStatus.Closed, schedule.Summary(Today).Status);
    }

    [Fact]
    public void Payments_after_the_account_stops_need_a_new_account()
    {
        var schedule = NewLoan().Schedule([], chargesStopOn: new DateOnly(2026, 10, 1));

        var payments = schedule.Payments(Today);

        Assert.True(schedule.NeedsAccount);
        Assert.True(payments[1].IsPaid);
        Assert.False(payments[2].IsPaid);
        Assert.True(payments[2].NeedsAccount);
        Assert.True(schedule.Summary(Today).HasOverduePayment);
    }

    [Fact]
    public void The_latest_paid_month_and_the_paid_range_follow_the_payments()
    {
        var schedule = NewLoan().Schedule([]);

        Assert.Equal(new YearMonth(2026, 10), schedule.LatestPaidMonth(Today));
        Assert.Equal(new PaidPayments(new YearMonth(2026, 8), new YearMonth(2026, 10), false), schedule.Paid(Today));
        Assert.Equal(3, schedule.AutomaticallyPaid(Today).Count());
    }

    [Fact]
    public void A_kept_automatic_payment_stays_paid_after_the_loan_becomes_a_bill()
    {
        var loan = NewLoan();
        var stored = loan.Schedule([]).AutomaticallyPaid(Today)
            .Select(p =>
            {
                var payment = LoanPayment.Create(loan.Id, p.Month, p.Kind, Now);
                payment.KeepAutomaticPayment(p.Amount, p.DueDate, p.AccountId!.Value, Now);
                return payment;
            })
            .ToList();

        loan.Update(loan.Details with { PaymentMethod = PaymentMethod.Bill, AccountId = null }, true, PaidPayments.None, Now);
        var payments = loan.Schedule(stored).Payments(Today);

        Assert.All(payments.Take(3), p => Assert.True(p.IsPaid));
        Assert.All(payments.Take(3), p => Assert.Equal(AccountId, p.AccountId));
        Assert.False(payments[3].IsPaid);
    }
}
