using Monthwise.Domain.Common;

namespace Monthwise.Domain.Cards;

/// <summary>Errors of the card rules. Field names match the API request fields.</summary>
public static class CardErrors
{
    public static readonly DomainError NameRequired = new("card.name.required", "Name is required.", "name");
    public static readonly DomainError NameTooLong =
        new("card.name.tooLong", $"Name can be at most {Card.NameMaxLength} characters.", "name");
    public static readonly DomainError NameInUse =
        new("card.name.inUse", "Another card already has this name.", "name");
    public static readonly DomainError CardNumberInvalid =
        new("card.cardNumber.invalid", "Card number is not valid.", "cardNumber");
    public static readonly DomainError CardNumberInUse =
        new("card.cardNumber.inUse", "This card number is already registered.", "cardNumber");
    public static readonly DomainError IbanInvalid = new("card.iban.invalid", "IBAN is not valid.", "iban");
    public static readonly DomainError IbanInUse =
        new("card.iban.inUse", "This IBAN is already registered.", "iban");
    public static readonly DomainError IssuerRequired = new("card.issuer.required", "Choose an issuer.", "issuerId");
    public static readonly DomainError IssuerUnknown = new("card.issuer.unknown", "This issuer does not exist.", "issuerId");
    public static readonly DomainError BankRequired = new("card.bank.required", "Choose a bank.", "bankId");
    public static readonly DomainError BankUnknown = new("card.bank.unknown", "This bank does not exist.", "bankId");
    public static readonly DomainError ValidityOrder =
        new("card.validThrough.beforeValidFrom", "Valid through must be the same as or later than valid from.", "validThrough");
    public static readonly DomainError DebtLimitNegative =
        new("card.debtLimit.negative", "Max debt limit cannot be negative.", "debtLimitCents");
    public static readonly DomainError PrimaryNotDebit =
        new("card.isPrimary.notDebit", "Only debit cards can be a primary account.", "isPrimary");
    public static readonly DomainError InstallmentsNotDebit = new(
        "card.allowsInstallments.notDebit", "Only debit cards have a monthly installments option.", "allowsInstallments");
    public static readonly DomainError BillingRequired = new(
        "card.billing.required", "A card that allows monthly installments needs its statement date and payment date.", "statementDay");
    public static readonly DomainError StatementDayInvalid = new(
        "card.statementDay.invalid",
        $"Statement date must be a day between {CardBillingTerms.MinStatementDay} and {CardBillingTerms.MaxStatementDay}.",
        "statementDay");
    public static readonly DomainError PaymentDayInvalid = new(
        "card.paymentDay.invalid",
        $"Payment date must be a day between {CardBillingTerms.MinPaymentDay} and {CardBillingTerms.MaxPaymentDay}.",
        "paymentDay");
    public static readonly DomainError PaymentDayNotAfterStatement = new(
        "card.paymentDay.notAfterStatement", "Payment date must be later than the statement date.", "paymentDay");
    public static readonly DomainError PayingAccountRequired =
        new("card.payingAccount.required", "Choose the primary account card payments are paid from.", "payingAccountId");
    public static readonly DomainError PayingAccountUnknown =
        new("card.payingAccount.unknown", "This account does not exist.", "payingAccountId");
    public static readonly DomainError PayingAccountNotPrimary = new(
        "card.payingAccount.notPrimary", "Choose a primary account that is not blocked, expired or archived.", "payingAccountId");
    public static readonly DomainError MaxInstallmentsInvalid =
        new("card.maxInstallments.invalid", "Max installments must be 12, 24 or 36.", "maxInstallments");
    public static readonly DomainError InstallmentsInUse = new(
        "card.allowsInstallments.inUse",
        "This card has purchases, so it must keep allowing monthly installments.",
        "allowsInstallments");
    public static readonly DomainError ReasonTooLong =
        new("card.reason.tooLong", $"Reason can be at most {Card.ReasonMaxLength} characters.", "reason");
    public static readonly DomainError ReplacementIsSameCard =
        new("card.replacement.sameCard", "A card cannot replace itself.", "replacementCardId");
    public static readonly DomainError ReplacementUnusable = new(
        "card.replacement.unusable", "The replacement card is archived, blocked or expired.", "replacementCardId");
    public static readonly DomainError ReplacementUnknown =
        new("card.replacement.unknown", "The replacement card does not exist.", "replacementCardId");

    public static readonly DomainError NotFound = new("card.notFound", "Card not found.", Kind: ErrorKind.NotFound);
    public static readonly DomainError ReadOnly = new(
        "card.readOnly", "This card is blocked, expired or archived and cannot be changed.", Kind: ErrorKind.Conflict);
    public static readonly DomainError NotBlocked = new("card.notBlocked", "This card is not blocked.", Kind: ErrorKind.Conflict);
    public static readonly DomainError AlreadyBlocked =
        new("card.alreadyBlocked", "This card is already blocked.", Kind: ErrorKind.Conflict);
    public static readonly DomainError AlreadyExpired =
        new("card.alreadyExpired", "This card is already marked as expired.", Kind: ErrorKind.Conflict);
    public static readonly DomainError AlreadyArchived =
        new("card.alreadyArchived", "This card is already archived.", Kind: ErrorKind.Conflict);
}
