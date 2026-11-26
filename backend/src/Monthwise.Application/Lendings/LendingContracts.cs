using Monthwise.Application.CashFlow;
using Monthwise.Domain.Lendings;

namespace Monthwise.Application.Lendings;

/// <summary>A lending as the user enters it (money lending L1 to L6). The borrower is a payer, found or created by name (FR-6).</summary>
public sealed record LendingRequest(
    string? BorrowerName,
    long AmountCents,
    DateOnly LendDate,
    Guid FromAccountId,
    DateOnly ExpectedReturnDate,
    string? Description);

/// <summary>A repayment as the user enters it (R1 to R4).</summary>
public sealed record RepaymentRequest(long AmountCents, Guid ToAccountId, DateOnly ReceivedDate, string? Note);

public sealed record RepaymentResponse(
    Guid Id,
    long AmountCents,
    NamedReference ToAccount,
    DateOnly ReceivedDate,
    string? Note);

/// <summary>A new repayment: its id (for its address) and the lending it belongs to.</summary>
public sealed record RepaymentCreatedResponse(Guid RepaymentId, LendingResponse Lending);

/// <summary>One lending with its repayments, what is returned and outstanding, and its state (FR-1, FR-4, FR-5).</summary>
public sealed record LendingResponse(
    Guid Id,
    NamedReference Borrower,
    long AmountCents,
    DateOnly LendDate,
    NamedReference FromAccount,
    DateOnly ExpectedReturnDate,
    string? Description,
    long ReturnedCents,
    long OutstandingCents,
    LendingState State,
    IReadOnlyList<RepaymentResponse> Repayments);

/// <summary>
/// The Lending screen (FR-1): open and overdue lendings by expected return date, or returned ones latest
/// first, with the total outstanding and overdue amounts of all lendings that are not returned.
/// </summary>
public sealed record LendingListResponse(
    IReadOnlyList<LendingResponse> Lendings,
    long OutstandingCents,
    long OverdueCents);

/// <summary>What one borrower was lent, returned and still owes (FR-6).</summary>
public sealed record BorrowerTotalsResponse(
    Guid PayerId,
    string Name,
    long LentCents,
    long ReturnedCents,
    long OutstandingCents,
    bool HasOverdue);

/// <summary>
/// One row of the Month screen's Lending list (FR-9): money lent, a repayment received, or the outstanding
/// amount expected back. <see cref="RepaymentId"/> is set for a repayment only.
/// </summary>
public sealed record MonthLendingEntryResponse(
    Guid LendingId,
    Guid? RepaymentId,
    NamedReference Borrower,
    DateOnly Date,
    long AmountCents,
    NamedReference Account,
    bool IsOverdue);

/// <summary>The month's lending (FR-9): lent, returned and expected back, with their totals.</summary>
public sealed record MonthLendingResponse(
    string Month,
    IReadOnlyList<MonthLendingEntryResponse> Lent,
    IReadOnlyList<MonthLendingEntryResponse> Returned,
    IReadOnlyList<MonthLendingEntryResponse> ExpectedBack,
    long LentCents,
    long ReturnedCents,
    long ExpectedBackCents);

/// <summary>One borrower in the monthly report's Lending block.</summary>
public sealed record ReportLendingBorrowerResponse(Guid PayerId, string Name, long LentCents, long ReturnedCents, long ExpectedBackCents);

/// <summary>
/// The month's lending in the monthly report (FR-10): money lent (an output), repayments received and
/// the outstanding amounts expected back (inputs), by borrower.
/// </summary>
public sealed record ReportLendingResponse(
    long LentCents,
    long ReturnedCents,
    long ExpectedBackCents,
    IReadOnlyList<ReportLendingBorrowerResponse> ByBorrower);
