using Monthwise.Application.CashFlow;
using Monthwise.Application.Loans;
using Monthwise.Application.SharedCosts;
using Monthwise.Domain.Expenses;

namespace Monthwise.Application.Expenses;

/// <summary>An expense category of the shared list (FR-1, installments Appendix 1).</summary>
public sealed record ExpenseCategoryResponse(Guid Id, string Name, bool IsPredefined, bool IsFixed, bool IsHidden);

/// <summary>
/// What a bill or expense is paid with: an account, or a credit card (Credit or Charge), whose card
/// payment then includes it (FR-5, FR-6).
/// </summary>
public sealed record PaidFromResponse(Guid Id, string Name, bool IsCreditCard);

/// <summary>
/// Add or edit a recurring bill (FR-2); <see cref="Share"/> shares its bills (shared bills FR-3). The
/// amount, issue day and due day are optional (Appendix 2, R6 to R8).
/// </summary>
public sealed record RecurringBillRequest(
    string Name,
    Guid CategoryId,
    string? Payee,
    Guid? PaidFromId,
    string? Description,
    ShareRequest? Share = null,
    long? AmountCents = null,
    int? IssueDay = null,
    int? DueDay = null);

/// <summary>A recurring bill (FR-2). <see cref="CanBeDeleted"/> is true while no bill was added from it.</summary>
public sealed record RecurringBillResponse(
    Guid Id,
    string Name,
    NamedReference Category,
    string? Payee,
    PaidFromResponse? PaidFrom,
    string? Description,
    bool IsActive,
    DateOnly? StoppedOn,
    bool CanBeDeleted,
    ShareResponse? Share = null,
    long? AmountCents = null,
    int? IssueDay = null,
    int? DueDay = null);

/// <summary>
/// Ticks Recurring on a one-off bill (Appendix 2, A2.3): the optional amount, issue day and due day of
/// the recurring bill made from it. Its other details come from the bill.
/// </summary>
public sealed record RecurringOptionsRequest(long? AmountCents, int? IssueDay, int? DueDay);

/// <summary>
/// Add a one-off bill or edit a bill (FR-4). A bill from a recurring bill stays Not issued while its
/// amount (0), issue date or due date is missing (Appendix 2). <see cref="PaidDate"/> set means paid on
/// that day; null means not paid. <see cref="Share"/> shares the bill, or stops sharing it when null
/// (shared bills FR-3). <see cref="Recurring"/> on a one-off bill also adds a recurring bill from it
/// (Appendix 2, A2.3); it is ignored on a bill that already has one.
/// </summary>
public sealed record BillRequest(
    string Name,
    Guid CategoryId,
    string? Payee,
    long AmountCents,
    DateOnly? IssueDate,
    DateOnly? DueDate,
    Guid? PaidFromId,
    string? Description,
    DateOnly? PaidDate,
    ShareRequest? Share = null,
    RecurringOptionsRequest? Recurring = null);

/// <summary>
/// A bill (FR-4). <see cref="PaidFrom"/> is what it was paid with, or what it will usually be paid with
/// while not paid. <see cref="DueDateHint"/> prefills the due date of a Not issued bill from the last
/// issued bill of its recurring bill (FR-2). <see cref="PaidWithCard"/> is true for a paid bill whose
/// amount is in a card payment (FR-6); <see cref="ChangesChargedCardPayment"/> then says that the card
/// payment is already due, so the UI warns before an edit, untick or delete. A shared bill has its
/// <see cref="Share"/>, the household's <see cref="OwnPartCents"/> once issued and its
/// <see cref="Contribution"/> (shared bills FR-5, FR-7).
/// </summary>
public sealed record BillResponse(
    Guid Id,
    Guid? RecurringBillId,
    string Month,
    string Name,
    NamedReference Category,
    string? Payee,
    long AmountCents,
    DateOnly? IssueDate,
    DateOnly? DueDate,
    BillState State,
    DateOnly? PaidDate,
    PaidFromResponse? PaidFrom,
    string? Description,
    bool IsOverdue,
    DateOnly? DueDateHint,
    bool PaidWithCard,
    bool ChangesChargedCardPayment,
    ShareResponse? Share = null,
    long? OwnPartCents = null,
    bool IsShareChanged = false,
    ContributionResponse? Contribution = null);

/// <summary>
/// The bills of a month (FR-9). The totals leave out Not issued bills and bills paid with a credit card,
/// which are in their card payment (<see cref="WithCardCents"/>). <see cref="OwnPartCents"/> is what the
/// household pays itself of the counted bills: their total minus the participants' parts (shared bills FR-7).
/// </summary>
public sealed record MonthBillsResponse(
    string Month,
    IReadOnlyList<BillResponse> Bills,
    LoanPaymentTotalsResponse Totals,
    long WithCardCents,
    int NotIssuedCount,
    long OwnPartCents);

/// <summary>Ticks Paid (FR-5). Without <see cref="PaidDate"/> it is today; without <see cref="PaidFromId"/> the bill's own.</summary>
public sealed record BillPaidRequest(DateOnly? PaidDate, Guid? PaidFromId);

/// <summary>The result of Add recurring bills (FR-3): how many bills were added to the month.</summary>
public sealed record AddRecurringBillsResponse(string Month, int Added);

/// <summary>Record or edit an expense (FR-7). For a cash withdrawal the category is kept.</summary>
public sealed record ExpenseRequest(DateOnly Date, long AmountCents, Guid CategoryId, Guid PaidFromId, string? Description);

/// <summary>Record or edit a cash withdrawal (FR-8), always from a primary account.</summary>
public sealed record CashWithdrawalRequest(DateOnly Date, long AmountCents, Guid AccountId, string? Description);

/// <summary>An expense or a cash withdrawal (FR-7, FR-8).</summary>
public sealed record ExpenseResponse(
    Guid Id,
    DateOnly Date,
    long AmountCents,
    NamedReference Category,
    PaidFromResponse PaidFrom,
    string? Description,
    bool IsCashWithdrawal,
    bool PaidWithCard,
    bool ChangesChargedCardPayment);

/// <summary>
/// Totals of a month's expenses (FR-9): all of them, those paid from accounts, those paid with credit
/// cards (in their card payments) and cash withdrawals.
/// </summary>
public sealed record ExpenseTotalsResponse(long TotalCents, long FromAccountsCents, long WithCardCents, long CashWithdrawalCents);

public sealed record MonthExpensesResponse(string Month, IReadOnlyList<ExpenseResponse> Expenses, ExpenseTotalsResponse Totals);

/// <summary>What the last expense was paid with, to prefill a new one (E4); null when there is none.</summary>
public sealed record LastPaidFromResponse(PaidFromResponse? PaidFrom);
