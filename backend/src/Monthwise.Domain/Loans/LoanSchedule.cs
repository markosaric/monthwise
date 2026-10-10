using Monthwise.Domain.Common;
using Monthwise.Domain.SharedCosts;

namespace Monthwise.Domain.Loans;

/// <summary>One payment of a loan as the user sees it: calculated, with a stored payment applied.</summary>
public sealed record ScheduledPayment(
    Guid LoanId,
    YearMonth Month,
    LoanPaymentKind Kind,
    int Number,
    int Total,
    Money Amount,
    DateOnly BillDate,
    DateOnly DueDate,
    DateOnly? PaidDate,
    Guid? AccountId,
    PaymentMethod Method,
    bool IsPaid,
    bool IsChanged,
    bool NeedsAccount,
    bool IsStored,
    bool PaidAutomatically,
    CostShare? Share = null,
    bool IsShareChanged = false)
{
    /// <summary>The date that decides the month the payment is shown in: the paid date once paid, the due date before (section 3).</summary>
    public DateOnly MonthDate => PaidDate ?? DueDate;

    /// <summary>The share that applies to the payment (shared bills FR-3); null when it is not shared.</summary>
    public bool IsShared => Share is not null;

    /// <summary>Due date passed and not paid (FR-13).</summary>
    public bool IsOverdue(DateOnly today) => !IsPaid && DueDate < today;

}

/// <summary>
/// Calculates the payments of a loan (FR-4 to FR-7, FR-9, FR-10): one regular payment per month from
/// the start month to the end month, and the residual value as a bill in the month after. The amount
/// and payment day of a month come from the newest payment change that applies to it. Stored payments
/// override the calculated values. A standing order payment is paid automatically at the end of its
/// due date, unless the user changed its paid state or its account stopped being usable before it
/// (<c>chargesStopOn</c>, FR-3). A closed loan keeps only its paid payments.
/// </summary>
public sealed class LoanSchedule
{
    private readonly Loan _loan;
    private readonly Dictionary<(YearMonth Month, LoanPaymentKind Kind), LoanPayment> _stored;
    private readonly DateOnly? _chargesStopOn;

    public LoanSchedule(Loan loan, IEnumerable<LoanPayment> storedPayments, DateOnly? chargesStopOn = null)
    {
        ArgumentNullException.ThrowIfNull(loan);
        ArgumentNullException.ThrowIfNull(storedPayments);
        _loan = loan;
        _stored = storedPayments.Where(p => p.LoanId == loan.Id).ToDictionary(p => (p.Month, p.Kind));
        _chargesStopOn = chargesStopOn;
    }

    /// <summary>True when the loan is a standing order whose account is no longer a usable primary account (FR-3).</summary>
    public bool NeedsAccount => _loan.PaymentMethod == PaymentMethod.StandingOrder && !_loan.IsClosed && _chargesStopOn is not null;

    /// <summary>All payments of the loan in schedule order: regular payments by month, then the residual value.</summary>
    public IReadOnlyList<ScheduledPayment> Payments(DateOnly today)
    {
        var payments = new List<ScheduledPayment>(_loan.PaymentCount + 1);
        var month = _loan.StartMonth;
        for (var number = 1; number <= _loan.PaymentCount; number++, month = month.Next())
        {
            Add(payments, month, LoanPaymentKind.Regular, number, today);
        }
        if (_loan.ResidualValue is not null)
        {
            Add(payments, _loan.EndMonth.Next(), LoanPaymentKind.Residual, 0, today);
        }
        return payments;
    }

    /// <summary>Payments shown between <paramref name="from"/> and <paramref name="to"/> (inclusive), by paid date or due date.</summary>
    public IEnumerable<ScheduledPayment> PaymentsBetween(DateOnly from, DateOnly to, DateOnly today) =>
        Payments(today).Where(p => p.MonthDate >= from && p.MonthDate <= to);

    /// <summary>The payment scheduled for <paramref name="month"/>, if the loan has one.</summary>
    public ScheduledPayment? Find(YearMonth month, LoanPaymentKind kind, DateOnly today) =>
        Payments(today).FirstOrDefault(p => p.Month == month && p.Kind == kind);

    /// <summary>The scheduled month of the latest paid payment; a payment change must start after it (FR-7).</summary>
    public YearMonth? LatestPaidMonth(DateOnly today) =>
        Payments(today).Where(p => p.IsPaid).Select(p => (YearMonth?)p.Month).Max();

    /// <summary>What is paid, for the edit rules (FR-10).</summary>
    public PaidPayments Paid(DateOnly today)
    {
        var paid = Payments(today).Where(p => p.IsPaid).ToList();
        var regular = paid.Where(p => p.Kind == LoanPaymentKind.Regular).Select(p => p.Month).ToList();
        return new PaidPayments(
            regular.Count == 0 ? null : regular.Min(),
            regular.Count == 0 ? null : regular.Max(),
            paid.Any(p => p.Kind == LoanPaymentKind.Residual));
    }

    public bool HasPaidPayments(DateOnly today) => Payments(today).Any(p => p.IsPaid);

    /// <summary>Standing order payments paid automatically, without a stored paid state. They are stored before the loan changes, so they stay paid.</summary>
    public IEnumerable<ScheduledPayment> AutomaticallyPaid(DateOnly today) => Payments(today).Where(p => p.PaidAutomatically);

    /// <summary>The sum of the expected payments, the residual value included (FR-9).</summary>
    public Money RemainingAmount(DateOnly today) => Money.Sum(Payments(today).Where(p => !p.IsPaid).Select(p => p.Amount));

    public LoanSummary Summary(DateOnly today)
    {
        var payments = Payments(today);
        var expected = payments.Where(p => !p.IsPaid).OrderBy(p => p.DueDate).ToList();
        var paid = payments.Where(p => p.IsPaid).ToList();
        var status = _loan.IsClosed ? LoanStatus.Closed : expected.Count == 0 ? LoanStatus.PaidOff : LoanStatus.Active;
        var next = expected.FirstOrDefault();
        return new LoanSummary(
            status,
            _loan.MonthlyPaymentFor(Current(today)),
            _loan.PaymentDayFor(Current(today)),
            next?.DueDate,
            paid.Count(p => p.Kind == LoanPaymentKind.Regular),
            expected.Count(p => p.Kind == LoanPaymentKind.Regular),
            Money.Sum(paid.Select(p => p.Amount)),
            Money.Sum(expected.Select(p => p.Amount)),
            expected.Any(p => p.IsOverdue(today)),
            NeedsAccount);
    }

    /// <summary>The month whose monthly payment is the current one: today's month within the loan's months.</summary>
    private YearMonth Current(DateOnly today)
    {
        var month = YearMonth.FromDate(today);
        if (month < _loan.StartMonth)
        {
            return _loan.StartMonth;
        }
        return month > _loan.EndMonth ? _loan.EndMonth : month;
    }

    private void Add(List<ScheduledPayment> payments, YearMonth month, LoanPaymentKind kind, int number, DateOnly today)
    {
        var payment = Calculate(month, kind, number, today);
        // A closed loan keeps only its paid payments (FR-10).
        if (!_loan.IsClosed || payment.IsPaid)
        {
            payments.Add(payment);
        }
    }

    private ScheduledPayment Calculate(YearMonth month, LoanPaymentKind kind, int number, DateOnly today)
    {
        _stored.TryGetValue((month, kind), out var stored);
        var method = kind == LoanPaymentKind.Residual ? PaymentMethod.Bill : _loan.PaymentMethod;
        var amount = stored?.Amount ?? (kind == LoanPaymentKind.Residual ? _loan.ResidualValue!.Value : _loan.MonthlyPaymentFor(month));
        var dueDate = stored?.DueDate ?? DueDateRule.DueDate(month, _loan.PaymentDayFor(month), method);

        // Charges end with the loan when it is closed.
        var chargedUntil = _loan.ClosedOn is { } closedOn && closedOn < today ? closedOn : today;
        var chargesStop = _chargesStopOn;
        bool isPaid;
        var paidAutomatically = false;
        DateOnly? paidDate = null;
        Guid? accountId = null;
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
            isPaid = method == PaymentMethod.StandingOrder
                && dueDate < chargedUntil
                && (chargesStop is null || dueDate < chargesStop);
            if (isPaid)
            {
                paidAutomatically = true;
                paidDate = dueDate;
                accountId = _loan.AccountId;
            }
        }
        if (!isPaid && method == PaymentMethod.StandingOrder)
        {
            accountId = _loan.AccountId;
        }
        var needsAccount = !isPaid && method == PaymentMethod.StandingOrder && chargesStop is { } stop && dueDate >= stop;
        return new ScheduledPayment(
            _loan.Id,
            month,
            kind,
            number,
            _loan.PaymentCount,
            amount,
            DueDateRule.BillDate(month),
            dueDate,
            paidDate,
            accountId,
            method,
            isPaid,
            stored?.IsChanged ?? false,
            needsAccount,
            stored is not null,
            paidAutomatically,
            stored is not null ? stored.ShareOr(_loan.Share) : _loan.Share,
            stored?.IsShareChanged ?? false);
    }
}

/// <summary>The figures of a loan for the list, details and Dashboard (FR-1, FR-9, FR-13).</summary>
public sealed record LoanSummary(
    LoanStatus Status,
    Money CurrentMonthlyPayment,
    int CurrentPaymentDay,
    DateOnly? NextPaymentDate,
    int PaymentsPaid,
    int PaymentsLeft,
    Money PaidAmount,
    Money RemainingAmount,
    bool HasOverduePayment,
    bool NeedsAccount);
