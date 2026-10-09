using Monthwise.Domain.Common;

namespace Monthwise.Domain.Cards;

/// <summary>
/// The statement day and payment day of a card from <see cref="FromMonth"/> on (installments FR-1,
/// FR-2). A card keeps every version, so a change of days applies only to the next payment months
/// and earlier card payments keep their dates.
/// </summary>
public sealed class CardBillingTerms
{
    public const int MinStatementDay = 1;
    public const int MaxStatementDay = 30;
    public const int MinPaymentDay = 2;
    public const int MaxPaymentDay = 31;

    /// <summary>The month of the first terms of a card: they cover every earlier purchase too.</summary>
    public static readonly YearMonth Beginning = new(1, 1);

    private CardBillingTerms()
    {
    }

    public Guid Id { get; private set; }
    public Guid CardId { get; private set; }
    public YearMonth FromMonth { get; private set; }
    public int StatementDay { get; private set; }
    public int PaymentDay { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    internal static CardBillingTerms Create(Guid cardId, YearMonth fromMonth, int statementDay, int paymentDay, DateTimeOffset now) =>
        new()
        {
            Id = Guid.CreateVersion7(now),
            CardId = cardId,
            FromMonth = fromMonth,
            StatementDay = statementDay,
            PaymentDay = paymentDay,
            CreatedAt = now,
        };

    internal bool HasDays(int statementDay, int paymentDay) => StatementDay == statementDay && PaymentDay == paymentDay;

    internal void ChangeDays(int statementDay, int paymentDay)
    {
        StatementDay = statementDay;
        PaymentDay = paymentDay;
    }
}

/// <summary>
/// What the user enters for a card that allows monthly installments (installments FR-1, B1 to B5).
/// The paying account must be a usable primary account; the caller checks that.
/// </summary>
public sealed record CardBillingDetails(
    int StatementDay,
    int PaymentDay,
    Guid PayingAccountId,
    bool PaysFromAccountDirectly,
    int MaxInstallments)
{
    /// <summary>The numbers of installments a card can allow at most (B5).</summary>
    public static readonly IReadOnlyList<int> MaxInstallmentsChoices = [12, 24, 36];
}
