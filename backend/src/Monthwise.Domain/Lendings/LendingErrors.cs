using Monthwise.Domain.CashFlow;
using Monthwise.Domain.Common;

namespace Monthwise.Domain.Lendings;

/// <summary>Errors of the lending and repayment rules (money lending). Field names match the API request fields.</summary>
public static class LendingErrors
{
    public static readonly DomainError BorrowerRequired = new("lending.borrower.required", "Borrower is required.", "borrowerName");
    public static readonly DomainError BorrowerTooLong = new(
        "lending.borrower.tooLong", $"Borrower can be at most {Payer.NameMaxLength} characters.", "borrowerName");
    public static readonly DomainError AmountNotPositive =
        new("lending.amount.notPositive", "Amount must be greater than zero.", "amountCents");
    public static readonly DomainError AmountBelowReturned = new(
        "lending.amount.belowReturned", "The amount cannot be less than what was already returned.", "amountCents");
    public static readonly DomainError FromAccountRequired =
        new("lending.fromAccount.required", "Choose the account the money left.", "fromAccountId");
    public static readonly DomainError FromAccountUnknown =
        new("lending.fromAccount.unknown", "This account does not exist.", "fromAccountId");
    public static readonly DomainError FromAccountUnusable = new(
        "lending.fromAccount.unusable", "Choose an account that is not a credit card and not blocked, expired or archived.", "fromAccountId");
    public static readonly DomainError ReturnBeforeLend = new(
        "lending.expectedReturnDate.beforeLend", "Expected return date must be the same as or later than the lend date.", "expectedReturnDate");
    public static readonly DomainError LendAfterRepayment = new(
        "lending.lendDate.afterRepayment", "The lend date cannot be after a repayment.", "lendDate");
    public static readonly DomainError DescriptionTooLong = new(
        "lending.description.tooLong", $"Description can be at most {Lending.DescriptionMaxLength} characters.", "description");

    public static readonly DomainError RepaymentAmountNotPositive =
        new("repayment.amount.notPositive", "Amount must be greater than zero.", "amountCents");
    public static readonly DomainError RepaymentAboveOutstanding = new(
        "repayment.amount.aboveOutstanding", "The repayment cannot be more than the outstanding amount.", "amountCents");
    public static readonly DomainError ToAccountRequired =
        new("repayment.toAccount.required", "Choose the account the money arrived on.", "toAccountId");
    public static readonly DomainError ToAccountUnknown =
        new("repayment.toAccount.unknown", "This account does not exist.", "toAccountId");
    public static readonly DomainError ToAccountUnusable = new(
        "repayment.toAccount.unusable", "Choose an account that is not a credit card and not blocked, expired or archived.", "toAccountId");
    public static readonly DomainError ReceivedBeforeLend = new(
        "repayment.receivedDate.beforeLend", "Received date must be the same as or later than the lend date.", "receivedDate");
    public static readonly DomainError NoteTooLong = new(
        "repayment.note.tooLong", $"Note can be at most {LendingRepayment.NoteMaxLength} characters.", "note");

    public static readonly DomainError LendingNotFound = new("lending.notFound", "Lending not found.", Kind: ErrorKind.NotFound);
    public static readonly DomainError RepaymentNotFound = new("repayment.notFound", "Repayment not found.", Kind: ErrorKind.NotFound);
}
