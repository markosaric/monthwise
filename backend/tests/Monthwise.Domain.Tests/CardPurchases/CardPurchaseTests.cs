using Monthwise.Domain.CardPurchases;
using Monthwise.Domain.Common;
using static Monthwise.Domain.Tests.CardPurchases.CardPurchaseTestData;

namespace Monthwise.Domain.Tests.CardPurchases;

public class CardPurchaseTests
{
    private static readonly Monthwise.Domain.Cards.Card Card = CreditCard();

    [Fact]
    public void The_remaining_cents_go_to_the_last_installment()
    {
        var purchase = NewPurchase(Purchase(Card.Id, amountCents: 10_000, installments: 3));

        var installments = purchase.Installments(Card.BillingCalendar()!);

        Assert.Equal([3_333L, 3_333L, 3_334L], installments.Select(i => i.Amount.Cents));
        Assert.Equal([1, 2, 3], installments.Select(i => i.Number));
        Assert.All(installments, i => Assert.Equal(3, i.Total));
    }

    [Fact]
    public void Installments_start_in_the_billing_period_of_the_purchase_date()
    {
        var calendar = Card.BillingCalendar()!;

        var onStatement = NewPurchase(Purchase(Card.Id, date: new DateOnly(2026, 10, 5))).Installments(calendar);
        var dayAfter = NewPurchase(Purchase(Card.Id, date: new DateOnly(2026, 10, 6))).Installments(calendar);

        Assert.Equal([new YearMonth(2026, 10), new YearMonth(2026, 11), new YearMonth(2026, 12)], onStatement.Select(i => i.PaymentMonth));
        Assert.Equal(new YearMonth(2026, 11), dayAfter[0].PaymentMonth);
    }

    [Fact]
    public void A_postponed_first_installment_is_one_billing_period_later()
    {
        var installments = NewPurchase(Purchase(Card.Id, date: new DateOnly(2026, 10, 1), postpone: true)).Installments(Card.BillingCalendar()!);

        Assert.Equal(new YearMonth(2026, 11), installments[0].PaymentMonth);
    }

    [Fact]
    public void A_purchase_without_installments_is_one_charge_of_the_whole_amount()
    {
        var purchase = NewPurchase(Purchase(Card.Id, amountCents: 12_345, installments: null, postpone: true));

        var installment = Assert.Single(purchase.Installments(Card.BillingCalendar()!));

        Assert.False(purchase.InInstallments);
        Assert.False(purchase.PostponeFirst);
        Assert.Equal(12_345, installment.Amount.Cents);
        Assert.Equal(new YearMonth(2026, 10), installment.PaymentMonth);
    }

    [Theory]
    [InlineData(1, 36)]
    [InlineData(37, 36)]
    [InlineData(24, 12)]
    public void The_number_of_installments_is_from_two_to_the_card_limit(int count, int max)
    {
        var result = CardPurchase.Create(Purchase(Card.Id, installments: count), max, Now);

        Assert.Equal("installmentCount", Assert.Single(result.Errors).Field);
    }

    [Fact]
    public void Description_card_and_a_positive_amount_are_required()
    {
        var result = CardPurchase.Create(Purchase(Guid.Empty, amountCents: 0, description: " "), 36, Now);

        Assert.Equal(["cardId", "description", "amountCents"], result.Errors.Select(e => e.Field));
    }

    [Fact]
    public void A_purchase_or_an_installment_is_reversed_only_once()
    {
        var purchase = NewPurchase(Purchase(Card.Id));
        var today = new DateOnly(2026, 10, 25);

        Assert.True(purchase.ReverseInstallment(2, today, "cancelled", Now).IsSuccess);
        Assert.Equal(CardPurchaseErrors.InstallmentReversed, Assert.Single(purchase.ReverseInstallment(2, today, null, Now).Errors));
        Assert.Equal(CardPurchaseErrors.InstallmentNotFound, Assert.Single(purchase.ReverseInstallment(4, today, null, Now).Errors));
        Assert.True(purchase.Reverse(today, "returned", Now).IsSuccess);
        Assert.Equal(CardPurchaseErrors.Reversed, Assert.Single(purchase.Reverse(today, null, Now).Errors));
        Assert.Equal(CardPurchaseErrors.Reversed, Assert.Single(purchase.Update(Purchase(Card.Id), 36, Now).Errors));
    }

    [Fact]
    public void Editing_to_fewer_installments_drops_reversals_of_installments_it_no_longer_has()
    {
        var purchase = NewPurchase(Purchase(Card.Id, installments: 6));
        purchase.ReverseInstallment(5, new DateOnly(2026, 10, 2), null, Now);

        Assert.True(purchase.Update(Purchase(Card.Id, installments: 3), 36, Now).IsSuccess);

        Assert.Empty(purchase.Reversals);
    }
}
