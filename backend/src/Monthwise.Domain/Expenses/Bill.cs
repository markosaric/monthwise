using Monthwise.Domain.Common;
using Monthwise.Domain.SharedCosts;

namespace Monthwise.Domain.Expenses;

/// <summary>
/// What the user enters for a bill (FR-4, B1 to B10, without the paid state). A one-off bill needs
/// amount and dates; a bill from a recurring bill is Not issued until all three are given (Appendix 2).
/// </summary>
public sealed record BillDetails(
    string? Name,
    Guid CategoryId,
    string? Payee,
    Money Amount,
    DateOnly? IssueDate,
    DateOnly? DueDate,
    Guid? PaidFromId,
    string? Description);

/// <summary>The state of a bill (FR-4).</summary>
public enum BillState
{
    /// <summary>Added from a recurring bill: amount, issue date or due date still missing (Appendix 2).</summary>
    NotIssued,

    /// <summary>Issued (amount and dates entered), not paid yet.</summary>
    Expected,

    Paid,
}

/// <summary>
/// A bill of one month (FR-4): added from a recurring bill as Not issued (FR-3), or added once. It is
/// paid from an account, or with a credit card, where it becomes a line of the card payment (FR-6).
/// </summary>
public sealed class Bill
{
    private Bill()
    {
        Name = string.Empty;
    }

    public Guid Id { get; private set; }

    /// <summary>The recurring bill it was added from; null for a one-off bill.</summary>
    public Guid? RecurringBillId { get; private set; }

    /// <summary>The month the bill was added to; it is listed there on the Month screen.</summary>
    public YearMonth Month { get; private set; }

    public string Name { get; private set; }
    public Guid CategoryId { get; private set; }
    public string? Payee { get; private set; }

    /// <summary>0 when not known yet (Not issued), otherwise greater than zero.</summary>
    public Money Amount { get; private set; }

    public DateOnly? IssueDate { get; private set; }
    public DateOnly? DueDate { get; private set; }
    public bool IsPaid { get; private set; }
    public DateOnly? PaidDate { get; private set; }

    /// <summary>What the bill was paid with once paid; before that, what it will usually be paid with.</summary>
    public Guid? PaidFromId { get; private set; }

    public string? Description { get; private set; }

    /// <summary>The share columns of <see cref="Share"/> (FR-2); null when the bill is not shared.</summary>
    public Guid? ShareParticipantId { get; private set; }
    public int? ShareCount { get; private set; }
    public int? ParticipantShareCount { get; private set; }
    public Guid? ContributionAccountId { get; private set; }

    /// <summary>The share was set or changed on this bill instead of being copied from its recurring bill (shared bills FR-3).</summary>
    public bool IsShareChanged { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Amount, issue date and due date are all entered (FR-4, Appendix 2).</summary>
    public bool IsIssued => Amount.IsPositive && IssueDate is not null && DueDate is not null;

    /// <summary>How the bill is shared (shared bills FR-2, FR-3).</summary>
    public CostShare? Share => CostShareColumns.Read(ShareParticipantId, ShareCount, ParticipantShareCount, ContributionAccountId);

    public BillState State => IsPaid ? BillState.Paid : IsIssued ? BillState.Expected : BillState.NotIssued;

    /// <summary>The date the bill counts on in the cash flow: the paid date once paid, the due date before (FR-4).</summary>
    public DateOnly? CashFlowDate => IsPaid ? PaidDate : DueDate;

    public BillDetails Details => new(Name, CategoryId, Payee, Amount, IssueDate, DueDate, PaidFromId, Description);

    /// <summary>Expected and its due date has passed (FR-4).</summary>
    public bool IsOverdue(DateOnly today) => State == BillState.Expected && DueDate < today;

    /// <summary>
    /// The bill of <paramref name="month"/> from a recurring bill (FR-3): with the recurring bill's
    /// amount and dates when it has them, so Expected when all three are set, otherwise Not issued
    /// (Appendix 2, A2.2).
    /// </summary>
    public static Bill CreateFromRecurring(RecurringBill recurringBill, YearMonth month, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(recurringBill);
        var (issueDate, dueDate) = recurringBill.DatesIn(month);
        return new Bill
        {
            Id = Guid.CreateVersion7(now),
            RecurringBillId = recurringBill.Id,
            Month = month,
            Name = recurringBill.Name,
            CategoryId = recurringBill.CategoryId,
            Payee = recurringBill.Payee,
            Amount = recurringBill.Amount ?? Money.Zero,
            IssueDate = issueDate,
            DueDate = dueDate,
            PaidFromId = recurringBill.PaidFromId,
            Description = recurringBill.Description,
            ShareParticipantId = recurringBill.ShareParticipantId,
            ShareCount = recurringBill.ShareCount,
            ParticipantShareCount = recurringBill.ParticipantShareCount,
            ContributionAccountId = recurringBill.ContributionAccountId,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    /// <summary>
    /// A one-off bill of <paramref name="month"/>, added directly as expected, or as paid when
    /// <paramref name="paidDate"/> is given (FR-4). The category and what it is paid with are checked by
    /// the caller.
    /// </summary>
    public static Result<Bill> CreateOneOff(YearMonth month, BillDetails details, DateOnly? paidDate, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(details);
        var errors = Validate(details, paidDate, requireIssued: true);
        if (errors.Count > 0)
        {
            return Result.Failure<Bill>(errors);
        }
        var bill = new Bill { Id = Guid.CreateVersion7(now), Month = month, CreatedAt = now };
        bill.Apply(details, paidDate, now);
        return Result.Success(bill);
    }

    /// <summary>
    /// Edits the bill: issues it when amount and dates are entered (FR-4), and sets its paid state
    /// (paid on <paramref name="paidDate"/>, or not paid when null). A one-off bill stays issued.
    /// </summary>
    public Result Update(BillDetails details, DateOnly? paidDate, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(details);
        var errors = Validate(details, paidDate, requireIssued: RecurringBillId is null);
        if (errors.Count > 0)
        {
            return Result.Failure(errors);
        }
        Apply(details, paidDate, now);
        return Result.Success();
    }

    /// <summary>
    /// Makes a one-off bill the bill of <paramref name="recurringBill"/> for its month, when the user
    /// ticks Recurring on it (Appendix 2, A2.3).
    /// </summary>
    public Result LinkTo(RecurringBill recurringBill, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(recurringBill);
        if (RecurringBillId is not null)
        {
            return Result.Failure(ExpenseErrors.BillAlreadyRecurring);
        }
        RecurringBillId = recurringBill.Id;
        UpdatedAt = now;
        return Result.Success();
    }

    /// <summary>Ticks Paid (FR-5): only an issued bill, paid on or after its issue date.</summary>
    public Result MarkPaid(DateOnly paidDate, Guid paidFromId, DateTimeOffset now)
    {
        if (!IsIssued)
        {
            return Result.Failure(ExpenseErrors.BillNotIssued);
        }
        if (paidFromId == Guid.Empty)
        {
            return Result.Failure(ExpenseErrors.PaidFromRequired);
        }
        if (paidDate < IssueDate)
        {
            return Result.Failure(ExpenseErrors.PaidBeforeIssue);
        }
        IsPaid = true;
        PaidDate = paidDate;
        PaidFromId = paidFromId;
        UpdatedAt = now;
        return Result.Success();
    }

    /// <summary>
    /// Shares the bill, changes its share or stops sharing it (shared bills FR-3). On a bill from a
    /// recurring bill a different share than the recurring bill's is marked as changed by hand.
    /// </summary>
    public void SetShare(CostShare? share, CostShare? recurringShare, DateTimeOffset now)
    {
        if (share == Share)
        {
            return;
        }
        (ShareParticipantId, ShareCount, ParticipantShareCount, ContributionAccountId) = CostShareColumns.Write(share);
        IsShareChanged = RecurringBillId is not null && share != recurringShare;
        UpdatedAt = now;
    }

    /// <summary>Unticks Paid: the bill is expected again (FR-5).</summary>
    public void MarkUnpaid(DateTimeOffset now)
    {
        IsPaid = false;
        PaidDate = null;
        UpdatedAt = now;
    }

    private static List<DomainError> Validate(BillDetails details, DateOnly? paidDate, bool requireIssued)
    {
        var errors = new List<DomainError>();
        var name = details.Name?.Trim() ?? string.Empty;
        if (name.Length == 0)
        {
            errors.Add(ExpenseErrors.NameRequired);
        }
        else if (name.Length > RecurringBill.NameMaxLength)
        {
            errors.Add(ExpenseErrors.NameTooLong);
        }
        if (details.CategoryId == Guid.Empty)
        {
            errors.Add(ExpenseErrors.CategoryRequired);
        }
        if (Text.Normalize(details.Payee)?.Length > RecurringBill.PayeeMaxLength)
        {
            errors.Add(ExpenseErrors.PayeeTooLong);
        }
        if (Text.Normalize(details.Description)?.Length > RecurringBill.DescriptionMaxLength)
        {
            errors.Add(ExpenseErrors.BillDescriptionTooLong);
        }

        // A one-off or paid bill needs all three; a bill from a recurring bill may keep any of them
        // empty and stays Not issued until all three are entered (Appendix 2, A2.2).
        if (requireIssued || paidDate is not null)
        {
            if (!details.Amount.IsPositive)
            {
                errors.Add(ExpenseErrors.AmountNotPositive);
            }
            if (details.IssueDate is null)
            {
                errors.Add(ExpenseErrors.IssueDateRequired);
            }
            if (details.DueDate is null)
            {
                errors.Add(ExpenseErrors.DueDateRequired);
            }
            else if (details.DueDate < details.IssueDate)
            {
                errors.Add(ExpenseErrors.DueBeforeIssue);
            }
        }
        else
        {
            if (details.Amount.IsNegative)
            {
                errors.Add(ExpenseErrors.AmountNotPositive);
            }
            if (details.DueDate < details.IssueDate)
            {
                errors.Add(ExpenseErrors.DueBeforeIssue);
            }
        }
        if (paidDate is { } paid)
        {
            if (details.PaidFromId is null || details.PaidFromId == Guid.Empty)
            {
                errors.Add(ExpenseErrors.PaidFromRequired);
            }
            if (paid < details.IssueDate)
            {
                errors.Add(ExpenseErrors.PaidBeforeIssue);
            }
        }
        return errors;
    }

    private void Apply(BillDetails details, DateOnly? paidDate, DateTimeOffset now)
    {
        Name = details.Name!.Trim();
        CategoryId = details.CategoryId;
        Payee = Text.Normalize(details.Payee);
        Amount = details.Amount;
        IssueDate = details.IssueDate;
        DueDate = details.DueDate;
        PaidFromId = details.PaidFromId is { } id && id != Guid.Empty ? id : null;
        Description = Text.Normalize(details.Description);
        IsPaid = paidDate is not null;
        PaidDate = paidDate;
        UpdatedAt = now;
    }
}
