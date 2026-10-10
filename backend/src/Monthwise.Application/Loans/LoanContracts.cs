using Monthwise.Application.CashFlow;
using Monthwise.Application.SharedCosts;
using Monthwise.Domain.Loans;

namespace Monthwise.Application.Loans;

/// <summary>
/// Add or edit a loan. Months use the ISO form <c>yyyy-MM</c>. Either <see cref="PaymentCount"/> or
/// <see cref="EndMonth"/> is needed; the other is calculated (A10, A11). <see cref="AccountId"/> is
/// for a standing order only. <see cref="Share"/> shares the loan (shared bills FR-3); when expected
/// payments have a share changed by hand, <see cref="KeepChangedPaymentShares"/> says whether they keep it.
/// </summary>
public sealed record LoanRequest(
    string Name,
    Guid CategoryId,
    Guid SubcategoryId,
    Guid LenderId,
    PaymentMethod PaymentMethod,
    Guid? AccountId,
    string StartMonth,
    int PaymentDay,
    long MonthlyPaymentCents,
    int? PaymentCount,
    string? EndMonth,
    long? ResidualValueCents,
    string? Description,
    ShareRequest? Share = null,
    bool KeepChangedPaymentShares = true);

public enum LoanListFilter
{
    /// <summary>Loans that are not closed, paid off ones included.</summary>
    Active,
    Closed,
}

/// <summary>A loan as the list, details and Dashboard show it (FR-1, FR-9, FR-13).</summary>
public sealed record LoanResponse(
    Guid Id,
    string Name,
    NamedReference Category,
    NamedReference Subcategory,
    NamedReference Lender,
    PaymentMethod PaymentMethod,
    NamedReference? Account,
    string StartMonth,
    string EndMonth,
    int PaymentDay,
    long MonthlyPaymentCents,
    int PaymentCount,
    long? ResidualValueCents,
    string? Description,
    DateOnly? ClosedOn,
    LoanStatus Status,
    long CurrentMonthlyPaymentCents,
    int CurrentPaymentDay,
    DateOnly? NextPaymentDate,
    int PaymentsPaid,
    int PaymentsLeft,
    long PaidAmountCents,
    long RemainingAmountCents,
    bool HasOverduePayment,
    bool NeedsAccount,
    ShareResponse? Share = null);

/// <summary>One payment change; <see cref="CanChange"/> is false once a payment it applies to is paid (FR-7).</summary>
public sealed record PaymentChangeResponse(
    Guid Id,
    string FromMonth,
    long? MonthlyPaymentCents,
    int? PaymentDay,
    string? Note,
    bool CanChange);

/// <summary>A loan with its payment change history and all its payments (FR-7, FR-9).</summary>
public sealed record LoanDetailsResponse(
    LoanResponse Loan,
    IReadOnlyList<PaymentChangeResponse> Changes,
    IReadOnlyList<LoanPaymentResponse> Payments);

/// <summary>
/// Records a payment change. When it covers payments the user changed by hand,
/// <see cref="KeepChangedPayments"/> says whether they keep their values (FR-7).
/// </summary>
public sealed record PaymentChangeRequest(
    string FromMonth,
    long? MonthlyPaymentCents,
    int? PaymentDay,
    string? Note,
    bool KeepChangedPayments);

/// <summary>
/// One payment of a loan. It is identified by <see cref="LoanId"/>, its scheduled <see cref="Month"/>
/// and <see cref="Kind"/>. <see cref="Number"/> is 0 for the residual value. A shared payment has its
/// <see cref="Share"/>, the household's <see cref="OwnPartCents"/> and its <see cref="Contribution"/>
/// (shared bills FR-5, FR-7); <see cref="IsShareChanged"/> marks a share set on this payment by hand.
/// </summary>
public sealed record LoanPaymentResponse(
    Guid LoanId,
    string Month,
    LoanPaymentKind Kind,
    string LoanName,
    NamedReference Category,
    NamedReference Subcategory,
    int Number,
    int Total,
    long AmountCents,
    DateOnly BillDate,
    DateOnly DueDate,
    DateOnly? PaidDate,
    NamedReference? Account,
    PaymentMethod PaymentMethod,
    bool IsPaid,
    bool IsChanged,
    bool IsOverdue,
    bool NeedsAccount,
    ShareResponse? Share = null,
    long? OwnPartCents = null,
    bool IsShareChanged = false,
    ContributionResponse? Contribution = null);

public sealed record LoanPaymentTotalsResponse(long ExpectedCents, long PaidCents, long TotalCents);

/// <summary>
/// The loan payments of a month (<c>yyyy-MM</c>) and their totals (FR-4). <see cref="OwnPartCents"/> is
/// what the household pays itself: the total minus the participants' parts (shared bills FR-7).
/// </summary>
public sealed record MonthLoanPaymentsResponse(
    string Month,
    IReadOnlyList<LoanPaymentResponse> Payments,
    LoanPaymentTotalsResponse Totals,
    long OwnPartCents);

/// <summary>Shares one payment, changes its share or stops sharing it (<see cref="Share"/> null), whatever the loan's share is (shared bills FR-3).</summary>
public sealed record LoanPaymentShareRequest(ShareRequest? Share);

/// <summary>Changes one expected payment (FR-4).</summary>
public sealed record LoanPaymentChangeRequest(long AmountCents, DateOnly DueDate);

/// <summary>
/// Ticks Paid (FR-5). Without <see cref="PaidDate"/> it is today; without <see cref="AccountId"/> it is
/// the loan's account for a standing order. For a bill the account is required.
/// </summary>
public sealed record LoanPaymentPaidRequest(DateOnly? PaidDate, Guid? AccountId);

/// <summary>The Dashboard Loans widget (FR-13).</summary>
public sealed record LoansDashboardResponse(
    bool HasAnyLoans,
    IReadOnlyList<LoanResponse> Loans,
    long TotalMonthlyPaymentCents,
    long TotalRemainingCents);

public sealed record LoanCategoryRequest(string Name, bool AllowsResidualValue);

public sealed record LoanSubcategoryResponse(Guid Id, Guid CategoryId, string Name, bool IsPredefined, bool IsHidden);

public sealed record LoanCategoryResponse(
    Guid Id,
    string Name,
    bool AllowsResidualValue,
    bool IsPredefined,
    bool IsHidden,
    IReadOnlyList<LoanSubcategoryResponse> Subcategories);
