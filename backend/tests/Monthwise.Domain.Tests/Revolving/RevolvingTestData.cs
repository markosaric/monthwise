using Monthwise.Domain.Cards;
using Monthwise.Domain.Common;
using Monthwise.Domain.Revolving;

namespace Monthwise.Domain.Tests.Revolving;

internal static class RevolvingTestData
{
    public static readonly DateTimeOffset Now = new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);
    public static readonly Guid PayingAccountId = Guid.NewGuid();

    /// <summary>8.33 % or at least 13.27 EUR, 12.75 % a year, bill on the 5th, due on the 20th.</summary>
    public static RevolvingDetails Terms(
        int minPaymentBasisPoints = 833,
        long minPaymentCents = 1_327,
        int interestBasisPoints = 1_275,
        int? interestPartBasisPoints = null,
        int billDay = 5,
        int dueDay = 20,
        bool direct = false) =>
        new(minPaymentBasisPoints, Money.FromCents(minPaymentCents), interestBasisPoints, interestPartBasisPoints, billDay, dueDay,
            PayingAccountId, direct);

    public static CardDetails Details(RevolvingDetails? terms, long? debtLimitCents = 300_000, bool isPrimary = false) =>
        new(
            "Revolving Visa", Guid.NewGuid(), Guid.NewGuid(), null, new YearMonth(2030, 12), CardType.Revolving,
            isPrimary, debtLimitCents is { } cents ? Money.FromCents(cents) : null, Revolving: terms);

    public static Card RevolvingCard(RevolvingDetails? terms = null) => Card.Create(Details(terms ?? Terms()), null, null, Now).Value;

    public static DebtEntry Starting(Card card, DateOnly date, long debtCents) =>
        DebtEntry.Create(card.Id, DebtEntryKind.Starting, date, Money.FromCents(debtCents), null, Now).Value;

    public static DebtEntry Correction(Card card, DateOnly date, long debtCents) =>
        DebtEntry.Create(card.Id, DebtEntryKind.Correction, date, Money.FromCents(debtCents), null, Now).Value;

    public static ExtraPayment Payment(Card card, DateOnly date, long amountCents) =>
        ExtraPayment.Create(new ExtraPaymentDetails(card.Id, PayingAccountId, date, Money.FromCents(amountCents), null), date, Now).Value;

    public static RevolvingCharge Charge(Card card, DateOnly date, long amountCents) =>
        new(Guid.NewGuid(), card.Id, RevolvingChargeKind.Expense, date, Money.FromCents(amountCents));

    public static RevolvingDebtCalculator Calculate(
        Card card,
        DateOnly today,
        IEnumerable<DebtEntry>? entries = null,
        IEnumerable<RevolvingCharge>? charges = null,
        IEnumerable<ExtraPayment>? payments = null,
        IEnumerable<RevolvingBill>? stored = null,
        YearMonth? horizon = null,
        DateOnly? chargesStopOn = null) =>
        new(card, entries ?? [], charges ?? [], payments ?? [], stored ?? [], today, horizon ?? YearMonth.FromDate(today).Next(), chargesStopOn);
}
