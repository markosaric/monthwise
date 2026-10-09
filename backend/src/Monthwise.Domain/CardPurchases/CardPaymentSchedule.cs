using Monthwise.Domain.Cards;
using Monthwise.Domain.Common;

namespace Monthwise.Domain.CardPurchases;

/// <summary>What a line of a card payment is (FR-8).</summary>
public enum CardPaymentLineKind
{
    /// <summary>A purchase paid in full with one card payment.</summary>
    FullPurchase,

    /// <summary>One installment of a purchase in installments.</summary>
    Installment,

    /// <summary>A reversed installment that was already charged, credited back (negative).</summary>
    Credit,
}

/// <summary>One line of a card payment: a purchase, an installment, or a credit from a reversal (FR-8).</summary>
public sealed record CardPaymentLine(
    Guid PurchaseId,
    YearMonth PaymentMonth,
    CardPaymentLineKind Kind,
    int InstallmentNumber,
    int InstallmentTotal,
    Money Amount);

/// <summary>One card payment as the user sees it: calculated, with a stored paid state applied.</summary>
public sealed record ScheduledCardPayment(
    Guid CardId,
    YearMonth Month,
    DateOnly PeriodFrom,
    DateOnly StatementDate,
    DateOnly PaymentDate,
    Money Amount,
    int LineCount,
    bool IsPaid,
    DateOnly? PaidDate,
    Guid? AccountId,
    bool PaysFromAccountDirectly,
    bool PaidAutomatically,
    bool NeedsAccount)
{
    /// <summary>The date that decides the month the card payment is shown in: the paid date once paid, the payment date before.</summary>
    public DateOnly MonthDate => PaidDate ?? PaymentDate;

    /// <summary>Payment date passed and not paid (FR-8).</summary>
    public bool IsOverdue(DateOnly today) => !IsPaid && PaymentDate < today;
}

/// <summary>An installment of a purchase with its card payment dates and its reversal, for the purchase details (FR-4, FR-5).</summary>
public sealed record ScheduledInstallment(
    PurchaseInstallment Installment,
    DateOnly StatementDate,
    DateOnly PaymentDate,
    bool IsCharged,
    InstallmentReversalInfo? Reversal,
    YearMonth? CreditMonth);

/// <summary>The figures of a purchase (FR-4): installments charged and the remaining amount.</summary>
public sealed record PurchaseSummary(int InstallmentsCharged, int InstallmentsLeft, Money ChargedAmount, Money RemainingAmount);

/// <summary>
/// Calculates the card payments of one card (FR-4, FR-5, FR-8): the lines of every payment month from
/// its purchases, installments and reversals, and the paid state of each card payment.
/// <list type="bullet">
/// <item>An installment that is not reversed is a line in its payment month.</item>
/// <item>A reversed installment whose payment date is on or after the reversal date is removed.</item>
/// <item>A reversed installment whose payment date passed before the reversal stays in its month, and its
/// amount is credited in the card payment of the billing period that contains the reversal date.</item>
/// </list>
/// The card payment of a card connected to its paying account is paid automatically at the end of its
/// payment date, unless the user changed its paid state or the paying account stopped being usable
/// before (<c>chargesStopOn</c>).
/// </summary>
public sealed class CardPaymentSchedule
{
    private readonly Card _card;
    private readonly CardBillingCalendar? _calendar;
    private readonly List<CardPurchase> _purchases;
    private readonly Dictionary<YearMonth, CardPayment> _stored;
    private readonly DateOnly? _chargesStopOn;
    private readonly List<CardPaymentLine> _lines;

    public CardPaymentSchedule(
        Card card, IEnumerable<CardPurchase> purchases, IEnumerable<CardPayment> storedPayments, DateOnly? chargesStopOn = null)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(purchases);
        ArgumentNullException.ThrowIfNull(storedPayments);
        _card = card;
        _calendar = card.BillingCalendar();
        _purchases = purchases.Where(p => p.CardId == card.Id).ToList();
        _stored = storedPayments.Where(p => p.CardId == card.Id).ToDictionary(p => p.Month);
        _chargesStopOn = chargesStopOn;
        _lines = _calendar is null ? [] : _purchases.SelectMany(p => LinesOf(p, _calendar)).ToList();
    }

    public Card Card => _card;

    /// <summary>The card's billing calendar; null when the card has no billing details.</summary>
    public CardBillingCalendar? Calendar => _calendar;

    public IReadOnlyList<CardPurchase> Purchases => _purchases;

    /// <summary>True when the paying account is no longer a usable primary account (FR-9).</summary>
    public bool NeedsAccount => _calendar is not null && _chargesStopOn is not null;

    /// <summary>All lines of all card payments.</summary>
    public IReadOnlyList<CardPaymentLine> Lines => _lines;

    /// <summary>The lines of the card payment of <paramref name="month"/>.</summary>
    public IEnumerable<CardPaymentLine> LinesOf(YearMonth month) => _lines.Where(l => l.PaymentMonth == month);

    /// <summary>Every card payment that has lines, oldest first.</summary>
    public IReadOnlyList<ScheduledCardPayment> Payments(DateOnly today) =>
        _lines.Select(l => l.PaymentMonth).Distinct().Order().Select(m => Calculate(m, today)).ToList();

    /// <summary>The card payment of <paramref name="month"/>, if it has lines.</summary>
    public ScheduledCardPayment? Find(YearMonth month, DateOnly today) =>
        _lines.Exists(l => l.PaymentMonth == month) ? Calculate(month, today) : null;

    /// <summary>Card payments paid automatically, without a stored paid state.</summary>
    public IEnumerable<ScheduledCardPayment> AutomaticallyPaid(DateOnly today) => Payments(today).Where(p => p.PaidAutomatically);

    /// <summary>Whether the card still has installments to charge: an ongoing transaction of the card.</summary>
    public bool HasInstallmentsLeft(DateOnly today) =>
        _calendar is not null && _purchases.Exists(p => Summary(p, today).InstallmentsLeft > 0);

    /// <summary>The installments of a purchase with their card payment dates and reversals (FR-4, FR-5).</summary>
    public IReadOnlyList<ScheduledInstallment> InstallmentsOf(CardPurchase purchase, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(purchase);
        if (_calendar is not { } calendar)
        {
            return [];
        }
        return purchase.Installments(calendar)
            .Select(i =>
            {
                var paymentDate = calendar.PaymentDate(i.PaymentMonth);
                var reversal = purchase.ReversalOf(i.Number);
                var charged = paymentDate < (reversal?.ReversedOn ?? today);
                YearMonth? creditMonth = reversal is not null && charged ? calendar.PaymentMonthOf(reversal.ReversedOn) : null;
                return new ScheduledInstallment(i, calendar.StatementDate(i.PaymentMonth), paymentDate, charged, reversal, creditMonth);
            })
            .ToList();
    }

    /// <summary>
    /// Installments charged (payment date passed) and left, and the remaining amount: the installments
    /// still to be charged that are not reversed (FR-4, FR-5).
    /// </summary>
    public PurchaseSummary Summary(CardPurchase purchase, DateOnly today)
    {
        var installments = InstallmentsOf(purchase, today);
        var charged = installments.Where(i => i.IsCharged && i.Reversal is null).ToList();
        var left = installments.Where(i => !i.IsCharged && i.Reversal is null).ToList();
        return new PurchaseSummary(
            charged.Count,
            left.Count,
            Money.Sum(charged.Select(i => i.Installment.Amount)),
            Money.Sum(left.Select(i => i.Installment.Amount)));
    }

    /// <summary>Whether any installment of the purchase is in a card payment whose payment date has passed (FR-6).</summary>
    public bool HasChargedInstallments(CardPurchase purchase, DateOnly today) =>
        InstallmentsOf(purchase, today).Any(i => i.IsCharged);

    private static IEnumerable<CardPaymentLine> LinesOf(CardPurchase purchase, CardBillingCalendar calendar)
    {
        foreach (var installment in purchase.Installments(calendar))
        {
            var kind = purchase.InInstallments ? CardPaymentLineKind.Installment : CardPaymentLineKind.FullPurchase;
            var line = new CardPaymentLine(
                purchase.Id, installment.PaymentMonth, kind, installment.Number, installment.Total, installment.Amount);
            if (purchase.ReversalOf(installment.Number) is not { } reversal)
            {
                yield return line;
                continue;
            }
            // Removed when its card payment was not paid yet; otherwise charged and credited back.
            if (calendar.PaymentDate(installment.PaymentMonth) < reversal.ReversedOn)
            {
                yield return line;
                yield return line with
                {
                    PaymentMonth = calendar.PaymentMonthOf(reversal.ReversedOn),
                    Kind = CardPaymentLineKind.Credit,
                    Amount = -installment.Amount,
                };
            }
        }
    }

    private ScheduledCardPayment Calculate(YearMonth month, DateOnly today)
    {
        var calendar = _calendar!;
        var lines = _lines.Where(l => l.PaymentMonth == month).ToList();
        var paymentDate = calendar.PaymentDate(month);
        var (periodFrom, statementDate) = calendar.Period(month);
        _stored.TryGetValue(month, out var stored);

        bool isPaid;
        var paidAutomatically = false;
        DateOnly? paidDate = null;
        Guid? accountId = _card.PayingAccountId;
        if (stored?.IsPaid is { } explicitState)
        {
            isPaid = explicitState;
            if (isPaid)
            {
                paidDate = stored.PaidDate;
                accountId = stored.PaidAccountId;
            }
        }
        else
        {
            isPaid = _card.PaysFromAccountDirectly
                && paymentDate < today
                && (_chargesStopOn is null || paymentDate < _chargesStopOn);
            if (isPaid)
            {
                paidAutomatically = true;
                paidDate = paymentDate;
            }
        }
        var needsAccount = !isPaid && _chargesStopOn is { } stop && paymentDate >= stop;
        return new ScheduledCardPayment(
            _card.Id,
            month,
            periodFrom,
            statementDate,
            paymentDate,
            Money.Sum(lines.Select(l => l.Amount)),
            lines.Count,
            isPaid,
            paidDate,
            accountId,
            _card.PaysFromAccountDirectly,
            paidAutomatically,
            needsAccount);
    }
}
