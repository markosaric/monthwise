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
    public void A_bill_adds_the_daily_interest_since_the_previous_bill_and_asks_for_the_minimum_of_the_debt_with_interest()
    {
        var card = RevolvingCard();

        // From 6 September up to and including the bill day on 5 October: 30 days of 1,000.00 EUR.
        var calculator = Calculate(card, October10, entries: [Starting(card, new DateOnly(2026, 9, 6), 100_000)]);

        Assert.Null(calculator.Bill(new YearMonth(2026, 9)));
        var bill = calculator.Bill(October)!;
        Assert.Equal(new DateOnly(2026, 10, 5), bill.BillDate);
        Assert.Equal(new DateOnly(2026, 10, 20), bill.DueDate);
        Assert.Equal(1_048, bill.Interest.Cents); // 30 × 1,000.00 EUR × 12.75 % ÷ 365 = 10.479 EUR
        Assert.Equal(101_048, bill.Debt.Cents);
        Assert.Equal(8_417, bill.Minimum.Cents); // 8.33 % of 1,010.48 EUR
        Assert.False(bill.IsEstimated);
        Assert.False(bill.IsPaid);
        Assert.False(bill.IsOverdue(October10));
        Assert.Equal(101_048, calculator.Debt.Cents);
    }

    [Fact]
    public void The_first_bill_counts_only_the_days_since_the_starting_debt()
    {
        var card = RevolvingCard();

        // 30 September up to and including 5 October: 6 days.
        var calculator = Calculate(card, October10, entries: [Starting(card, new DateOnly(2026, 9, 30), 100_000)]);

        Assert.Equal(210, calculator.Bill(October)!.Interest.Cents); // 6 × 1,000.00 EUR × 12.75 % ÷ 365 = 2.096 EUR
    }

    [Fact]
    public void A_payment_during_the_month_lowers_the_interest_from_its_date()
    {
        var card = RevolvingCard();

        // 15 days of 1,000.00 EUR (6 to 20 September), then 15 days of 500.00 EUR (21 September to 5 October).
        var calculator = Calculate(
            card, October10, entries: [Starting(card, new DateOnly(2026, 9, 6), 100_000)],
            payments: [Payment(card, new DateOnly(2026, 9, 21), 50_000)]);

        Assert.Equal(786, calculator.Bill(October)!.Interest.Cents); // 22,500.00 EUR-days × 12.75 % ÷ 365 = 7.860 EUR
    }

    [Fact]
    public void Next_months_bill_is_estimated_with_daily_interest_as_if_only_the_minimum_is_paid_on_the_due_date()
    {
        var card = RevolvingCard();

        var calculator = Calculate(card, October10, entries: [Starting(card, new DateOnly(2026, 9, 6), 100_000)]);

        var november = calculator.Bill(November)!;
        Assert.True(november.IsEstimated);
        // 6 to 19 October: 14 days of 1,010.48 EUR; 20 October to 5 November: 17 days of 926.31 EUR.
        var centDays = (14 * 101_048L) + (17 * (101_048L - 8_417));
        Assert.Equal(1_044, november.Interest.Cents);
        Assert.Equal(RevolvingMath.DailyInterest(centDays, card.RevolvingTermVersions[0]), november.Interest);
        Assert.Equal(101_048 - 8_417 + 1_044, november.Debt.Cents);
        Assert.Equal(101_048, calculator.Debt.Cents); // the assumed payment is not counted before its date
    }

    [Fact]
    public void A_starting_debt_is_the_debt_at_the_start_of_its_date_so_a_payment_on_that_date_lowers_it()
    {
        var card = RevolvingCard();
        var date = new DateOnly(2026, 10, 7);
        var starting = Starting(card, date, 150_000);
        var payment = Payment(card, date, 40_000);

        var calculator = Calculate(card, date, entries: [starting], payments: [payment]);

        Assert.Equal(110_000, calculator.Debt.Cents);
        Assert.Equal(
            [RevolvingMovementKind.Starting, RevolvingMovementKind.ExtraPayment],
            calculator.Movements.Where(m => m.Date == date).Select(m => m.Kind));
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
            charges: [Charge(card, new DateOnly(2026, 9, 10), 5_000), Charge(card, new DateOnly(2026, 9, 15), 1_000)],
            payments: [Payment(card, new DateOnly(2026, 9, 15), 3_000)]);

        Assert.Equal(200_000, calculator.Debt.Cents);
        Assert.Equal(200_000 - 103_000, calculator.Movements.Single(m => m.SourceId == correction.Id).Change.Cents);
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
