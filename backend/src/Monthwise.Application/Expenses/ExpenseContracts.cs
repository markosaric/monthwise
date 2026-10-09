using Monthwise.Application.CashFlow;
using Monthwise.Application.Loans;
using Monthwise.Domain.Expenses;

namespace Monthwise.Application.Expenses;

/// <summary>An expense category of the shared list (FR-1, installments Appendix 1).</summary>
public sealed record ExpenseCategoryResponse(Guid Id, string Name, bool IsPredefined, bool IsFixed, bool IsHidden);

/// <summary>
/// What a bill or expense is paid with: an account, or a credit card (Credit or Charge), whose card
/// payment then includes it (FR-5, FR-6).
/// </summary>
public sealed record PaidFromResponse(Guid Id, string Name, bool IsCreditCard);

/// <summary>Add or edit a recurring bill (FR-2).</summary>
public sealed record RecurringBillRequest(string Name, Guid CategoryId, string? Payee, Guid? PaidFromId, string? Description);

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
    bool CanBeDeleted);

/// <summary>
/// Add a one-off bill or edit a bill (FR-4). Amount 0 without dates keeps a bill from a recurring bill
/// Not issued. <see cref="PaidDate"/> set means paid on that day; null means not paid.
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
    DateOnly? PaidDate);

/// <summary>
/// A bill (FR-4). <see cref="PaidFrom"/> is what it was paid with, or what it will usually be paid with
/// while not paid. <see cref="DueDateHint"/> prefills the due date of a Not issued bill from the last
/// issued bill of its recurring bill (FR-2). <see cref="PaidWithCard"/> is true for a paid bill whose
/// amount is in a card payment (FR-6); <see cref="ChangesChargedCardPayment"/> then says that the card
/// payment is already due, so the UI warns before an edit, untick or delete.
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
    bool ChangesChargedCardPayment);

/// <summary>
/// The bills of a month (FR-9). The totals leave out Not issued bills and bills paid with a credit card,
/// which are in their card payment (<see cref="WithCardCents"/>).
/// </summary>
public sealed record MonthBillsResponse(
    string Month,
    IReadOnlyList<BillResponse> Bills,
    LoanPaymentTotalsResponse Totals,
    long WithCardCents,
    int NotIssuedCount);

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
