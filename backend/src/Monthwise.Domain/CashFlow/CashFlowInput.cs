using Monthwise.Domain.Common;
using Monthwise.Domain.Loans;
using Monthwise.Domain.SharedCosts;

namespace Monthwise.Domain.CashFlow;

/// <summary>
/// One incoming cash flow (requirements: docs/feature-requirements/input-cash-flow.md). An input is
/// received when it has a <see cref="ReceivedDate"/>. A stored occurrence of a recurring input keeps
/// the template id and its scheduled <see cref="OccurrenceDate"/>.
/// </summary>
public sealed class CashFlowInput
{
    public const int DescriptionMaxLength = 200;

    private CashFlowInput()
    {
    }

    public Guid Id { get; private set; }
    public Guid PayerId { get; private set; }
    public Guid CategoryId { get; private set; }
    public Guid AccountId { get; private set; }
    public Money Amount { get; private set; }
    public DateOnly IssueDate { get; private set; }
    public DateOnly ExpectedDate { get; private set; }
    public DateOnly? ReceivedDate { get; private set; }
    public string? Description { get; private set; }
    public Guid? RecurringInputId { get; private set; }
    public DateOnly? OccurrenceDate { get; private set; }

    /// <summary>The shared bill of a stored contribution (shared bills FR-5).</summary>
    public Guid? ContributionBillId { get; private set; }

    /// <summary>The loan, scheduled month and kind of the shared loan payment of a stored contribution.</summary>
    public Guid? ContributionLoanId { get; private set; }

    public YearMonth? ContributionMonth { get; private set; }
    public LoanPaymentKind? ContributionPaymentKind { get; private set; }

    /// <summary>A contribution the user edited: it keeps its values until reset to the part (shared bills FR-6).</summary>
    public bool IsEdited { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public bool IsReceived => ReceivedDate is not null;

    /// <summary>The date that decides the month: received date once received, expected date before.</summary>
    public DateOnly MonthDate => ReceivedDate ?? ExpectedDate;

    public YearMonth Month => YearMonth.FromDate(MonthDate);

    /// <summary>The shared cost of a stored contribution; null for any other input.</summary>
    public ContributionSource? Contribution =>
        ContributionBillId is { } billId
            ? ContributionSource.ForBill(billId)
            : ContributionLoanId is { } loanId && ContributionMonth is { } month && ContributionPaymentKind is { } kind
                ? ContributionSource.ForLoanPayment(loanId, month, kind)
                : null;

    public static Result<CashFlowInput> Create(InputDetails details, DateTimeOffset now) =>
        Create(details, recurringInputId: null, occurrenceDate: null, now);

    /// <summary>Stores one occurrence of a recurring input, e.g. when the user changes or receives it.</summary>
    public static Result<CashFlowInput> CreateOccurrence(
        InputDetails details, Guid recurringInputId, DateOnly occurrenceDate, DateTimeOffset now) =>
        Create(details, recurringInputId, occurrenceDate, now);

    /// <summary>Stores a contribution, e.g. when it is received or edited (shared bills FR-6).</summary>
    public static Result<CashFlowInput> CreateContribution(InputDetails details, ContributionSource source, bool isEdited, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(source);
        var created = Create(details, recurringInputId: null, occurrenceDate: null, now);
        if (created.IsSuccess)
        {
            var input = created.Value;
            input.ContributionBillId = source.BillId;
            input.ContributionLoanId = source.LoanId;
            input.ContributionMonth = source.Month;
            input.ContributionPaymentKind = source.PaymentKind;
            input.IsEdited = isEdited;
        }
        return created;
    }

    /// <summary>Marks a stored contribution as edited, or as following its part again.</summary>
    public void SetEdited(bool isEdited, DateTimeOffset now)
    {
        IsEdited = isEdited;
        UpdatedAt = now;
    }

    /// <summary>Gives a received contribution the current part again (Reset to part, shared bills FR-6).</summary>
    public void ResetAmount(Money part, DateTimeOffset now)
    {
        Amount = part;
        IsEdited = false;
        UpdatedAt = now;
    }

    /// <summary>Unlinks a contribution from its shared cost, e.g. when the bill is deleted: it stays as an ordinary input.</summary>
    public void Detach(DateTimeOffset now)
    {
        ContributionBillId = null;
        ContributionLoanId = null;
        ContributionMonth = null;
        ContributionPaymentKind = null;
        IsEdited = false;
        UpdatedAt = now;
    }

    public Result Update(InputDetails details, DateTimeOffset now)
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

    /// <summary>Ticks Received: sets the received date to today and keeps the issue and expected dates (FR-3).</summary>
    public Result MarkReceived(DateOnly today, DateTimeOffset now)
    {
        if (IsReceived)
        {
            return Result.Success();
        }
        if (today < IssueDate)
        {
            return Result.Failure(CashFlowErrors.ReceivedBeforeIssue);
        }
        ReceivedDate = today;
        UpdatedAt = now;
        return Result.Success();
    }

    /// <summary>Unticks Received: the input is expected again and the received date is cleared (FR-3).</summary>
    public void MarkExpected(DateTimeOffset now)
    {
        ReceivedDate = null;
        UpdatedAt = now;
    }

    public InputDetails Details => new(PayerId, CategoryId, AccountId, Amount, IssueDate, ExpectedDate, ReceivedDate, Description);

    /// <summary>The rules of section 3 of the requirements; an empty list when the details are valid.</summary>
    public static List<DomainError> Validate(InputDetails details)
    {
        var errors = new List<DomainError>();
        if (details.PayerId == Guid.Empty)
        {
            errors.Add(CashFlowErrors.PayerRequired);
        }
        if (details.CategoryId == Guid.Empty)
        {
            errors.Add(CashFlowErrors.CategoryRequired);
        }
        if (details.AccountId == Guid.Empty)
        {
            errors.Add(CashFlowErrors.AccountRequired);
        }
        if (!details.Amount.IsPositive)
        {
            errors.Add(CashFlowErrors.AmountNotPositive);
        }
        if (details.ExpectedDate < details.IssueDate)
        {
            errors.Add(CashFlowErrors.ExpectedBeforeIssue);
        }
        if (details.ReceivedDate is { } received && received < details.IssueDate)
        {
            errors.Add(CashFlowErrors.ReceivedBeforeIssue);
        }
        if (NormalizeDescription(details.Description) is { Length: > DescriptionMaxLength })
        {
            errors.Add(CashFlowErrors.DescriptionTooLong);
        }
        return errors;
    }

    internal static string? NormalizeDescription(string? description) =>
        string.IsNullOrWhiteSpace(description) ? null : description.Trim();

    private static Result<CashFlowInput> Create(
        InputDetails details, Guid? recurringInputId, DateOnly? occurrenceDate, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(details);
        var errors = Validate(details);
        if (errors.Count > 0)
        {
            return Result.Failure<CashFlowInput>(errors);
        }

        var input = new CashFlowInput
        {
            Id = Guid.CreateVersion7(now),
            RecurringInputId = recurringInputId,
            OccurrenceDate = occurrenceDate,
            CreatedAt = now,
        };
        input.Apply(details, now);
        return Result.Success(input);
    }

    private void Apply(InputDetails details, DateTimeOffset now)
    {
        PayerId = details.PayerId;
        CategoryId = details.CategoryId;
        AccountId = details.AccountId;
        Amount = details.Amount;
        IssueDate = details.IssueDate;
        ExpectedDate = details.ExpectedDate;
        ReceivedDate = details.ReceivedDate;
        Description = NormalizeDescription(details.Description);
        UpdatedAt = now;
    }
}
