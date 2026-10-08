using Monthwise.Application.Loans;
using Monthwise.Domain.CashFlow;

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
    RecurrenceFrequency? Frequency);

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
/// paid and expected outputs (loan payments).
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

/// <summary>A group of outputs in the report; <see cref="Id"/> is null for bills without an account yet.</summary>
public sealed record ReportOutputGroupResponse(Guid? Id, string Name, LoanPaymentTotalsResponse Totals);

/// <summary>The month's outputs (loan payments) grouped by loan category, account and loan (Appendix 1).</summary>
public sealed record ReportOutputsResponse(
    LoanPaymentTotalsResponse Totals,
    IReadOnlyList<ReportOutputGroupResponse> ByCategory,
    IReadOnlyList<ReportOutputGroupResponse> ByAccount,
    IReadOnlyList<ReportOutputGroupResponse> ByLoan);

/// <summary>
/// The monthly cash flow report (FR-13, Appendix 1). <see cref="MonthResultCents"/> is all inputs
/// minus all outputs; the previous-month figures allow the comparison.
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
    long PreviousMonthResultCents);
