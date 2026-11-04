using Monthwise.Domain.Common;
using Monthwise.Domain.SharedCosts;

namespace Monthwise.Domain.Expenses;

/// <summary>
/// What the user enters for a recurring bill (FR-2, R1 to R5), with the optional amount, issue day and
/// due day of Appendix 2 (R6 to R8).
/// </summary>
public sealed record RecurringBillDetails(
    string? Name,
    Guid CategoryId,
    string? Payee,
    Guid? PaidFromId,
    string? Description,
    Money? Amount = null,
    int? IssueDay = null,
    int? DueDay = null);

/// <summary>
/// A template for a bill that arrives every month (FR-2). Its amount, issue day and due day are
/// optional (Appendix 2): each month gets a bill from it (FR-3) with what is known, Expected when all
/// three are set and Not issued otherwise, which the user fills in when the bill arrives.
/// </summary>
public sealed class RecurringBill
{
    public const int NameMaxLength = 50;
    public const int PayeeMaxLength = 100;
    public const int DescriptionMaxLength = 200;
    public const int MaxDay = 31;

    private RecurringBill()
    {
        Name = string.Empty;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public Guid CategoryId { get; private set; }
    public string? Payee { get; private set; }

    /// <summary>The account or credit card the bill is usually paid with; prefills its bills (R4).</summary>
    public Guid? PaidFromId { get; private set; }

    public string? Description { get; private set; }

    /// <summary>The usual amount (R6); null when it changes every month.</summary>
    public Money? Amount { get; private set; }

    /// <summary>The day of the month the bill is usually issued (R7), 1 to 31.</summary>
    public int? IssueDay { get; private set; }

    /// <summary>The day of the month the bill is usually due (R8), 1 to 31; earlier than the issue day means the following month.</summary>
    public int? DueDay { get; private set; }

    /// <summary>Set when stopped: no longer added to new months; bills already added stay.</summary>
    public DateOnly? StoppedOn { get; private set; }

    /// <summary>The share columns of <see cref="Share"/> (FR-2); null when the recurring bill is not shared.</summary>
    public Guid? ShareParticipantId { get; private set; }
    public int? ShareCount { get; private set; }
    public int? ParticipantShareCount { get; private set; }
    public Guid? ContributionAccountId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public bool IsActive => StoppedOn is null;

    /// <summary>How its bills are shared (shared bills FR-3): copied to each bill added from it.</summary>
    public CostShare? Share => CostShareColumns.Read(ShareParticipantId, ShareCount, ParticipantShareCount, ContributionAccountId);

    public RecurringBillDetails Details => new(Name, CategoryId, Payee, PaidFromId, Description, Amount, IssueDay, DueDay);

    /// <summary>
    /// The issue and due date of its bill in <paramref name="month"/> (Appendix 2, A2.2): each day in
    /// that month, or its last day when the month is shorter; the due date is in the following month
    /// when the due day is earlier than the issue day (R8).
    /// </summary>
    public (DateOnly? IssueDate, DateOnly? DueDate) DatesIn(YearMonth month)
    {
        var issueDate = IssueDay is { } issueDay ? DayIn(month, issueDay) : (DateOnly?)null;
        var dueMonth = DueDay < IssueDay ? month.AddMonths(1) : month;
        var dueDate = DueDay is { } dueDay ? DayIn(dueMonth, dueDay) : (DateOnly?)null;
        return (issueDate, dueDate);
    }

    private static DateOnly DayIn(YearMonth month, int day) =>
        new(month.Year, month.Month, Math.Min(day, month.LastDay.Day));

    /// <summary>
    /// Adds a recurring bill. Unique names, the category and <see cref="PaidFromId"/> are checked by the
    /// caller, which loads them.
    /// </summary>
    public static Result<RecurringBill> Create(RecurringBillDetails details, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(details);
        var errors = Validate(details);
        if (errors.Count > 0)
        {
            return Result.Failure<RecurringBill>(errors);
        }
        var bill = new RecurringBill { Id = Guid.CreateVersion7(now), CreatedAt = now };
        bill.Apply(details, now);
        return Result.Success(bill);
    }

    /// <summary>Edits the recurring bill; bills already added from it stay unchanged.</summary>
    public Result Update(RecurringBillDetails details, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(details);
        var errors = Validate(details);
        if (errors.Count > 0)
        {
            return Result.Failure(errors);
        }
        Apply(details, now);
        return Result.Success();
    }

    /// <summary>Shares the recurring bill, or stops sharing it; bills already added keep their shares (shared bills FR-3).</summary>
    public void SetShare(CostShare? share, DateTimeOffset now)
    {
        (ShareParticipantId, ShareCount, ParticipantShareCount, ContributionAccountId) = CostShareColumns.Write(share);
        UpdatedAt = now;
    }

    public void Stop(DateOnly today, DateTimeOffset now)
    {
        StoppedOn ??= today;
        UpdatedAt = now;
    }

    public void Restart(DateTimeOffset now)
    {
        StoppedOn = null;
        UpdatedAt = now;
    }

    private static List<DomainError> Validate(RecurringBillDetails details)
    {
        var errors = new List<DomainError>();
        var name = details.Name?.Trim() ?? string.Empty;
        if (name.Length == 0)
        {
            errors.Add(ExpenseErrors.NameRequired);
        }
        else if (name.Length > NameMaxLength)
        {
            errors.Add(ExpenseErrors.NameTooLong);
        }
        if (details.CategoryId == Guid.Empty)
        {
            errors.Add(ExpenseErrors.CategoryRequired);
        }
        if (Text.Normalize(details.Payee)?.Length > PayeeMaxLength)
        {
            errors.Add(ExpenseErrors.PayeeTooLong);
        }
        if (Text.Normalize(details.Description)?.Length > DescriptionMaxLength)
        {
            errors.Add(ExpenseErrors.BillDescriptionTooLong);
        }
        if (details.Amount is { } amount && !amount.IsPositive)
        {
            errors.Add(ExpenseErrors.RecurringAmountNotPositive);
        }
        if (details.IssueDay is < 1 or > MaxDay)
        {
            errors.Add(ExpenseErrors.IssueDayOutOfRange);
        }
        if (details.DueDay is < 1 or > MaxDay)
        {
            errors.Add(ExpenseErrors.DueDayOutOfRange);
        }
        return errors;
    }

    private void Apply(RecurringBillDetails details, DateTimeOffset now)
    {
        Name = details.Name!.Trim();
        CategoryId = details.CategoryId;
        Payee = Text.Normalize(details.Payee);
        PaidFromId = details.PaidFromId is { } id && id != Guid.Empty ? id : null;
        Description = Text.Normalize(details.Description);
        Amount = details.Amount;
        IssueDay = details.IssueDay;
        DueDay = details.DueDay;
        UpdatedAt = now;
    }
}

/// <summary>Optional free text: trimmed, and null when empty.</summary>
internal static class Text
{
    public static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
