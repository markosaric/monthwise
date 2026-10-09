using Monthwise.Application.CashFlow;
using Monthwise.Application.Loans;
using Monthwise.Domain.CardPurchases;
using Monthwise.Domain.Cards;

namespace Monthwise.Application.CardPurchases;

/// <summary>
/// Record or edit a card purchase (FR-3). <see cref="InstallmentCount"/> is needed when
/// <see cref="InInstallments"/> is true; a purchase without installments is paid in full.
/// </summary>
public sealed record CardPurchaseRequest(
    Guid CardId,
    string Description,
    DateOnly PurchaseDate,
    long AmountCents,
    bool InInstallments,
    int? InstallmentCount,
    bool PostponeFirst,
    Guid? CategoryId);

/// <summary>Reverses a purchase or an installment, with an optional reason (FR-5).</summary>
public sealed record ReversalRequest(string? Reason);

/// <summary>
/// A card that allows monthly installments, as the Cards screen lists it (FR-7): its billing details,
/// its next card payment and what is still to be charged.
/// </summary>
public sealed record InstallmentCardResponse(
    Guid Id,
    string Name,
    string? CardNumberMasked,
    CardType Type,
    CardStatus Status,
    bool CanBeUsed,
    bool NeedsBillingDetails,
    int? StatementDay,
    int? PaymentDay,
    NamedReference? PayingAccount,
    bool PaysFromAccountDirectly,
    int? MaxInstallments,
    bool NeedsAccount,
    CardPaymentResponse? NextPayment,
    long RemainingCents,
    int PurchasesWithInstallmentsLeft);

/// <summary>
/// One card payment (FR-8): the amount of its billing period, its dates and its paid state.
/// <see cref="Account"/> is the account it was paid from, or the paying account while expected.
/// </summary>
public sealed record CardPaymentResponse(
    Guid CardId,
    string CardName,
    string Month,
    DateOnly PeriodFrom,
    DateOnly StatementDate,
    DateOnly PaymentDate,
    long AmountCents,
    int LineCount,
    bool IsPaid,
    DateOnly? PaidDate,
    NamedReference? Account,
    bool PaysFromAccountDirectly,
    bool IsOverdue,
    bool NeedsAccount);

/// <summary>One line of a card payment in the drill-down (FR-8). Credits are negative.</summary>
public sealed record CardPaymentLineResponse(
    Guid PurchaseId,
    string Description,
    DateOnly PurchaseDate,
    CardPaymentLineKind Kind,
    int InstallmentNumber,
    int InstallmentTotal,
    NamedReference? Category,
    long AmountCents);

/// <summary>The drill-down of a card payment: every purchase, installment and credit in it (FR-8).</summary>
public sealed record CardPaymentDetailsResponse(CardPaymentResponse Payment, IReadOnlyList<CardPaymentLineResponse> Lines);

/// <summary>A purchase with its figures (FR-4, FR-5, FR-7).</summary>
public sealed record CardPurchaseResponse(
    Guid Id,
    NamedReference Card,
    string Description,
    DateOnly PurchaseDate,
    long AmountCents,
    bool InInstallments,
    int InstallmentCount,
    bool PostponeFirst,
    NamedReference? Category,
    DateOnly? ReversedOn,
    string? ReversalReason,
    int InstallmentsCharged,
    int InstallmentsLeft,
    long ChargedCents,
    long RemainingCents,
    bool HasChargedInstallments);

/// <summary>
/// One installment of a purchase with the dates of its card payment (FR-4) and its reversal (FR-5).
/// <see cref="CreditMonth"/> is the card payment a charged and reversed installment is credited in.
/// </summary>
public sealed record InstallmentResponse(
    int Number,
    int Total,
    long AmountCents,
    string PaymentMonth,
    DateOnly StatementDate,
    DateOnly PaymentDate,
    bool IsCharged,
    DateOnly? ReversedOn,
    string? ReversalReason,
    bool ReversedWithPurchase,
    string? CreditMonth);

public sealed record CardPurchaseDetailsResponse(CardPurchaseResponse Purchase, IReadOnlyList<InstallmentResponse> Installments);

/// <summary>The card payments of a month (<c>yyyy-MM</c>) for the Month screen, one per card, with totals (FR-8).</summary>
public sealed record MonthCardPaymentsResponse(
    string Month,
    IReadOnlyList<CardPaymentResponse> Payments,
    LoanPaymentTotalsResponse Totals);

/// <summary>
/// Ticks Paid (FR-8). Without <see cref="PaidDate"/> it is today; without <see cref="AccountId"/> it is
/// the card's paying account.
/// </summary>
public sealed record CardPaymentPaidRequest(DateOnly? PaidDate, Guid? AccountId);

public sealed record PurchaseCategoryResponse(Guid Id, string Name, bool IsHidden);
