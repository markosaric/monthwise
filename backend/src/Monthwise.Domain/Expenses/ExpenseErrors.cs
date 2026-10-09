using Monthwise.Domain.Common;

namespace Monthwise.Domain.Expenses;

/// <summary>Errors of the bill, expense and expense category rules. Field names match the API request fields.</summary>
public static class ExpenseErrors
{
    public static readonly DomainError NameRequired = new("bill.name.required", "Name is required.", "name");
    public static readonly DomainError NameTooLong =
        new("bill.name.tooLong", $"Name can be at most {RecurringBill.NameMaxLength} characters.", "name");
    public static readonly DomainError NameInUse =
        new("recurringBill.name.inUse", "Another recurring bill already has this name.", "name");
    public static readonly DomainError PayeeTooLong =
        new("bill.payee.tooLong", $"Payee can be at most {RecurringBill.PayeeMaxLength} characters.", "payee");
    public static readonly DomainError BillDescriptionTooLong = new(
        "bill.description.tooLong", $"Description can be at most {RecurringBill.DescriptionMaxLength} characters.", "description");
    public static readonly DomainError ExpenseDescriptionTooLong = new(
        "expense.description.tooLong", $"Description can be at most {Expense.DescriptionMaxLength} characters.", "description");
    public static readonly DomainError AmountNotPositive =
        new("expense.amount.notPositive", "Amount must be greater than zero.", "amountCents");
    public static readonly DomainError IssueDateRequired = new("bill.issueDate.required", "Issue date is required.", "issueDate");
    public static readonly DomainError DueDateRequired = new("bill.dueDate.required", "Due date is required.", "dueDate");
    public static readonly DomainError DueBeforeIssue =
        new("bill.dueDate.beforeIssue", "Due date must be the same as or later than the issue date.", "dueDate");
    public static readonly DomainError PaidBeforeIssue =
        new("bill.paidDate.beforeIssue", "Paid date must be the same as or later than the issue date.", "paidDate");
    public static readonly DomainError BillNotIssued = new(
        "bill.notIssued", "Enter the amount, issue date and due date before marking the bill as paid.", "paidDate");

    public static readonly DomainError CategoryRequired = new("expense.category.required", "Choose a category.", "categoryId");
    public static readonly DomainError CategoryUnknown = new("expense.category.unknown", "This category does not exist.", "categoryId");
    public static readonly DomainError CategoryHidden = new("expense.category.hidden", "This category is hidden.", "categoryId");
    public static readonly DomainError CategoryCashWithdrawal = new(
        "expense.category.cashWithdrawal", "Record a cash withdrawal with Cash withdrawal instead.", "categoryId");

    public static readonly DomainError PaidFromRequired = new("expense.paidFrom.required", "Choose what it was paid with.", "paidFromId");
    public static readonly DomainError PaidFromUnknown = new("expense.paidFrom.unknown", "This account or card does not exist.", "paidFromId");
    public static readonly DomainError PaidFromUnusable = new(
        "expense.paidFrom.unusable", "This account or card is blocked, expired, archived or not valid yet.", "paidFromId");
    public static readonly DomainError PaidFromNeedsBillingDetails = new(
        "expense.paidFrom.needsBillingDetails", "Set the card's statement date, payment date and paying account first.", "paidFromId");
    public static readonly DomainError PaidFromNotValidYet =
        new("expense.paidFrom.notValidYet", "The date is before the card's valid from.", "paidFromId");
    public static readonly DomainError CashWithdrawalNotPrimary = new(
        "expense.cashWithdrawal.notPrimary", "Cash can only be withdrawn from a primary account.", "paidFromId");

    public static readonly DomainError BillNotFound = new("bill.notFound", "Bill not found.", Kind: ErrorKind.NotFound);
    public static readonly DomainError RecurringBillNotFound =
        new("recurringBill.notFound", "Recurring bill not found.", Kind: ErrorKind.NotFound);
    public static readonly DomainError RecurringBillInUse = new(
        "recurringBill.inUse", "Bills were already added from this recurring bill. Stop it instead.", Kind: ErrorKind.Conflict);
    public static readonly DomainError ExpenseNotFound = new("expense.notFound", "Expense not found.", Kind: ErrorKind.NotFound);

    public static readonly DomainError CategoryNameRequired = new("expenseCategory.name.required", "Name is required.", "name");
    public static readonly DomainError CategoryNameTooLong = new(
        "expenseCategory.name.tooLong", $"Name can be at most {ExpenseCategory.NameMaxLength} characters.", "name");
    public static readonly DomainError CategoryNameInUse =
        new("expenseCategory.name.inUse", "Another category already has this name.", "name");
    public static readonly DomainError CategoryInUse = new(
        "expenseCategory.inUse", "This category is used and cannot be deleted. Hide it instead.", Kind: ErrorKind.Conflict);
    public static readonly DomainError CategoryFixed = new(
        "expenseCategory.fixed", "Cash withdrawal cannot be renamed, hidden or deleted.", Kind: ErrorKind.Conflict);
    public static readonly DomainError CategoryNotFound =
        new("expenseCategory.notFound", "Category not found.", Kind: ErrorKind.NotFound);
}
