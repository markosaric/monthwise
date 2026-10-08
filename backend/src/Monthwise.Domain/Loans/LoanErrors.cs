using Monthwise.Domain.Common;

namespace Monthwise.Domain.Loans;

/// <summary>Errors of the loan rules. Field names match the API request fields.</summary>
public static class LoanErrors
{
    public static readonly DomainError NameRequired = new("loan.name.required", "Name is required.", "name");
    public static readonly DomainError NameTooLong =
        new("loan.name.tooLong", $"Name can be at most {Loan.NameMaxLength} characters.", "name");
    public static readonly DomainError NameInUse = new("loan.name.inUse", "Another loan already has this name.", "name");
    public static readonly DomainError CategoryRequired = new("loan.category.required", "Choose a category.", "categoryId");
    public static readonly DomainError CategoryUnknown = new("loan.category.unknown", "This category does not exist.", "categoryId");
    public static readonly DomainError CategoryHidden = new("loan.category.hidden", "This category is hidden.", "categoryId");
    public static readonly DomainError SubcategoryRequired =
        new("loan.subcategory.required", "Choose a subcategory.", "subcategoryId");
    public static readonly DomainError SubcategoryUnknown =
        new("loan.subcategory.unknown", "This subcategory does not exist.", "subcategoryId");
    public static readonly DomainError SubcategoryHidden =
        new("loan.subcategory.hidden", "This subcategory is hidden.", "subcategoryId");
    public static readonly DomainError SubcategoryNotInCategory =
        new("loan.subcategory.notInCategory", "Choose a subcategory of the chosen category.", "subcategoryId");
    public static readonly DomainError LenderRequired = new("loan.lender.required", "Choose a lender.", "lenderId");
    public static readonly DomainError LenderUnknown = new("loan.lender.unknown", "This lender does not exist.", "lenderId");
    public static readonly DomainError PaymentMethodRequired =
        new("loan.paymentMethod.required", "Choose standing order or bill.", "paymentMethod");
    public static readonly DomainError AccountRequired =
        new("loan.account.required", "A standing order needs a primary account.", "accountId");
    public static readonly DomainError AccountUnknown = new("loan.account.unknown", "This account does not exist.", "accountId");
    public static readonly DomainError AccountNotPrimary = new(
        "loan.account.notPrimary", "Choose a primary account that is not blocked, expired or archived.", "accountId");
    public static readonly DomainError AccountNotForBill =
        new("loan.account.notForBill", "A bill has no account; it is chosen when a payment is paid.", "accountId");
    public static readonly DomainError StartMonthInvalid =
        new("loan.startMonth.invalid", "Start month is not valid.", "startMonth");
    public static readonly DomainError PaymentDayInvalid = new(
        "loan.paymentDay.invalid",
        $"Payment day must be between {DueDateRule.MinPaymentDay} and {DueDateRule.MaxPaymentDay}.",
        "paymentDay");
    public static readonly DomainError MonthlyPaymentNotPositive =
        new("loan.monthlyPayment.notPositive", "Monthly payment must be greater than zero.", "monthlyPaymentCents");
    public static readonly DomainError PaymentCountInvalid =
        new("loan.paymentCount.invalid", "Number of payments must be a whole number of 1 or more.", "paymentCount");
    public static readonly DomainError PaymentCountTooLarge = new(
        "loan.paymentCount.tooLarge", $"Number of payments can be at most {Loan.MaxPaymentCount}.", "paymentCount");
    public static readonly DomainError EndMonthBeforeStart =
        new("loan.endMonth.beforeStart", "End month must be the same as or later than the start month.", "endMonth");
    public static readonly DomainError EndMonthInvalid =
        new("loan.endMonth.invalid", "End month is not valid.", "endMonth");
    public static readonly DomainError PaymentCountOrEndMonthRequired =
        new("loan.paymentCount.required", "Enter the number of payments or the end month.", "paymentCount");
    public static readonly DomainError PaymentCountAndEndMonthDiffer = new(
        "loan.endMonth.mismatch", "End month does not match the number of payments.", "endMonth");
    public static readonly DomainError ResidualValueNotPositive =
        new("loan.residualValue.notPositive", "Residual value must be greater than zero.", "residualValueCents");
    public static readonly DomainError ResidualValueNotAllowed = new(
        "loan.residualValue.notAllowed", "The chosen category does not allow a residual value.", "residualValueCents");
    public static readonly DomainError ResidualValuePaid = new(
        "loan.residualValue.paid", "The residual value is already paid and cannot be changed.", "residualValueCents");
    public static readonly DomainError DescriptionTooLong = new(
        "loan.description.tooLong", $"Description can be at most {Loan.DescriptionMaxLength} characters.", "description");
    public static readonly DomainError StartAfterPaidPayment = new(
        "loan.startMonth.afterPaid", "Start month cannot be after a paid payment.", "startMonth");
    public static readonly DomainError EndBeforePaidPayment = new(
        "loan.paymentCount.beforePaid", "The loan cannot end before a paid payment.", "paymentCount");

    public static readonly DomainError ChangeFromMonthRequired =
        new("loan.change.fromMonth.required", "Choose the month the change applies from.", "fromMonth");
    public static readonly DomainError ChangeNothingChanged = new(
        "loan.change.empty", "Enter a new monthly payment, a new payment day, or both.", "monthlyPaymentCents");
    public static readonly DomainError ChangeMonthlyPaymentNotPositive = new(
        "loan.change.monthlyPayment.notPositive", "Monthly payment must be greater than zero.", "monthlyPaymentCents");
    public static readonly DomainError ChangePaymentDayInvalid = new(
        "loan.change.paymentDay.invalid",
        $"Payment day must be between {DueDateRule.MinPaymentDay} and {DueDateRule.MaxPaymentDay}.",
        "paymentDay");
    public static readonly DomainError ChangeNoteTooLong = new(
        "loan.change.note.tooLong", $"Note can be at most {PaymentChange.NoteMaxLength} characters.", "note");
    public static readonly DomainError ChangeNotAfterPaid = new(
        "loan.change.fromMonth.notAfterPaid", "The change must start after the month of the latest paid payment.", "fromMonth");
    public static readonly DomainError ChangeOutsideLoan = new(
        "loan.change.fromMonth.outside", "The change must start within the loan's payments.", "fromMonth");
    public static readonly DomainError ChangeMonthInUse = new(
        "loan.change.fromMonth.inUse", "Another payment change already starts in this month.", "fromMonth");
    public static readonly DomainError ChangeCoversPaidPayment = new(
        "loan.change.paid", "A payment this change applies to is already paid, so it cannot be changed or deleted.",
        Kind: ErrorKind.Conflict);

    public static readonly DomainError PaymentAmountNotPositive =
        new("loan.payment.amount.notPositive", "Amount must be greater than zero.", "amountCents");
    public static readonly DomainError PaymentDueDateOutsideMonth =
        new("loan.payment.dueDate.outsideMonth", "Due date must be in the payment's month.", "dueDate");
    public static readonly DomainError PaidBeforeBillDate = new(
        "loan.payment.paidDate.beforeBillDate", "Paid date must be the same as or later than the bill date.", "paidDate");
    public static readonly DomainError PaidAccountRequired =
        new("loan.payment.account.required", "Choose the account it was paid from.", "accountId");
    public static readonly DomainError PaidAccountUnknown =
        new("loan.payment.account.unknown", "This account does not exist.", "accountId");
    public static readonly DomainError PaidAccountUnusable = new(
        "loan.payment.account.unusable", "This account is blocked, expired or archived.", "accountId");
    public static readonly DomainError PaymentPaid =
        new("loan.payment.paid", "This payment is already paid. Untick it first.", Kind: ErrorKind.Conflict);

    public static readonly DomainError CategoryNameRequired = new("loanCategory.name.required", "Name is required.", "name");
    public static readonly DomainError CategoryNameTooLong =
        new("loanCategory.name.tooLong", $"Name can be at most {LoanCategory.NameMaxLength} characters.", "name");
    public static readonly DomainError CategoryNameInUse =
        new("loanCategory.name.inUse", "Another category already has this name.", "name");
    public static readonly DomainError CategoryInUse = new(
        "loanCategory.inUse", "This category is used by loans and cannot be deleted. Hide it instead.", Kind: ErrorKind.Conflict);
    public static readonly DomainError SubcategoryNameRequired =
        new("loanSubcategory.name.required", "Name is required.", "name");
    public static readonly DomainError SubcategoryNameTooLong =
        new("loanSubcategory.name.tooLong", $"Name can be at most {LoanSubcategory.NameMaxLength} characters.", "name");
    public static readonly DomainError SubcategoryNameInUse =
        new("loanSubcategory.name.inUse", "This category already has a subcategory with this name.", "name");
    public static readonly DomainError SubcategoryInUse = new(
        "loanSubcategory.inUse", "This subcategory is used by loans and cannot be deleted. Hide it instead.",
        Kind: ErrorKind.Conflict);

    public static readonly DomainError LoanNotFound = new("loan.notFound", "Loan not found.", Kind: ErrorKind.NotFound);
    public static readonly DomainError PaymentNotFound =
        new("loan.payment.notFound", "This payment does not exist.", Kind: ErrorKind.NotFound);
    public static readonly DomainError ChangeNotFound =
        new("loan.change.notFound", "Payment change not found.", Kind: ErrorKind.NotFound);
    public static readonly DomainError CategoryNotFound =
        new("loanCategory.notFound", "Category not found.", Kind: ErrorKind.NotFound);
    public static readonly DomainError SubcategoryNotFound =
        new("loanSubcategory.notFound", "Subcategory not found.", Kind: ErrorKind.NotFound);
    public static readonly DomainError LoanClosed =
        new("loan.closed", "This loan is closed and cannot be changed.", Kind: ErrorKind.Conflict);
    public static readonly DomainError LoanHasPaidPayments = new(
        "loan.hasPaidPayments", "This loan has paid payments and cannot be deleted. Close it instead.", Kind: ErrorKind.Conflict);
}
