using Monthwise.Domain.Cards;
using Monthwise.Domain.Common;

namespace Monthwise.Domain.Revolving;

/// <summary>What raises or lowers the debt of a revolving card as a charge (revolving FR-6).</summary>
public enum RevolvingChargeKind
{
    /// <summary>A bill paid with the card.</summary>
    Bill,

    /// <summary>An expense paid with the card.</summary>
    Expense,

    /// <summary>A card purchase, always paid in full.</summary>
    Purchase,

    /// <summary>A reversed purchase, credited back on its reversal date (negative).</summary>
    PurchaseCredit,
}

/// <summary>
/// A bill, an expense or a purchase paid with a revolving card, or the credit of a reversed purchase
/// (revolving FR-6). A positive <see cref="Amount"/> raises the debt on <see cref="Date"/>; a credit is
/// negative.
/// </summary>
public sealed record RevolvingCharge(Guid SourceId, Guid CardId, RevolvingChargeKind Kind, DateOnly Date, Money Amount);

/// <summary>What changed the debt (revolving FR-10, the debt history on the card).</summary>
public enum RevolvingMovementKind
{
    Starting,
    Correction,
    TransferIn,
    TransferOut,
    Charge,
    Credit,
    Interest,
    BillPayment,
    ExtraPayment,
}

/// <summary>
/// One change of the debt, with the debt after it. <see cref="Change"/> is positive when the debt goes
/// up. <see cref="SourceId"/> is the entry, charge or extra payment; <see cref="BillMonth"/> the monthly
/// bill of an interest or a bill payment. An estimated movement is a bill payment assumed in the future.
/// </summary>
public sealed record RevolvingMovement(
    DateOnly Date,
    RevolvingMovementKind Kind,
    Money Change,
    Money DebtAfter,
    Guid? SourceId,
    YearMonth? BillMonth,
    bool IsEstimated);

/// <summary>One monthly bill as the user sees it: calculated, with the stored correction and paid state applied (revolving FR-4, FR-8).</summary>
public sealed record ScheduledRevolvingBill(
    Guid CardId,
    YearMonth Month,
    DateOnly BillDate,
    DateOnly DueDate,
    Money Debt,
    Money Interest,
    Money Minimum,
    bool IsCorrected,
    bool IsEstimated,
    bool IsPaid,
    DateOnly? PaidDate,
    Money? PaidAmount,
    Guid? AccountId,
    bool PaysFromAccountDirectly,
    bool PaidAutomatically,
    bool NeedsAccount)
{
    /// <summary>The date that decides the month the bill is shown in: the paid date once paid, the due date before.</summary>
    public DateOnly MonthDate => PaidDate ?? DueDate;

    /// <summary>The amount of the output: the paid amount once paid, the minimum payment before.</summary>
    public Money Amount => IsPaid ? PaidAmount!.Value : Minimum;

    /// <summary>Due date passed and not paid (FR-8).</summary>
    public bool IsOverdue(DateOnly today) => !IsPaid && DueDate < today;
}

/// <summary>
/// Calculates the debt and the monthly bills of one revolving card (revolving FR-3 to FR-8), day by day
/// in date order:
/// <list type="bullet">
/// <item>On each date, a starting debt sets the debt at the start of that date; charges, credits,
/// transfers and payments come next; a correction then sets the debt at the end of that date; the
/// monthly bill of a bill date comes last, so everything dated on the bill day counts in that bill
/// (FR-5, Appendix 1).</item>
/// <item>Interest accrues daily (FR-7, Appendix 1): the debt at the end of every day after the previous
/// bill date, up to and including this bill date, is summed and × yearly rate ÷ 365 is added on the
/// bill date. Then the minimum payment is calculated from the debt with interest (FR-3). Without debt on
/// the bill date there is no bill.</item>
/// <item>A paid bill lowers the debt on its paid date. A bill of a card connected to its paying account
/// is paid automatically with its minimum at the end of its due date, unless the user changed its state
/// or the account stopped being usable before (<c>chargesStopOn</c>).</item>
/// <item>Bills after today are estimated: every bill not paid yet and due after today is assumed paid
/// with its minimum on its due date.</item>
/// </list>
/// </summary>
public sealed class RevolvingDebtCalculator
{
    private readonly List<RevolvingMovement> _movements = [];
    private readonly List<ScheduledRevolvingBill> _bills = [];

    /// <param name="horizon">The last month bills are calculated for; later ones are not needed.</param>
    public RevolvingDebtCalculator(
        Card card,
        IEnumerable<DebtEntry> entries,
        IEnumerable<RevolvingCharge> charges,
        IEnumerable<ExtraPayment> payments,
        IEnumerable<RevolvingBill> storedBills,
        DateOnly today,
        YearMonth horizon,
        DateOnly? chargesStopOn = null)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(charges);
        ArgumentNullException.ThrowIfNull(payments);
        ArgumentNullException.ThrowIfNull(storedBills);
        Card = card;
        Today = today;

        var events = new List<Event>();
        foreach (var entry in entries.Where(e => e.CardId == card.Id))
        {
            events.Add(entry.Kind switch
            {
                DebtEntryKind.Starting => new Event(entry.Date, StartOfDay, RevolvingMovementKind.Starting, entry.Amount, entry.Id),
                DebtEntryKind.Correction => new Event(entry.Date, EndOfDay, RevolvingMovementKind.Correction, entry.Amount, entry.Id),
                DebtEntryKind.TransferIn => new Event(entry.Date, Relative, RevolvingMovementKind.TransferIn, entry.Amount, entry.Id),
                _ => new Event(entry.Date, Relative, RevolvingMovementKind.TransferOut, -entry.Amount, entry.Id),
            });
        }
        events.AddRange(charges.Where(c => c.CardId == card.Id).Select(c => new Event(
            c.Date, Relative, c.Amount.IsNegative ? RevolvingMovementKind.Credit : RevolvingMovementKind.Charge, c.Amount, c.SourceId)));
        events.AddRange(payments.Where(p => p.CardId == card.Id).Select(p => new Event(
            p.Date, Relative, RevolvingMovementKind.ExtraPayment, -p.Amount, p.Id)));

        var stored = storedBills.Where(b => b.CardId == card.Id).ToDictionary(b => b.Month);
        Calculate(events, stored, horizon, chargesStopOn);
    }

    /// <summary>A starting debt: sets the debt at the start of its date, before anything else on it.</summary>
    private const int StartOfDay = -1;

    /// <summary>A charge, credit, transfer or payment: changes the debt by its amount.</summary>
    private const int Relative = 0;

    /// <summary>A correction: sets the debt at the end of its date, after everything else on it.</summary>
    private const int EndOfDay = 1;

    public Card Card { get; }
    public DateOnly Today { get; }

    /// <summary>Every change of the debt, in date order, estimated bill payments included.</summary>
    public IReadOnlyList<RevolvingMovement> Movements => _movements;

    /// <summary>The monthly bills, oldest first, estimated ones included.</summary>
    public IReadOnlyList<ScheduledRevolvingBill> Bills => _bills;

    /// <summary>The debt at the end of <paramref name="date"/>; zero before anything happened on the card.</summary>
    public Money DebtOn(DateOnly date) => _movements.LastOrDefault(m => m.Date <= date)?.DebtAfter ?? Money.Zero;

    /// <summary>Today's debt (FR-5).</summary>
    public Money Debt => DebtOn(Today);

    /// <summary>The bill of <paramref name="month"/>, if the card has one.</summary>
    public ScheduledRevolvingBill? Bill(YearMonth month) => _bills.Find(b => b.Month == month);

    /// <summary>The first bill not paid yet, by due date: the next bill to pay (FR-10).</summary>
    public ScheduledRevolvingBill? NextBill => _bills.Where(b => !b.IsPaid).OrderBy(b => b.DueDate).FirstOrDefault();

    /// <summary>Bills paid automatically, without a stored paid state.</summary>
    public IEnumerable<ScheduledRevolvingBill> AutomaticallyPaid => _bills.Where(b => b.PaidAutomatically);

    private void Calculate(List<Event> events, Dictionary<YearMonth, RevolvingBill> stored, YearMonth horizon, DateOnly? chargesStopOn)
    {
        var queue = new PriorityQueue<Event, (DateOnly, int, int)>();
        var sequence = 0;
        void Enqueue(Event e) => queue.Enqueue(e, (e.Date, e.Order, sequence++));
        foreach (var e in events.OrderBy(e => e.Date).ThenBy(e => e.Order))
        {
            Enqueue(e);
        }

        var debt = Money.Zero;

        // Daily interest (FR-7): the sum of the debt at the end of each day since the previous bill date,
        // in cent-days. countedThrough is the last day already in the sum.
        long debtCentDays = 0;
        var countedThrough = DateOnly.MinValue;
        void CountDaysThrough(DateOnly date)
        {
            if (date <= countedThrough)
            {
                return;
            }
            if (debt.IsPositive && countedThrough != DateOnly.MinValue)
            {
                debtCentDays = checked(debtCentDays + (debt.Cents * (date.DayNumber - countedThrough.DayNumber)));
            }
            countedThrough = date;
        }

        void ApplyUpTo(DateOnly date, bool includeDate)
        {
            while (queue.TryPeek(out var next, out _) && (next.Date < date || (includeDate && next.Date == date)))
            {
                queue.Dequeue();
                // Every day before this event ended with the debt as it is now.
                CountDaysThrough(next.Date.AddDays(-1));
                var absolute = next.Order != Relative;
                var change = absolute ? next.Amount - debt : next.Amount;
                debt = absolute ? next.Amount : debt + next.Amount;
                _movements.Add(new RevolvingMovement(
                    next.Date, next.Kind, change, debt, next.SourceId, next.BillMonth, next.IsEstimated));
            }
        }

        var calendar = Card.RevolvingDates();
        if (calendar is not null && events.Count > 0)
        {
            countedThrough = events.Min(e => e.Date).AddDays(-1);
            for (var month = YearMonth.FromDate(events.Min(e => e.Date)); month <= horizon; month = month.Next())
            {
                var billDate = calendar.BillDate(month);
                ApplyUpTo(billDate, includeDate: true);
                CountDaysThrough(billDate);
                var centDays = debtCentDays;
                debtCentDays = 0;
                if (!debt.IsPositive)
                {
                    continue;
                }

                stored.TryGetValue(month, out var state);
                var terms = calendar.TermsFor(month);
                var interest = state?.Interest ?? RevolvingMath.DailyInterest(centDays, terms);
                var estimated = billDate > Today;
                if (!interest.IsZero)
                {
                    debt += interest;
                    _movements.Add(new RevolvingMovement(billDate, RevolvingMovementKind.Interest, interest, debt, null, month, estimated));
                }
                var minimum = state?.Minimum ?? RevolvingMath.MinimumPayment(debt, terms);
                var dueDate = calendar.DueDate(month);
                var bill = PaidState(month, billDate, dueDate, debt, interest, minimum, state, estimated, chargesStopOn);
                _bills.Add(bill);

                if (bill.IsPaid)
                {
                    Enqueue(new Event(bill.PaidDate!.Value, Relative, RevolvingMovementKind.BillPayment, -bill.PaidAmount!.Value, null, month));
                }
                else if (dueDate > Today && minimum.IsPositive)
                {
                    // An estimate: the minimum is assumed paid on the due date.
                    Enqueue(new Event(dueDate, Relative, RevolvingMovementKind.BillPayment, -minimum, null, month, IsEstimated: true));
                }
            }
        }
        ApplyUpTo(DateOnly.MaxValue, includeDate: true);
    }

    private ScheduledRevolvingBill PaidState(
        YearMonth month,
        DateOnly billDate,
        DateOnly dueDate,
        Money debt,
        Money interest,
        Money minimum,
        RevolvingBill? state,
        bool estimated,
        DateOnly? chargesStopOn)
    {
        var connected = Card.PaysFromAccountDirectly;
        var isPaid = false;
        var automatic = false;
        DateOnly? paidDate = null;
        Money? paidAmount = null;
        var accountId = Card.PayingAccountId;
        if (state?.IsPaid is { } explicitState)
        {
            isPaid = explicitState;
            if (isPaid)
            {
                paidDate = state.PaidDate;
                paidAmount = state.PaidAmount;
                accountId = state.PaidAccountId;
            }
        }
        else if (connected && dueDate < Today && minimum.IsPositive && (chargesStopOn is null || dueDate < chargesStopOn))
        {
            isPaid = true;
            automatic = true;
            paidDate = dueDate;
            paidAmount = minimum;
        }
        return new ScheduledRevolvingBill(
            Card.Id,
            month,
            billDate,
            dueDate,
            debt,
            interest,
            minimum,
            state?.IsCorrected ?? false,
            estimated,
            isPaid,
            paidDate,
            paidAmount,
            accountId,
            connected,
            automatic,
            !isPaid && chargesStopOn is not null);
    }

    private sealed record Event(
        DateOnly Date, int Order, RevolvingMovementKind Kind, Money Amount, Guid? SourceId, YearMonth? BillMonth = null, bool IsEstimated = false);
}
