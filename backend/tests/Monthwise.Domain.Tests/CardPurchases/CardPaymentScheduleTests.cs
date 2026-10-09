using Monthwise.Domain.CardPurchases;
using Monthwise.Domain.Common;
using static Monthwise.Domain.Tests.CardPurchases.CardPurchaseTestData;

namespace Monthwise.Domain.Tests.CardPurchases;

public class CardPaymentScheduleTests
{
    private static readonly YearMonth October = new(2026, 10);
    private static readonly YearMonth November = new(2026, 11);
    private static readonly DateOnly Today = new(2026, 10, 1);

    [Fact]
    public void A_card_payment_sums_the_installments_and_purchases_of_its_billing_period()
    {
        var card = CreditCard();
        var inInstallments = NewPurchase(Purchase(card.Id, amountCents: 10_000, installments: 3));
        var inFull = NewPurchase(Purchase(card.Id, date: new DateOnly(2026, 10, 3), amountCents: 2_500, installments: null));
        var nextPeriod = NewPurchase(Purchase(card.Id, date: new DateOnly(2026, 10, 6), amountCents: 1_000, installments: null));

        var schedule = new CardPaymentSchedule(card, [inInstallments, inFull, nextPeriod], []);

        var october = schedule.Find(October, Today)!;
        Assert.Equal(3_333 + 2_500, october.Amount.Cents);
        Assert.Equal(2, october.LineCount);
        Assert.Equal(new DateOnly(2026, 9, 6), october.PeriodFrom);
        Assert.Equal(new DateOnly(2026, 10, 5), october.StatementDate);
        Assert.Equal(new DateOnly(2026, 10, 20), october.PaymentDate);
        Assert.Equal(3_333 + 1_000, schedule.Find(November, Today)!.Amount.Cents);
        Assert.Equal(3, schedule.Payments(Today).Count);
    }

    [Fact]
    public void A_connected_card_payment_is_paid_after_its_payment_date_and_not_on_it()
    {
        var card = CreditCard(Billing(direct: true));
        var schedule = new CardPaymentSchedule(card, [NewPurchase(Purchase(card.Id))], []);

        var onPaymentDate = schedule.Find(October, new DateOnly(2026, 10, 20))!;
        var dayAfter = schedule.Find(October, new DateOnly(2026, 10, 21))!;

        Assert.False(onPaymentDate.IsPaid);
        Assert.True(dayAfter.IsPaid);
        Assert.True(dayAfter.PaidAutomatically);
        Assert.Equal(new DateOnly(2026, 10, 20), dayAfter.PaidDate);
        Assert.Equal(PayingAccountId, dayAfter.AccountId);
    }

    [Fact]
    public void A_card_payment_of_a_card_that_is_not_connected_is_paid_only_when_ticked()
    {
        var card = CreditCard();
        var stored = CardPayment.Create(card.Id, October, Now);
        var account = Guid.NewGuid();
        stored.MarkPaid(new DateOnly(2026, 10, 22), account, new DateOnly(2026, 10, 5), Now);
        var purchases = new[] { NewPurchase(Purchase(card.Id)) };

        var expected = new CardPaymentSchedule(card, purchases, []).Find(October, new DateOnly(2026, 11, 1))!;
        var paid = new CardPaymentSchedule(card, purchases, [stored]).Find(October, new DateOnly(2026, 11, 1))!;

        Assert.False(expected.IsPaid);
        Assert.True(expected.IsOverdue(new DateOnly(2026, 11, 1)));
        Assert.True(paid.IsPaid);
        Assert.Equal(account, paid.AccountId);
        Assert.Equal(new DateOnly(2026, 10, 22), paid.MonthDate);
    }

    [Fact]
    public void An_unticked_automatic_card_payment_stays_expected()
    {
        var card = CreditCard(Billing(direct: true));
        var stored = CardPayment.Create(card.Id, October, Now);
        stored.MarkUnpaid(Now);

        var payment = new CardPaymentSchedule(card, [NewPurchase(Purchase(card.Id))], [stored]).Find(October, new DateOnly(2026, 11, 1))!;

        Assert.False(payment.IsPaid);
    }

    [Fact]
    public void A_paying_account_that_is_no_longer_usable_stops_automatic_payments()
    {
        var card = CreditCard(Billing(direct: true));
        var schedule = new CardPaymentSchedule(card, [NewPurchase(Purchase(card.Id))], [], chargesStopOn: new DateOnly(2026, 11, 1));

        var october = schedule.Find(October, new DateOnly(2026, 12, 1))!;
        var november = schedule.Find(November, new DateOnly(2026, 12, 1))!;

        Assert.True(october.IsPaid);
        Assert.False(november.IsPaid);
        Assert.True(november.NeedsAccount);
        Assert.True(schedule.NeedsAccount);
    }

    [Fact]
    public void A_reversed_installment_not_yet_charged_is_removed()
    {
        var card = CreditCard();
        var purchase = NewPurchase(Purchase(card.Id));
        purchase.ReverseInstallment(2, new DateOnly(2026, 10, 25), null, Now);

        var schedule = new CardPaymentSchedule(card, [purchase], []);

        Assert.Null(schedule.Find(November, Today));
        Assert.Equal(3_333, schedule.Find(October, Today)!.Amount.Cents);
        var summary = schedule.Summary(purchase, new DateOnly(2026, 10, 25));
        Assert.Equal(1, summary.InstallmentsCharged);
        Assert.Equal(1, summary.InstallmentsLeft);
        Assert.Equal(3_334, summary.RemainingAmount.Cents);
    }

    [Fact]
    public void A_reversed_installment_already_charged_is_credited_in_the_next_card_payment()
    {
        var card = CreditCard();
        var purchase = NewPurchase(Purchase(card.Id));
        purchase.ReverseInstallment(1, new DateOnly(2026, 10, 25), "refund", Now);

        var schedule = new CardPaymentSchedule(card, [purchase], []);

        Assert.Equal(3_333, schedule.Find(October, Today)!.Amount.Cents);
        var november = schedule.Find(November, Today)!;
        Assert.Equal(3_333 - 3_333, november.Amount.Cents);
        var credit = Assert.Single(schedule.LinesOf(November), l => l.Kind == CardPaymentLineKind.Credit);
        Assert.Equal(-3_333, credit.Amount.Cents);
        var installment = schedule.InstallmentsOf(purchase, Today)[0];
        Assert.True(installment.IsCharged);
        Assert.Equal(November, installment.CreditMonth);
    }

    [Fact]
    public void A_reversed_purchase_removes_its_future_installments_and_credits_the_charged_ones()
    {
        var card = CreditCard();
        var purchase = NewPurchase(Purchase(card.Id, amountCents: 30_000));
        // October's card payment (20 October) was charged; November and December were not.
        purchase.Reverse(new DateOnly(2026, 11, 3), "returned", Now);

        var schedule = new CardPaymentSchedule(card, [purchase], []);

        Assert.Equal(10_000, schedule.Find(October, Today)!.Amount.Cents);
        Assert.Equal(-10_000, schedule.Find(November, Today)!.Amount.Cents);
        Assert.Null(schedule.Find(new YearMonth(2026, 12), Today));
        Assert.Equal(Money.Zero, schedule.Summary(purchase, new DateOnly(2026, 11, 3)).RemainingAmount);
        Assert.False(schedule.HasInstallmentsLeft(new DateOnly(2026, 11, 3)));
    }

    [Fact]
    public void A_card_without_billing_details_has_no_card_payments()
    {
        var card = CreditCard();
        var debit = Monthwise.Domain.Cards.Card.Create(
            new Monthwise.Domain.Cards.CardDetails(
                "Debit", Guid.NewGuid(), Guid.NewGuid(), null, new YearMonth(2030, 12), Monthwise.Domain.Cards.CardType.Debit,
                false, null),
            null,
            null,
            Now).Value;

        var schedule = new CardPaymentSchedule(debit, [NewPurchase(Purchase(card.Id))], []);

        Assert.Empty(schedule.Payments(Today));
        Assert.Null(schedule.Calendar);
    }
}
