using Monthwise.Domain.Common;
using Monthwise.Domain.SharedCosts;

namespace Monthwise.Domain.Loans;

/// <summary>
/// A stored payment of a loan: created the first time the user changes, pays or unticks a payment
/// (FR-4, FR-5). It is identified by its loan, scheduled <see cref="Month"/> and <see cref="Kind"/>.
/// <see cref="Amount"/> and <see cref="DueDate"/> override the calculated values: set by the user
/// (<see cref="IsChanged"/>), or kept when the payment is paid, so later edits of the loan leave paid
/// payments unchanged. <see cref="IsPaid"/> is the user's explicit state; when it is null a
/// standing order payment is paid automatically after its due date.
/// </summary>
public sealed class LoanPayment
{
    private LoanPayment()
    {
    }

    public Guid Id { get; private set; }
    public Guid LoanId { get; private set; }
    public YearMonth Month { get; private set; }
    public LoanPaymentKind Kind { get; private set; }
    public Money? Amount { get; private set; }
    public DateOnly? DueDate { get; private set; }
    public bool IsChanged { get; private set; }
    public bool? IsPaid { get; private set; }
    public DateOnly? PaidDate { get; private set; }
    public Guid? PaidAccountId { get; private set; }

    /// <summary>
    /// The payment has a share of its own instead of the loan's (shared bills FR-3): set by the user
    /// (<see cref="IsShareChanged"/>), or kept when the payment is paid, so later changes of the loan's
    /// share leave paid payments unchanged. With no share columns it is not shared.
    /// </summary>
    public bool HasOwnShare { get; private set; }

    public bool IsShareChanged { get; private set; }
    public Guid? ShareParticipantId { get; private set; }
    public int? ShareCount { get; private set; }
    public int? ParticipantShareCount { get; private set; }
    public Guid? ContributionAccountId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>True when the payment holds nothing the calculation does not already give, so it can be removed.</summary>
    public bool IsEmpty => !IsChanged && IsPaid is null && Amount is null && DueDate is null && !HasOwnShare;

    /// <summary>The payment's own share when <see cref="HasOwnShare"/>; null otherwise or when it is not shared.</summary>
    public CostShare? Share => CostShareColumns.Read(ShareParticipantId, ShareCount, ParticipantShareCount, ContributionAccountId);

    /// <summary>The share that applies to the payment: its own, otherwise the loan's.</summary>
    public CostShare? ShareOr(CostShare? loanShare) => HasOwnShare ? Share : loanShare;

    public static LoanPayment Create(Guid loanId, YearMonth month, LoanPaymentKind kind, DateTimeOffset now) =>
        new()
        {
            Id = Guid.CreateVersion7(now),
            LoanId = loanId,
            Month = month,
            Kind = kind,
            CreatedAt = now,
            UpdatedAt = now,
        };

    /// <summary>Changes one expected payment (FR-4). The due date stays in the payment's month.</summary>
    public Result Change(Money amount, DateOnly dueDate, DateTimeOffset now)
    {
        var errors = new List<DomainError>();
        if (!amount.IsPositive)
        {
            errors.Add(LoanErrors.PaymentAmountNotPositive);
        }
        if (YearMonth.FromDate(dueDate) != Month)
        {
            errors.Add(LoanErrors.PaymentDueDateOutsideMonth);
        }
        if (errors.Count > 0)
        {
            return Result.Failure(errors);
        }
        Amount = amount;
        DueDate = dueDate;
        IsChanged = true;
        UpdatedAt = now;
        return Result.Success();
    }

    /// <summary>
    /// Marks the payment as paid on <paramref name="paidDate"/> from <paramref name="accountId"/> (FR-5).
    /// The amount and due date it was paid with are kept.
    /// </summary>
    public Result MarkPaid(DateOnly paidDate, Guid accountId, Money amount, DateOnly dueDate, DateTimeOffset now, CostShare? loanShare = null)
    {
        var errors = new List<DomainError>();
        if (paidDate < DueDateRule.BillDate(Month))
        {
            errors.Add(LoanErrors.PaidBeforeBillDate);
        }
        if (accountId == Guid.Empty)
        {
            errors.Add(LoanErrors.PaidAccountRequired);
        }
        if (errors.Count > 0)
        {
            return Result.Failure(errors);
        }
        Amount ??= amount;
        DueDate ??= dueDate;
        KeepShare(loanShare);
        IsPaid = true;
        PaidDate = paidDate;
        PaidAccountId = accountId;
        UpdatedAt = now;
        return Result.Success();
    }

    /// <summary>Unticks the payment: it is expected again, also when it is a standing order (FR-5).</summary>
    public void MarkUnpaid(DateTimeOffset now)
    {
        if (!IsChanged)
        {
            Amount = null;
            DueDate = null;
        }
        ForgetKeptShare();
        IsPaid = false;
        PaidDate = null;
        PaidAccountId = null;
        UpdatedAt = now;
    }

    /// <summary>
    /// Forgets the explicit paid state of an expected payment, e.g. an unticked standing order
    /// payment once the loan is paid as a bill; it is then expected as calculated.
    /// </summary>
    public void ClearPaidState(DateTimeOffset now)
    {
        if (IsPaid == true)
        {
            return;
        }
        if (!IsChanged)
        {
            Amount = null;
            DueDate = null;
        }
        ForgetKeptShare();
        IsPaid = null;
        UpdatedAt = now;
    }

    /// <summary>Drops the hand-changed amount and due date of an expected payment (FR-7).</summary>
    public void ClearChanges(DateTimeOffset now)
    {
        if (IsPaid == true)
        {
            return;
        }
        Amount = null;
        DueDate = null;
        IsChanged = false;
        UpdatedAt = now;
    }

    /// <summary>
    /// Stores a standing order payment that was paid automatically, so it stays paid with its values
    /// when the loan or its payment method changes (FR-5, FR-10).
    /// </summary>
    public void KeepAutomaticPayment(Money amount, DateOnly dueDate, Guid accountId, DateTimeOffset now, CostShare? loanShare = null)
    {
        Amount ??= amount;
        DueDate ??= dueDate;
        KeepShare(loanShare);
        IsPaid = true;
        PaidDate = DueDate;
        PaidAccountId = accountId;
        UpdatedAt = now;
    }

    /// <summary>
    /// Shares this one payment, changes its share or stops sharing it (<paramref name="share"/> null),
    /// whatever the loan's share is (shared bills FR-3).
    /// </summary>
    public void ChangeShare(CostShare? share, DateTimeOffset now)
    {
        SetShareColumns(share);
        HasOwnShare = true;
        IsShareChanged = true;
        UpdatedAt = now;
    }

    /// <summary>Drops the hand-changed share: the payment follows the loan's share again, unless it is paid.</summary>
    public void ResetShare(DateTimeOffset now)
    {
        IsShareChanged = false;
        if (IsPaid != true)
        {
            HasOwnShare = false;
            SetShareColumns(null);
        }
        UpdatedAt = now;
    }

    private void KeepShare(CostShare? loanShare)
    {
        if (!HasOwnShare)
        {
            SetShareColumns(loanShare);
            HasOwnShare = true;
        }
    }

    private void ForgetKeptShare()
    {
        if (!IsShareChanged)
        {
            HasOwnShare = false;
            SetShareColumns(null);
        }
    }

    private void SetShareColumns(CostShare? share) =>
        (ShareParticipantId, ShareCount, ParticipantShareCount, ContributionAccountId) = CostShareColumns.Write(share);
}
