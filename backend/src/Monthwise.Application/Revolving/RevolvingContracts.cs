using Monthwise.Application.CashFlow;
using Monthwise.Application.Loans;
using Monthwise.Domain.Cards;
using Monthwise.Domain.Revolving;

namespace Monthwise.Application.Revolving;

/// <summary>
/// One version of a revolving card's terms (revolving FR-2). Percentages are basis points (8.33 % is
/// 833). <see cref="FromMonth"/> is null for the first version, which covers every earlier month.
/// </summary>
public sealed record RevolvingTermsResponse(
    string? FromMonth,
    int MinPaymentBasisPoints,
    long MinPaymentAmountCents,
    int InterestBasisPoints,
    int? MinPaymentInterestBasisPoints,
    int BillDay,
    int DueDay);

/// <summary>
/// The revolving terms of a card as the card form edits them: the ones from next month on, the paying
/// account, and every version in <see cref="Terms"/> (revolving FR-2).
/// </summary>
public sealed record CardRevolvingResponse(
    int MinPaymentBasisPoints,
    long MinPaymentAmountCents,
    int InterestBasisPoints,
    int? MinPaymentInterestBasisPoints,
    int BillDay,
    int DueDay,
    NamedReference PayingAccount,
    bool PaysFromAccountDirectly,
    IReadOnlyList<RevolvingTermsResponse> Terms);

/// <summary>One monthly bill (revolving FR-4, FR-8). An estimated bill is not made yet.</summary>
public sealed record RevolvingBillResponse(
    Guid CardId,
    string CardName,
    string Month,
    DateOnly BillDate,
    DateOnly DueDate,
    long DebtCents,
    long InterestCents,
    long MinimumCents,
    bool IsCorrected,
    bool IsEstimated,
    bool IsPaid,
    DateOnly? PaidDate,
    long? PaidAmountCents,
    NamedReference? Account,
    bool PaysFromAccountDirectly,
    bool PaidAutomatically,
    bool IsOverdue,
    bool NeedsAccount);

/// <summary>An extra payment to a revolving card (revolving FR-8).</summary>
public sealed record ExtraPaymentResponse(
    Guid Id, Guid CardId, string CardName, NamedReference Account, DateOnly Date, long AmountCents, string? Description);

/// <summary>
/// A starting debt, a correction or a transfer (revolving FR-5, FR-9). For a starting debt or a
/// correction, <see cref="AmountCents"/> is the debt entered and <see cref="DifferenceCents"/> the
/// difference to the calculated debt before it.
/// </summary>
public sealed record DebtEntryResponse(
    Guid Id,
    Guid CardId,
    DebtEntryKind Kind,
    DateOnly Date,
    long AmountCents,
    long DifferenceCents,
    string? Note,
    NamedReference? LinkedCard);

/// <summary>A bill, expense or purchase paid with the card, or the credit of a reversed purchase (revolving FR-6).</summary>
public sealed record RevolvingChargeResponse(
    Guid SourceId, RevolvingChargeKind Kind, DateOnly Date, string Description, NamedReference? Category, long AmountCents);

/// <summary>One change of the debt, with the debt after it (revolving FR-10).</summary>
public sealed record RevolvingMovementResponse(
    DateOnly Date, RevolvingMovementKind Kind, long ChangeCents, long DebtAfterCents, Guid? SourceId, string? BillMonth);

/// <summary>A revolving card as the Cards screen lists it (revolving FR-5, FR-10).</summary>
public sealed record RevolvingCardResponse(
    Guid Id,
    string Name,
    string? CardNumberMasked,
    CardStatus Status,
    bool CanBeUsed,
    long DebtCents,
    long DebtLimitCents,
    long RemainingLimitCents,
    bool IsOverLimit,
    RevolvingBillResponse? NextBill,
    CardRevolvingResponse? Terms,
    bool NeedsAccount);

/// <summary>The revolving cards of the Cards screen and their debt together (revolving FR-10).</summary>
public sealed record RevolvingCardsResponse(IReadOnlyList<RevolvingCardResponse> Cards, long TotalDebtCents);

/// <summary>The revolving card page (revolving FR-10): terms, bills, payments, charges, entries and the debt history up to today.</summary>
public sealed record RevolvingCardDetailsResponse(
    RevolvingCardResponse Card,
    IReadOnlyList<RevolvingBillResponse> Bills,
    IReadOnlyList<ExtraPaymentResponse> ExtraPayments,
    IReadOnlyList<DebtEntryResponse> DebtEntries,
    IReadOnlyList<RevolvingChargeResponse> Charges,
    IReadOnlyList<RevolvingMovementResponse> Movements);

/// <summary>The monthly bills and extra payments of a month for the Month screen (revolving FR-11).</summary>
public sealed record MonthRevolvingResponse(
    string Month,
    IReadOnlyList<RevolvingBillResponse> Bills,
    IReadOnlyList<ExtraPaymentResponse> ExtraPayments,
    LoanPaymentTotalsResponse Totals);

/// <summary>The debt of a card on a date, for the extra payment form (Pay the whole debt).</summary>
public sealed record RevolvingDebtResponse(Guid CardId, DateOnly Date, long DebtCents);

/// <summary>Pays a monthly bill (FR-8). Defaults: today, the paying account and the minimum payment.</summary>
public sealed record RevolvingBillPaidRequest(DateOnly? PaidDate, Guid? AccountId, long? AmountCents);

/// <summary>Corrects a monthly bill to the bank's bill (FR-4, FR-7). Null keeps the calculated value.</summary>
public sealed record RevolvingBillCorrectionRequest(long? MinimumCents, long? InterestCents);

/// <summary>Records or edits an extra payment (FR-8, X2 to X5).</summary>
public sealed record ExtraPaymentRequest(Guid AccountId, DateOnly Date, long AmountCents, string? Description);

/// <summary>Records or edits a starting debt or a correction (FR-5): the debt on <see cref="Date"/>.</summary>
public sealed record DebtEntryRequest(DebtEntryKind Kind, DateOnly Date, long DebtCents, string? Note);
