using Monthwise.Domain.Cards;
using Monthwise.Domain.Common;
using static Monthwise.Domain.Tests.CardPurchases.CardPurchaseTestData;

namespace Monthwise.Domain.Tests.CardPurchases;

public class CardBillingRulesTests
{
    private static Result<Card> Create(CardType type, CardBillingDetails? billing, bool allowsInstallments = false) =>
        Card.Create(
            new CardDetails("Card", Guid.NewGuid(), Guid.NewGuid(), null, new YearMonth(2030, 12), type, false, null, allowsInstallments, billing),
            null,
            null,
            Now);

    [Theory]
    [InlineData(CardType.Credit, false)]
    [InlineData(CardType.Charge, false)]
    [InlineData(CardType.Debit, true)]
    public void Cards_that_allow_installments_need_billing_details(CardType type, bool allowsInstallments)
    {
        var result = Create(type, billing: null, allowsInstallments);

        Assert.Equal(CardErrors.BillingRequired, Assert.Single(result.Errors));
    }

    [Theory]
    [InlineData(CardType.Prepaid)]
    [InlineData(CardType.Debit)]
    public void Other_cards_have_no_billing_details(CardType type)
    {
        var card = Create(type, Billing()).Value;

        Assert.False(card.HasBillingDetails);
        Assert.Empty(card.BillingTerms);
        Assert.Null(card.PayingAccountId);
    }

    [Theory]
    [InlineData(0, 20, "statementDay")]
    [InlineData(31, 31, "statementDay")]
    [InlineData(5, 1, "paymentDay")]
    [InlineData(5, 32, "paymentDay")]
    [InlineData(20, 20, "paymentDay")]
    [InlineData(20, 5, "paymentDay")]
    public void Days_must_be_valid_and_the_payment_later_than_the_statement(int statementDay, int paymentDay, string field)
    {
        var result = Create(CardType.Credit, Billing(statementDay, paymentDay));

        Assert.Equal(field, Assert.Single(result.Errors).Field);
    }

    [Fact]
    public void Paying_account_and_max_installments_are_required()
    {
        var result = Create(CardType.Credit, new CardBillingDetails(5, 20, Guid.Empty, false, 18));

        Assert.Equal(["payingAccountId", "maxInstallments"], result.Errors.Select(e => e.Field));
    }

    [Fact]
    public void Unticking_installments_on_a_debit_card_clears_its_billing_details()
    {
        var card = Create(CardType.Debit, Billing(), allowsInstallments: true).Value;
        Assert.True(card.HasBillingDetails);

        var details = new CardDetails("Card", card.IssuerId, card.BankId, null, card.ValidThrough, CardType.Debit, false, null, false, Billing());
        card.Update(details, null, null, new DateOnly(2026, 10, 20), Now);

        Assert.False(card.HasBillingDetails);
        Assert.Empty(card.BillingTerms);
        Assert.Null(card.MaxInstallments);
    }
}
