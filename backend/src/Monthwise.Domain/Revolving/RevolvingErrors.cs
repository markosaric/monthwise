using Monthwise.Domain.Common;

namespace Monthwise.Domain.Revolving;

/// <summary>Errors of the revolving card rules. Field names match the API request fields.</summary>
public static class RevolvingErrors
{
    public static readonly DomainError CardUnknown = new("revolving.card.unknown", "This revolving card does not exist.", Kind: ErrorKind.NotFound);
    public static readonly DomainError AmountNotPositive =
        new("revolving.amount.notPositive", "Amount must be more than zero.", "amountCents");
    public static readonly DomainError AmountOverDebt =
        new("revolving.amount.overDebt", "Amount cannot be more than the debt on that day.", "amountCents");
    public static readonly DomainError DateInFuture =
        new("revolving.date.future", "An extra payment is always recorded as paid: its date cannot be in the future.", "date");
    public static readonly DomainError DescriptionTooLong = new(
        "revolving.description.tooLong", $"Description can be at most {ExtraPayment.DescriptionMaxLength} characters.", "description");
    public static readonly DomainError AccountRequired = new("revolving.account.required", "Choose the account it was paid from.", "accountId");
    public static readonly DomainError AccountUnknown = new("revolving.account.unknown", "This account does not exist.", "accountId");
    public static readonly DomainError AccountUnusable = new(
        "revolving.account.unusable", "Choose an account that is not blocked, expired or archived.", "accountId");
    public static readonly DomainError CardUnusable = new(
        "revolving.card.unusable", "This revolving card is not active.", "cardId");
    public static readonly DomainError DebtNegative = new("revolving.debt.negative", "Debt cannot be negative.", "debtCents");
    public static readonly DomainError NoteTooLong =
        new("revolving.note.tooLong", $"Note can be at most {DebtEntry.NoteMaxLength} characters.", "note");
    public static readonly DomainError PaidBeforeBill =
        new("revolving.paidDate.beforeBill", "A monthly bill cannot be paid before its bill date.", "paidDate");
    public static readonly DomainError CorrectionNegative =
        new("revolving.correction.negative", "Minimum payment and interest cannot be negative.", "minimumCents");

    public static readonly DomainError PaymentNotFound =
        new("revolving.payment.notFound", "Extra payment not found.", Kind: ErrorKind.NotFound);
    public static readonly DomainError EntryNotFound =
        new("revolving.entry.notFound", "Debt entry not found.", Kind: ErrorKind.NotFound);
    public static readonly DomainError BillNotFound =
        new("revolving.bill.notFound", "There is no monthly bill in this month.", Kind: ErrorKind.NotFound);
    public static readonly DomainError BillEstimated = new(
        "revolving.bill.estimated", "This monthly bill is not made yet: it is only an estimate.", Kind: ErrorKind.Conflict);
    public static readonly DomainError TransferReadOnly = new(
        "revolving.entry.transfer", "A debt transfer to or from a replacement card cannot be changed.", Kind: ErrorKind.Conflict);
}
