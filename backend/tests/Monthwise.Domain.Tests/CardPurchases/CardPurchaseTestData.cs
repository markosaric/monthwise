using Monthwise.Domain.CardPurchases;
using Monthwise.Domain.Cards;
using Monthwise.Domain.Common;

namespace Monthwise.Domain.Tests.CardPurchases;

internal static class CardPurchaseTestData
{
    public static readonly DateTimeOffset Now = new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);
    public static readonly Guid PayingAccountId = Guid.NewGuid();

    public static CardBillingDetails Billing(
        int statementDay = 5, int paymentDay = 20, bool direct = false, int maxInstallments = 36, Guid? payingAccountId = null) =>
        new(statementDay, paymentDay, payingAccountId ?? PayingAccountId, direct, maxInstallments);

    /// <summary>A credit card with statement on the 5th and payment on the 20th, unless given other billing details.</summary>
    public static Card CreditCard(CardBillingDetails? billing = null) =>
        Card.Create(
            new CardDetails(
                "Family Visa", Guid.NewGuid(), Guid.NewGuid(), null, new YearMonth(2030, 12), CardType.Credit,
                IsPrimary: false, DebtLimit: null, AllowsInstallments: false, billing ?? Billing()),
            null,
            null,
            Now).Value;

    public static CardPurchaseDetails Purchase(
        Guid cardId,
        DateOnly? date = null,
        long amountCents = 10_000,
        int? installments = 3,
        bool postpone = false,
        string description = "Washing machine") =>
        new(
            cardId,
            description,
            date ?? new DateOnly(2026, 10, 1),
            Money.FromCents(amountCents),
            InInstallments: installments is not null,
            installments,
            postpone,
            CategoryId: null);

    public static CardPurchase NewPurchase(CardPurchaseDetails details, int maxInstallments = 36) =>
        CardPurchase.Create(details, maxInstallments, Now).Value;
}
