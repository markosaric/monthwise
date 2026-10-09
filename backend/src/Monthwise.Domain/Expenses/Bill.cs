using Monthwise.Domain.Common;

namespace Monthwise.Domain.Expenses;

/// <summary>
/// What the user enters for a bill (FR-4, B1 to B10, without the paid state). A bill without amount
/// and dates is Not issued; once one of them is given, all three are needed.
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
    /// <summary>Added from a recurring bill: amount 0, no dates yet.</summary>
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

    /// <summary>0 while Not issued, then greater than zero.</summary>
    public Money Amount { get; private set; }

    public DateOnly? IssueDate { get; private set; }
    public DateOnly? DueDate { get; private set; }
    public bool IsPaid { get; private set; }
    public DateOnly? PaidDate { get; private set; }

    /// <summary>What the bill was paid with once paid; before that, what it will usually be paid with.</summary>
    public Guid? PaidFromId { get; private set; }

    public string? Description { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public bool IsIssued => IssueDate is not null;

    public BillState State => IsPaid ? BillState.Paid : IsIssued ? BillState.Expected : BillState.NotIssued;

    /// <summary>The date the bill counts on in the cash flow: the paid date once paid, the due date before (FR-4).</summary>
    public DateOnly? CashFlowDate => IsPaid ? PaidDate : DueDate;

    public BillDetails Details => new(Name, CategoryId, Payee, Amount, IssueDate, DueDate, PaidFromId, Description);

    /// <summary>Expected and its due date has passed (FR-4).</summary>
    public bool IsOverdue(DateOnly today) => State == BillState.Expected && DueDate < today;

    /// <summary>A Not issued bill of <paramref name="month"/> from a recurring bill (FR-3).</summary>
    public static Bill CreateFromRecurring(RecurringBill recurringBill, YearMonth month, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(recurringBill);
        return new Bill
        {
            Id = Guid.CreateVersion7(now),
            RecurringBillId = recurringBill.Id,
            Month = month,
            Name = recurringBill.Name,
            CategoryId = recurringBill.CategoryId,
            Payee = recurringBill.Payee,
            Amount = Money.Zero,
            PaidFromId = recurringBill.PaidFromId,
            Description = recurringBill.Description,
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

        var issued = requireIssued || paidDate is not null
            || details.IssueDate is not null || details.DueDate is not null || !details.Amount.IsZero;
        if (issued)
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
