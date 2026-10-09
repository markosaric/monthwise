using Monthwise.Domain.Common;

namespace Monthwise.Domain.CardPurchases;

/// <summary>Errors of the card purchase rules. Field names match the API request fields.</summary>
public static class CardPurchaseErrors
{
    public static readonly DomainError CardRequired = new("purchase.card.required", "Choose a card.", "cardId");
    public static readonly DomainError CardUnknown = new("purchase.card.unknown", "This card does not exist.", "cardId");
    public static readonly DomainError CardNoInstallments = new(
        "purchase.card.noInstallments", "This card does not allow monthly installments.", "cardId");
    public static readonly DomainError CardNeedsBillingDetails = new(
        "purchase.card.needsBillingDetails", "Set the card's statement date, payment date and paying account first.", "cardId");
    public static readonly DomainError CardUnusable = new(
        "purchase.card.unusable", "This card is blocked, expired, archived or not valid yet.", "cardId");
    public static readonly DomainError DescriptionRequired =
        new("purchase.description.required", "Description is required.", "description");
    public static readonly DomainError DescriptionTooLong = new(
        "purchase.description.tooLong", $"Description can be at most {CardPurchase.DescriptionMaxLength} characters.", "description");
    public static readonly DomainError PurchaseDateBeforeValidFrom = new(
        "purchase.purchaseDate.beforeValidFrom", "The purchase date is before the card's valid from.", "purchaseDate");
    public static readonly DomainError AmountNotPositive =
        new("purchase.amount.notPositive", "Amount must be greater than zero.", "amountCents");
    public static readonly DomainError InstallmentCountRequired = new(
        "purchase.installmentCount.required", "Enter the number of installments.", "installmentCount");
    public static readonly DomainError InstallmentCountTooSmall = new(
        "purchase.installmentCount.tooSmall", $"A purchase in installments has at least {CardPurchase.MinInstallments} installments.",
        "installmentCount");
    public static readonly DomainError InstallmentCountTooLarge = new(
        "purchase.installmentCount.tooLarge", "This card allows fewer installments.", "installmentCount");
    public static readonly DomainError CategoryUnknown =
        new("purchase.category.unknown", "This category does not exist.", "categoryId");
    public static readonly DomainError CategoryHidden = new("purchase.category.hidden", "This category is hidden.", "categoryId");
    public static readonly DomainError ReasonTooLong = new(
        "purchase.reason.tooLong", $"Reason can be at most {CardPurchase.ReasonMaxLength} characters.", "reason");

    public static readonly DomainError NotFound = new("purchase.notFound", "Purchase not found.", Kind: ErrorKind.NotFound);
    public static readonly DomainError InstallmentNotFound =
        new("purchase.installment.notFound", "This installment does not exist.", Kind: ErrorKind.NotFound);
    public static readonly DomainError Reversed = new(
        "purchase.reversed", "This purchase is reversed and cannot be changed.", Kind: ErrorKind.Conflict);
    public static readonly DomainError InstallmentReversed = new(
        "purchase.installment.reversed", "This installment is already reversed.", Kind: ErrorKind.Conflict);

    public static readonly DomainError CategoryNameRequired = new("purchaseCategory.name.required", "Name is required.", "name");
    public static readonly DomainError CategoryNameTooLong = new(
        "purchaseCategory.name.tooLong", $"Name can be at most {PurchaseCategory.NameMaxLength} characters.", "name");
    public static readonly DomainError CategoryNameInUse =
        new("purchaseCategory.name.inUse", "Another category already has this name.", "name");
    public static readonly DomainError CategoryInUse = new(
        "purchaseCategory.inUse", "This category is used by purchases and cannot be deleted. Hide it instead.", Kind: ErrorKind.Conflict);
    public static readonly DomainError CategoryNotFound =
        new("purchaseCategory.notFound", "Category not found.", Kind: ErrorKind.NotFound);

    public static readonly DomainError PaymentNotFound =
        new("cardPayment.notFound", "This card payment does not exist.", Kind: ErrorKind.NotFound);
    public static readonly DomainError PaidBeforeStatement = new(
        "cardPayment.paidDate.beforeStatement", "Paid date must be the same as or later than the statement date.", "paidDate");
    public static readonly DomainError PaidAccountRequired =
        new("cardPayment.account.required", "Choose the account it was paid from.", "accountId");
    public static readonly DomainError PaidAccountUnknown =
        new("cardPayment.account.unknown", "This account does not exist.", "accountId");
    public static readonly DomainError PaidAccountUnusable = new(
        "cardPayment.account.unusable", "This account is blocked, expired or archived.", "accountId");
}
