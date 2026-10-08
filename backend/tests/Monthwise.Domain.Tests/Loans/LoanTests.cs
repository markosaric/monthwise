using Monthwise.Domain.Common;
using Monthwise.Domain.Loans;
using static Monthwise.Domain.Tests.Loans.LoanTestData;

namespace Monthwise.Domain.Tests.Loans;

public class LoanTests
{
    [Fact]
    public void The_end_month_is_calculated_from_the_number_of_payments_and_back()
    {
        var loan = NewLoan(Details(start: new YearMonth(2026, 8), count: 60));

        Assert.Equal(new YearMonth(2031, 7), loan.EndMonth);
        Assert.Equal(60, Loan.PaymentCountBetween(new YearMonth(2026, 8), new YearMonth(2031, 7)));
        Assert.Equal(1, Loan.PaymentCountBetween(new YearMonth(2026, 8), new YearMonth(2026, 8)));
    }

    [Fact]
    public void Each_invalid_attribute_is_reported_on_its_field()
    {
        var details = Details(name: " ", paymentDay: 32, monthlyCents: 0, count: 0, residualCents: 0) with
        {
            CategoryId = Guid.Empty,
            SubcategoryId = Guid.Empty,
            LenderId = Guid.Empty,
            Description = new string('x', 201),
        };

        var result = Loan.Create(details, residualValueAllowed: true, Now);

        Assert.Equal(
            new[]
            {
                LoanErrors.NameRequired, LoanErrors.CategoryRequired, LoanErrors.SubcategoryRequired, LoanErrors.LenderRequired,
                LoanErrors.PaymentDayInvalid, LoanErrors.MonthlyPaymentNotPositive, LoanErrors.PaymentCountInvalid,
                LoanErrors.ResidualValueNotPositive, LoanErrors.DescriptionTooLong,
            },
            result.Errors);
    }

    [Fact]
    public void A_name_longer_than_50_characters_is_rejected()
    {
        var result = Loan.Create(Details(name: new string('a', 51)), true, Now);

        Assert.Equal(new[] { LoanErrors.NameTooLong }, result.Errors);
    }

    [Fact]
    public void A_standing_order_needs_an_account_and_a_bill_has_none()
    {
        var standingOrder = Details() with { AccountId = null };
        var bill = Details(method: PaymentMethod.Bill) with { AccountId = Guid.NewGuid() };

        Assert.Equal(new[] { LoanErrors.AccountRequired }, Loan.Create(standingOrder, true, Now).Errors);
        Assert.Equal(new[] { LoanErrors.AccountNotForBill }, Loan.Create(bill, true, Now).Errors);
    }

    [Fact]
    public void A_residual_value_needs_a_category_that_allows_it()
    {
        var result = Loan.Create(Details(residualCents: 1_000_000), residualValueAllowed: false, Now);

        Assert.Equal(new[] { LoanErrors.ResidualValueNotAllowed }, result.Errors);
    }

    [Fact]
    public void An_existing_residual_value_stays_allowed_after_the_category_is_unticked()
    {
        var loan = NewLoan(Details(residualCents: 1_000_000));

        var result = loan.Update(loan.Details with { Name = "Renamed" }, residualValueAllowed: false, PaidPayments.None, Now);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void An_edit_keeps_the_paid_payments_in_the_loan()
    {
        var loan = NewLoan();
        var paid = new PaidPayments(new YearMonth(2026, 8), new YearMonth(2026, 10), ResidualPaid: false);

        var laterStart = loan.Update(loan.Details with { StartMonth = new YearMonth(2026, 9) }, true, paid, Now);
        var shorter = loan.Update(loan.Details with { PaymentCount = 2 }, true, paid, Now);
        var fine = loan.Update(loan.Details with { MonthlyPayment = Money.FromCents(60_000), PaymentCount = 3 }, true, paid, Now);

        Assert.Equal(new[] { LoanErrors.StartAfterPaidPayment }, laterStart.Errors);
        Assert.Equal(new[] { LoanErrors.EndBeforePaidPayment }, shorter.Errors);
        Assert.True(fine.IsSuccess);
        Assert.Equal(new YearMonth(2026, 10), loan.EndMonth);
    }

    [Fact]
    public void Switching_to_a_bill_drops_the_account()
    {
        var loan = NewLoan();

        loan.Update(loan.Details with { PaymentMethod = PaymentMethod.Bill, AccountId = null }, true, PaidPayments.None, Now);

        Assert.Null(loan.AccountId);
    }

    [Fact]
    public void A_closed_loan_cannot_be_closed_or_changed_again()
    {
        var loan = NewLoan();
        var today = new DateOnly(2026, 10, 20);

        Assert.True(loan.Close(today, Now).IsSuccess);
        Assert.Equal(today, loan.ClosedOn);
        Assert.Equal(new[] { LoanErrors.LoanClosed }, loan.Close(today, Now).Errors);
        Assert.Equal(new[] { LoanErrors.LoanClosed }, loan.Update(loan.Details, true, PaidPayments.None, Now).Errors);
    }

    [Fact]
    public void A_payment_change_must_start_after_the_latest_paid_payment()
    {
        var loan = NewLoan();
        var latestPaid = new YearMonth(2026, 10);

        var inPaidMonth = loan.AddChange(Change(new YearMonth(2026, 10)), latestPaid, Now);
        var next = loan.AddChange(Change(new YearMonth(2026, 11)), latestPaid, Now);

        Assert.Equal(new[] { LoanErrors.ChangeNotAfterPaid }, inPaidMonth.Errors);
        Assert.True(next.IsSuccess);
    }

    [Fact]
    public void A_payment_change_needs_a_new_amount_or_day_within_the_loan()
    {
        var loan = NewLoan();

        var empty = loan.AddChange(new PaymentChangeDetails(new YearMonth(2026, 11), null, null, null), null, Now);
        var invalid = loan.AddChange(new PaymentChangeDetails(new YearMonth(2026, 11), Money.Zero, 0, null), null, Now);
        var outside = loan.AddChange(Change(new YearMonth(2027, 8)), null, Now);

        Assert.Equal(new[] { LoanErrors.ChangeNothingChanged }, empty.Errors);
        Assert.Equal(new[] { LoanErrors.ChangeMonthlyPaymentNotPositive, LoanErrors.ChangePaymentDayInvalid }, invalid.Errors);
        Assert.Equal(new[] { LoanErrors.ChangeOutsideLoan }, outside.Errors);
    }

    [Fact]
    public void Two_payment_changes_cannot_start_in_the_same_month()
    {
        var loan = NewLoan();
        loan.AddChange(Change(new YearMonth(2026, 11)), null, Now);

        Assert.Equal(new[] { LoanErrors.ChangeMonthInUse }, loan.AddChange(Change(new YearMonth(2026, 11)), null, Now).Errors);
    }

    [Fact]
    public void A_payment_change_cannot_be_edited_or_deleted_once_a_covered_payment_is_paid()
    {
        var loan = NewLoan();
        var change = loan.AddChange(Change(new YearMonth(2026, 11)), null, Now).Value;
        var latestPaid = new YearMonth(2026, 11);

        Assert.Equal(new[] { LoanErrors.ChangeCoversPaidPayment }, loan.UpdateChange(change.Id, Change(new YearMonth(2026, 12)), latestPaid, Now).Errors);
        Assert.Equal(new[] { LoanErrors.ChangeCoversPaidPayment }, loan.RemoveChange(change.Id, latestPaid, Now).Errors);
        Assert.True(loan.RemoveChange(change.Id, new YearMonth(2026, 10), Now).IsSuccess);
        Assert.Empty(loan.Changes);
    }

    [Fact]
    public void The_newest_change_decides_the_amount_and_the_day_of_a_month()
    {
        var loan = NewLoan();
        loan.AddChange(new PaymentChangeDetails(new YearMonth(2026, 11), Money.FromCents(52_000), null, "Rate change"), null, Now);
        loan.AddChange(new PaymentChangeDetails(new YearMonth(2027, 2), null, 20, null), null, Now);
        loan.AddChange(new PaymentChangeDetails(new YearMonth(2027, 4), Money.FromCents(48_000), 10, null), null, Now);

        Assert.Equal(Money.FromCents(50_000), loan.MonthlyPaymentFor(new YearMonth(2026, 10)));
        Assert.Equal(Money.FromCents(52_000), loan.MonthlyPaymentFor(new YearMonth(2027, 3)));
        Assert.Equal(20, loan.PaymentDayFor(new YearMonth(2027, 3)));
        Assert.Equal(15, loan.PaymentDayFor(new YearMonth(2027, 1)));
        Assert.Equal(Money.FromCents(48_000), loan.MonthlyPaymentFor(new YearMonth(2027, 5)));
        Assert.Equal(10, loan.PaymentDayFor(new YearMonth(2027, 5)));
    }

    private static PaymentChangeDetails Change(YearMonth from) => new(from, Money.FromCents(55_000), null, null);
}
