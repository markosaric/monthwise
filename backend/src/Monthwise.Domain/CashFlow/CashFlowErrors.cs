using Monthwise.Domain.Common;

namespace Monthwise.Domain.CashFlow;

/// <summary>Errors of the input cash flow rules. Field names match the API request fields.</summary>
public static class CashFlowErrors
{
    public static readonly DomainError PayerRequired = new("input.payer.required", "Payer is required.", "payerName");
    public static readonly DomainError PayerTooLong =
        new("input.payer.tooLong", $"Payer can be at most {Payer.NameMaxLength} characters.", "payerName");
    public static readonly DomainError AccountRequired = new("input.account.required", "Choose an account.", "accountId");
    public static readonly DomainError AccountUnknown = new("input.account.unknown", "This account does not exist.", "accountId");
    public static readonly DomainError AccountUnusable = new(
        "input.account.unusable", "This account is blocked, expired or archived and cannot receive new inputs.", "accountId");
    public static readonly DomainError CategoryRequired = new("input.category.required", "Choose a category.", "categoryId");
    public static readonly DomainError CategoryUnknown = new("input.category.unknown", "This category does not exist.", "categoryId");
    public static readonly DomainError CategoryHidden = new("input.category.hidden", "This category is hidden.", "categoryId");
    public static readonly DomainError AmountNotPositive =
        new("input.amount.notPositive", "Amount must be greater than zero.", "amountCents");
    public static readonly DomainError ExpectedBeforeIssue = new(
        "input.expectedDate.beforeIssueDate", "Expected date must be the same as or later than the issue date.", "expectedDate");
    public static readonly DomainError ReceivedBeforeIssue = new(
        "input.receivedDate.beforeIssueDate", "Received date must be the same as or later than the issue date.", "receivedDate");
    public static readonly DomainError DescriptionTooLong = new(
        "input.description.tooLong", $"Description can be at most {CashFlowInput.DescriptionMaxLength} characters.", "description");
    public static readonly DomainError EndDateBeforeStart = new(
        "recurring.endDate.beforeStart", "End date must be the same as or later than the first expected date.", "endDate");
    public static readonly DomainError ScheduleBeforeReceived = new(
        "recurring.expectedDate.beforeReceived",
        "When the schedule changes, the first expected date must be after the last received occurrence.",
        "expectedDate");
    public static readonly DomainError FrequencyRequired = new("recurring.frequency.required", "Choose how often it repeats.", "frequency");

    public static readonly DomainError CategoryNameRequired = new("category.name.required", "Name is required.", "name");
    public static readonly DomainError CategoryNameTooLong =
        new("category.name.tooLong", $"Name can be at most {InputCategory.NameMaxLength} characters.", "name");
    public static readonly DomainError CategoryNameInUse = new("category.name.inUse", "Another category already has this name.", "name");
    public static readonly DomainError CategoryInUse = new(
        "category.inUse", "This category is used by inputs and cannot be deleted. Hide it instead.", Kind: ErrorKind.Conflict);

    public static readonly DomainError PayerNameRequired = new("payer.name.required", "Name is required.", "name");
    public static readonly DomainError PayerNameTooLong =
        new("payer.name.tooLong", $"Name can be at most {Payer.NameMaxLength} characters.", "name");
    public static readonly DomainError PayerNameInUse = new("payer.name.inUse", "Another payer already has this name.", "name");

    public static readonly DomainError BalanceNoteTooLong = new(
        "balance.note.tooLong", $"Note can be at most {BalanceEntry.NoteMaxLength} characters.", "note");
    public static readonly DomainError StartingBalanceExists = new(
        "balance.starting.exists", "This account already has a starting balance. Add a correction instead.", "kind");
    public static readonly DomainError KindInvalid = new("balance.kind.invalid", "Choose starting balance or correction.", "kind");

    public static readonly DomainError InputNotFound = new("input.notFound", "Input not found.", Kind: ErrorKind.NotFound);
    public static readonly DomainError RecurringNotFound =
        new("recurring.notFound", "Recurring input not found.", Kind: ErrorKind.NotFound);
    public static readonly DomainError OccurrenceNotFound =
        new("recurring.occurrence.notFound", "This occurrence does not exist.", Kind: ErrorKind.NotFound);
    public static readonly DomainError PayerNotFound = new("payer.notFound", "Payer not found.", Kind: ErrorKind.NotFound);
    public static readonly DomainError CategoryNotFound = new("category.notFound", "Category not found.", Kind: ErrorKind.NotFound);
    public static readonly DomainError BalanceEntryNotFound =
        new("balance.notFound", "Balance entry not found.", Kind: ErrorKind.NotFound);
    public static readonly DomainError RecurringStopped =
        new("recurring.stopped", "This recurring input has already ended.", Kind: ErrorKind.Conflict);
}
