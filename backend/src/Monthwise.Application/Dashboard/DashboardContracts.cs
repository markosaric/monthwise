namespace Monthwise.Application.Dashboard;

/// <summary>What a Dashboard item is (design refresh FR-10).</summary>
public enum DashboardItemKind
{
    Bill,
    LoanPayment,
    CardPayment,
    Input,
}

/// <summary>
/// Inputs or outputs of a month: the total, the part already received or paid, and the previous
/// month's total for the change pill (design refresh FR-10, K1 and K2).
/// </summary>
public sealed record DashboardTotalsResponse(long TotalCents, long DoneCents, long PreviousMonthTotalCents);

/// <summary>Expected outputs of the month not paid yet (K4), and how many of them are overdue.</summary>
public sealed record DashboardStillToPayResponse(long TotalCents, int Count, long OverdueCents, int OverdueCount);

/// <summary>A bill, loan payment, card payment or expected input with the date it is due.</summary>
public sealed record DashboardItemResponse(DashboardItemKind Kind, string Name, DateOnly Date, long AmountCents);

/// <summary>The month's outputs split by kind; together they are the month's outputs total.</summary>
public sealed record DashboardOutputsByKindResponse(long BillsCents, long ExpensesCents, long LoanPaymentsCents, long CardPaymentsCents);

/// <summary>Inputs and outputs of one month for the 6-month chart.</summary>
public sealed record DashboardMonthResponse(string Month, long InputsCents, long OutputsCents);

/// <summary>The Loans small card; null on the Dashboard when there are no active loans.</summary>
public sealed record DashboardLoansResponse(int ActiveCount, long MonthlyPaymentCents, long RemainingCents, bool HasOverdue);

/// <summary>The Card payments small card: this month's card payments and the cards with installments left.</summary>
public sealed record DashboardCardPaymentsResponse(long MonthTotalCents, int Count, int CardsWithInstallments);

/// <summary>The Shared costs small card: outstanding contributions and how many participants owe something.</summary>
public sealed record DashboardSharedCostsResponse(long OutstandingCents, int ParticipantsOwing, bool HasOverdue);

/// <summary>The Recurring bills small card: active recurring bills and this month's bills not issued yet.</summary>
public sealed record DashboardRecurringBillsResponse(int ActiveCount, int NotIssuedThisMonth);

/// <summary>
/// The Dashboard of the current month (design refresh FR-10). Every figure comes from an existing
/// calculation: the monthly report, the month lists, the cash flow chart and the section overviews.
/// Small-card blocks are null when their section has no data.
/// </summary>
public sealed record DashboardResponse(
    string Month,
    DateOnly Today,
    DashboardTotalsResponse Inputs,
    DashboardTotalsResponse Outputs,
    long MonthResultCents,
    long PreviousMonthResultCents,
    DashboardStillToPayResponse StillToPay,
    DashboardItemResponse? NextDue,
    IReadOnlyList<DashboardItemResponse> ComingUp,
    DashboardOutputsByKindResponse OutputsByKind,
    IReadOnlyList<DashboardMonthResponse> LastSixMonths,
    DashboardLoansResponse? Loans,
    DashboardCardPaymentsResponse? CardPayments,
    DashboardSharedCostsResponse? SharedCosts,
    DashboardRecurringBillsResponse? RecurringBills);
