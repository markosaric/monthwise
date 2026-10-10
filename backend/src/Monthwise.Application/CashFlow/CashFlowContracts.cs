using Monthwise.Application.Loans;
using Monthwise.Application.SharedCosts;
using Monthwise.Domain.CashFlow;
using Monthwise.Domain.Loans;
using Monthwise.Domain.SharedCosts;

namespace Monthwise.Application.CashFlow;

/// <summary>
/// Add or edit an input. The payer is a name: an existing payer with that name (ignoring case) is
/// used, otherwise a new one is saved. With <see cref="Recurrence"/>, adding creates a recurring
/// input whose first occurrence has these dates; it is ignored when editing.
/// </summary>
public sealed record InputRequest(
    string PayerName,
    Guid AccountId,
    Guid CategoryId,
    long AmountCents,
    DateOnly IssueDate,
    DateOnly ExpectedDate,
    DateOnly? ReceivedDate,
    string? Description,
    RecurrenceRequest? Recurrence);

public sealed record RecurrenceRequest(RecurrenceFrequency Frequency, DateOnly? EndDate);

/// <summary>Changes a recurring input. The dates are those of its first occurrence (the schedule anchors).</summary>
public sealed record RecurringInputRequest(
    string PayerName,
    Guid AccountId,
    Guid CategoryId,
    long AmountCents,
    DateOnly IssueDate,
    DateOnly ExpectedDate,
    string? Description,
    RecurrenceFrequency Frequency,
    DateOnly? EndDate);

public sealed record NamedReference(Guid Id, string Name);

/// <summary>
/// One input in a list: a stored input (<see cref="Id"/> set) or a calculated occurrence of a
/// recurring input (<see cref="Id"/> null, <see cref="RecurringInputId"/> and <see cref="OccurrenceDate"/> set).
/// A contribution of a shared bill or loan payment has <see cref="Contribution"/> set; it is calculated
/// from its cost while <see cref="Id"/> is null (shared bills FR-5).
/// </summary>
public sealed record InputEntryResponse(
    Guid? Id,
    Guid? RecurringInputId,
    DateOnly? OccurrenceDate,
    NamedReference Payer,
    NamedReference Category,
    NamedReference Account,
    long AmountCents,
    DateOnly IssueDate,
    DateOnly ExpectedDate,
    DateOnly? ReceivedDate,
    string? Description,
    bool IsReceived,
    bool IsRecurring,
    RecurrenceFrequency? Frequency,
    InputContributionResponse? Contribution = null);

/// <summary>
/// The shared bill or loan payment of a contribution in the input list (shared bills FR-7):
/// <see cref="PartCents"/> is the participant's current part; <see cref="NeedsAccount"/> is true while
/// no account applies yet.
/// </summary>
public sealed record InputContributionResponse(
    ContributionSourceKind Kind,
    Guid? BillId,
    Guid? LoanId,
    string? Month,
    LoanPaymentKind? PaymentKind,
    string CostName,
    long PartCents,
    bool IsEdited,
    bool NeedsAccount);

public sealed record InputTotalsResponse(long ExpectedCents, long ReceivedCents, long TotalCents);

/// <summary>The inputs of a month (<c>yyyy-MM</c>) and their totals (FR-1).</summary>
public sealed record MonthInputsResponse(string Month, IReadOnlyList<InputEntryResponse> Inputs, InputTotalsResponse Totals);

public sealed record RecurringInputResponse(
    Guid Id,
    NamedReference Payer,
    NamedReference Category,
    NamedReference Account,
    long AmountCents,
    string? Description,
    RecurrenceFrequency Frequency,
    DateOnly IssueDate,
    DateOnly ExpectedDate,
    DateOnly? EndDate,
    bool IsStopped);

public sealed record NameRequest(string Name);

public sealed record PayerResponse(Guid Id, string Name, bool IsRemoved);

public sealed record InputCategoryResponse(Guid Id, string Name, bool IsPredefined, bool IsHidden);

public sealed record BalanceEntryRequest(BalanceEntryKind Kind, DateOnly Date, long BalanceCents, string? Note);

/// <summary>A starting balance or correction; <see cref="DifferenceCents"/> is how much it changed the balance.</summary>
public sealed record BalanceEntryResponse(
    Guid Id,
    Guid AccountId,
    BalanceEntryKind Kind,
    DateOnly Date,
    long BalanceCents,
    long DifferenceCents,
    string? Note);

/// <summary>Current balance (today) and expected balance at the end of the current month (FR-10).</summary>
public sealed record AccountBalanceResponse(
    Guid AccountId,
    long CurrentBalanceCents,
    long ExpectedBalanceCents,
    bool HasStartingBalance);

public sealed record AccountBalanceDetailsResponse(AccountBalanceResponse Balance, IReadOnlyList<BalanceEntryResponse> Entries);

public enum ChartGranularity
{
    Day,
    Month,
}

/// <summary>
/// Sums of one day or month of the chart (FR-12, Appendix 1): received and expected inputs, and
/// paid and expected outputs (loan and card payments).
/// </summary>
public sealed record ChartBucketResponse(
    DateOnly Start,
    long ReceivedCents,
    long ExpectedCents,
    long OutputPaidCents,
    long OutputExpectedCents);

public sealed record CashFlowChartResponse(
    DateOnly From,
    DateOnly To,
    ChartGranularity Granularity,
    IReadOnlyList<ChartBucketResponse> Buckets);

public sealed record ReportGroupResponse(Guid Id, string Name, InputTotalsResponse Totals);

public sealed record ReportBalanceEntryResponse(
    NamedReference Account,
    BalanceEntryKind Kind,
    DateOnly Date,
    long BalanceCents,
    long DifferenceCents,
    string? Note);

/// <summary>
/// A group of outputs in the report; <see cref="Id"/> is null for bills without an account yet, and for
/// the Card payments category.
/// </summary>
public sealed record ReportOutputGroupResponse(Guid? Id, string Name, LoanPaymentTotalsResponse Totals);

/// <summary>
/// The month's outputs (loan and card payments, bills and expenses) grouped by category, account, loan
/// and card (Appendix 1, installments FR-8, bills and expenses FR-10). Card payments are one group named
/// "Card payments" in <see cref="ByCategory"/>, with a null id; bills and expenses are grouped by their
/// expense category.
/// </summary>
public sealed record ReportOutputsResponse(
    LoanPaymentTotalsResponse Totals,
    IReadOnlyList<ReportOutputGroupResponse> ByCategory,
    IReadOnlyList<ReportOutputGroupResponse> ByAccount,
    IReadOnlyList<ReportOutputGroupResponse> ByLoan,
    IReadOnlyList<ReportOutputGroupResponse> ByCard);

/// <summary>
/// The monthly cash flow report (FR-13, Appendix 1). <see cref="MonthResultCents"/> is all inputs
/// minus all outputs; the previous-month figures allow the comparison. <see cref="SharedCosts"/> sums
/// the month's shared bills and loan payments and their contributions (shared bills FR-9).
/// </summary>
public sealed record MonthlyReportResponse(
    string Month,
    InputTotalsResponse Totals,
    IReadOnlyList<ReportGroupResponse> ByCategory,
    IReadOnlyList<ReportGroupResponse> ByAccount,
    IReadOnlyList<ReportGroupResponse> ByPayer,
    IReadOnlyList<ReportBalanceEntryResponse> BalanceEntries,
    long PreviousMonthTotalCents,
    long DifferenceCents,
    ReportOutputsResponse Outputs,
    long MonthResultCents,
    long PreviousMonthOutputsCents,
    long PreviousMonthResultCents,
    ReportSharedCostsResponse? SharedCosts = null);
