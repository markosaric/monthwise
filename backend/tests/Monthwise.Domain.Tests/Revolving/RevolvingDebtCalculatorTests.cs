using Monthwise.Domain.Common;
using Monthwise.Domain.Revolving;
using static Monthwise.Domain.Tests.Revolving.RevolvingTestData;

namespace Monthwise.Domain.Tests.Revolving;

public class RevolvingDebtCalculatorTests
{
    private static readonly YearMonth October = new(2026, 10);
    private static readonly YearMonth November = new(2026, 11);
    private static readonly DateOnly October10 = new(2026, 10, 10);

    [Fact]
    public void A_card_without_debt_gets_no_monthly_bill()
    {
        var card = RevolvingCard();

        var calculator = Calculate(card, October10, entries: [Starting(card, new DateOnly(2026, 9, 1), 0)]);

        Assert.Empty(calculator.Bills);
        Assert.Equal(Money.Zero, calculator.Debt);
    }

    [Fact]
    public void A_bill_adds_the_interest_of_the_month_and_asks_for_the_minimum_of_the_debt_with_interest()
    {
        var card = RevolvingCard();

        var calculator = Calculate(card, October10, entries: [Starting(card, new DateOnly(2026, 9, 30), 100_000)]);

        var bill = calculator.Bill(October)!;
        Assert.Equal(new DateOnly(2026, 10, 5), bill.BillDate);
        Assert.Equal(new DateOnly(2026, 10, 20), bill.DueDate);
        Assert.Equal(1_063, bill.Interest.Cents);
        Assert.Equal(101_063, bill.Debt.Cents);
        Assert.Equal(8_419, bill.Minimum.Cents); // 8.33 % of 1,010.63 EUR
        Assert.False(bill.IsEstimated);
        Assert.False(bill.IsPaid);
        Assert.False(bill.IsOverdue(October10));
        Assert.Equal(101_063, calculator.Debt.Cents);
    }

    [Fact]
    public void Next_months_bill_is_estimated_as_if_only_the_minimum_is_paid_on_the_due_date()
    {
        var card = RevolvingCard();

        var calculator = Calculate(card, October10, entries: [Starting(card, new DateOnly(2026, 9, 30), 100_000)]);

        var november = calculator.Bill(November)!;
        Assert.True(november.IsEstimated);
        Assert.Equal(101_063 - 8_419 + 984, november.Debt.Cents);
        Assert.Equal(101_063, calculator.Debt.Cents); // the assumed payment is not counted before its date
    }

    [Fact]
    public void A_charge_on_the_bill_day_is_in_that_bill()
    {
        var card = RevolvingCard(Terms(interestBasisPoints: 0));

        var calculator = Calculate(card, October10, charges: [Charge(card, new DateOnly(2026, 10, 5), 50_000)]);

        Assert.Equal(50_000, calculator.Bill(October)!.Debt.Cents);
    }

    [Fact]
    public void An_extra_payment_after_the_bill_day_lowers_only_the_next_bill()
    {
        var card = RevolvingCard(Terms(interestBasisPoints: 0));
        var starting = Starting(card, new DateOnly(2026, 9, 30), 100_000);
        var without = Calculate(card, October10, entries: [starting]);

        var with = Calculate(card, October10, entries: [starting], payments: [Payment(card, new DateOnly(2026, 10, 8), 20_000)]);

        Assert.Equal(without.Bill(October)!.Minimum, with.Bill(October)!.Minimum);
        Assert.Equal(80_000, with.Debt.Cents);
        Assert.Equal(without.Bill(November)!.Debt.Cents - 20_000, with.Bill(November)!.Debt.Cents);
    }

    [Fact]
    public void Paying_the_whole_debt_stops_the_monthly_bills()
    {
        var card = RevolvingCard();

        var calculator = Calculate(
            card, October10, entries: [Starting(card, new DateOnly(2026, 9, 30), 100_000)],
            payments: [Payment(card, new DateOnly(2026, 10, 1), 100_000)]);

        Assert.Equal(Money.Zero, calculator.Debt);
        Assert.Empty(calculator.Bills);
    }

    [Fact]
    public void A_connected_card_pays_the_minimum_automatically_after_the_due_date_and_not_on_it()
    {
        var card = RevolvingCard(Terms(interestBasisPoints: 0, direct: true));
        var starting = Starting(card, new DateOnly(2026, 9, 30), 100_000);

        var onDueDate = Calculate(card, new DateOnly(2026, 10, 20), entries: [starting]).Bill(October)!;
        var dayAfter = Calculate(card, new DateOnly(2026, 10, 21), entries: [starting]);

        Assert.False(onDueDate.IsPaid);
        var bill = dayAfter.Bill(October)!;
        Assert.True(bill.IsPaid);
        Assert.True(bill.PaidAutomatically);
        Assert.Equal(new DateOnly(2026, 10, 20), bill.PaidDate);
        Assert.Equal(PayingAccountId, bill.AccountId);
        Assert.Equal(100_000 - 8_330, dayAfter.Debt.Cents);
    }

    [Fact]
    public void A_connected_card_whose_account_stopped_needs_an_account_and_is_not_paid()
    {
        var card = RevolvingCard(Terms(direct: true));

        var calculator = Calculate(
            card, new DateOnly(2026, 10, 25), entries: [Starting(card, new DateOnly(2026, 9, 30), 100_000)],
            chargesStopOn: new DateOnly(2026, 10, 15));

        var bill = calculator.Bill(October)!;
        Assert.False(bill.IsPaid);
        Assert.True(bill.NeedsAccount);
        Assert.True(bill.IsOverdue(new DateOnly(2026, 10, 25)));
    }

    [Fact]
    public void A_paid_bill_and_a_correction_of_the_bank_bill_change_the_debt()
    {
        var card = RevolvingCard();
        var stored = RevolvingBill.Create(card.Id, October, Now);
        stored.Correct(Money.FromCents(9_000), Money.FromCents(1_000), Now);
        stored.MarkPaid(new DateOnly(2026, 10, 18), Guid.NewGuid(), Money.FromCents(20_000), new DateOnly(2026, 10, 5), Now);

        var calculator = Calculate(
            card, new DateOnly(2026, 10, 25), entries: [Starting(card, new DateOnly(2026, 9, 30), 100_000)], stored: [stored]);

        var bill = calculator.Bill(October)!;
        Assert.True(bill.IsCorrected);
        Assert.Equal(1_000, bill.Interest.Cents);
        Assert.Equal(9_000, bill.Minimum.Cents);
        Assert.Equal(20_000, bill.Amount.Cents);
        Assert.Equal(101_000 - 20_000, calculator.Debt.Cents);
    }

    [Fact]
    public void A_correction_sets_the_debt_at_the_end_of_its_date()
    {
        var card = RevolvingCard(Terms(interestBasisPoints: 0));
        var correction = Correction(card, new DateOnly(2026, 9, 15), 200_000);

        var calculator = Calculate(
            card, new DateOnly(2026, 9, 20),
            entries: [Starting(card, new DateOnly(2026, 9, 1), 100_000), correction],
            charges: [Charge(card, new DateOnly(2026, 9, 10), 5_000), Charge(card, new DateOnly(2026, 9, 15), 1_000)]);

        Assert.Equal(200_000, calculator.Debt.Cents);
        Assert.Equal(200_000 - 106_000, calculator.Movements.Single(m => m.SourceId == correction.Id).Change.Cents);
    }

    [Fact]
    public void A_transfer_moves_the_debt_to_the_replacement_card()
    {
        var old = RevolvingCard();
        var replacement = RevolvingCard();
        var (moveOut, moveIn) = DebtEntry.CreateTransfer(old.Id, replacement.Id, new DateOnly(2026, 10, 10), Money.FromCents(50_000), Now);
        var entries = new[] { Starting(old, new DateOnly(2026, 10, 6), 50_000), moveOut, moveIn };

        Assert.Equal(Money.Zero, Calculate(old, new DateOnly(2026, 10, 11), entries: entries).Debt);
        var newCard = Calculate(replacement, new DateOnly(2026, 10, 11), entries: entries);
        Assert.Equal(50_000, newCard.Debt.Cents);
        Assert.NotNull(newCard.Bill(November));
    }
}
