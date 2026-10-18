using Monthwise.Application.CardPurchases;
using Monthwise.Application.CashFlow;
using Monthwise.Application.Common;
using Monthwise.Application.Expenses;
using Monthwise.Application.Loans;
using Monthwise.Application.SharedCosts;
using Monthwise.Domain.Common;
using Monthwise.Domain.Expenses;

namespace Monthwise.Application.Dashboard;

/// <summary>
/// The Dashboard of the current month (design refresh FR-10). It only combines what other screens
/// already calculate: the monthly report for the key figures, the month lists of bills, loan
/// payments, card payments and inputs for what is still to pay and coming up, the cash flow chart
/// for the last six months, and the Loans, Cards, Shared costs and Recurring bills overviews.
/// </summary>
public sealed class GetDashboardHandler(
    TimeProvider timeProvider,
    GetMonthlyReportHandler report,
    GetCashFlowChartHandler chart,
    GetMonthBillsHandler bills,
    GetMonthLoanPaymentsHandler loanPayments,
    GetMonthCardPaymentsHandler cardPayments,
    GetMonthInputsHandler inputs,
    GetLoansDashboardHandler loans,
    GetInstallmentCardsHandler installmentCards,
    GetSharedCostsHandler sharedCosts,
    RecurringBillHandler recurringBills,
    LoanLedger loanLedger,
    CardPaymentLedger cardPaymentLedger,
    ExpenseLedger expenseLedger)
{
    /// <summary>How many days Coming up looks ahead, today included.</summary>
    public const int ComingUpDays = 7;

    /// <summary>At most this many items are shown under Coming up.</summary>
    public const int ComingUpLimit = 5;

    public async Task<DashboardResponse> HandleAsync(CancellationToken cancellationToken)
    {
        var today = timeProvider.Today();
        var month = YearMonth.FromDate(today);
        var next = month.Next();

        var monthReport = await report.HandleAsync(month, cancellationToken);
        var monthBills = await bills.HandleAsync(month, cancellationToken);
        var monthLoanPayments = await loanPayments.HandleAsync(month, cancellationToken);
        var monthCardPayments = await cardPayments.HandleAsync(month, cancellationToken);

        var unpaid = Unpaid(monthBills, monthLoanPayments, monthCardPayments).ToList();
        var stillToPay = new DashboardStillToPayResponse(
            unpaid.Sum(u => u.Item.AmountCents),
            unpaid.Count,
            unpaid.Where(u => u.IsOverdue).Sum(u => u.Item.AmountCents),
            unpaid.Count(u => u.IsOverdue));
        var nextDue = unpaid
            .Select(u => u.Item)
            .Where(i => i.Date >= today)
            .OrderBy(i => i.Date)
            .ThenBy(i => i.Kind)
            .FirstOrDefault();

        var comingUp = await ComingUpAsync(today, month, next, unpaid, cancellationToken);

        return new DashboardResponse(
            month.ToString(),
            today,
            new DashboardTotalsResponse(monthReport.Totals.TotalCents, monthReport.Totals.ReceivedCents, monthReport.PreviousMonthTotalCents),
            new DashboardTotalsResponse(monthReport.Outputs.Totals.TotalCents, monthReport.Outputs.Totals.PaidCents, monthReport.PreviousMonthOutputsCents),
            monthReport.MonthResultCents,
            monthReport.PreviousMonthResultCents,
            stillToPay,
            nextDue,
            comingUp,
            await OutputsByKindAsync(month, cancellationToken),
            await LastSixMonthsAsync(month, cancellationToken),
            await LoansAsync(cancellationToken),
            await CardPaymentsAsync(monthCardPayments, cancellationToken),
            await SharedCostsAsync(month, cancellationToken),
            await RecurringBillsAsync(monthBills, cancellationToken));
    }

    /// <summary>Issued bills, loan payments and card payments of a month that are not paid yet.</summary>
    private static IEnumerable<UnpaidItem> Unpaid(
        MonthBillsResponse monthBills, MonthLoanPaymentsResponse monthLoanPayments, MonthCardPaymentsResponse monthCardPayments) =>
        monthBills.Bills
            .Where(b => b.State == BillState.Expected && b.DueDate is not null)
            .Select(b => new UnpaidItem(new DashboardItemResponse(DashboardItemKind.Bill, b.Name, b.DueDate!.Value, b.AmountCents), b.IsOverdue))
            .Concat(monthLoanPayments.Payments
                .Where(p => !p.IsPaid)
                .Select(p => new UnpaidItem(new DashboardItemResponse(DashboardItemKind.LoanPayment, p.LoanName, p.DueDate, p.AmountCents), p.IsOverdue)))
            .Concat(monthCardPayments.Payments
                .Where(p => !p.IsPaid)
                .Select(p => new UnpaidItem(new DashboardItemResponse(DashboardItemKind.CardPayment, p.CardName, p.PaymentDate, p.AmountCents), p.IsOverdue)));

    /// <summary>
    /// Unpaid outputs and expected inputs due from today through the next six days, this month and
    /// next, earliest first.
    /// </summary>
    private async Task<IReadOnlyList<DashboardItemResponse>> ComingUpAsync(
        DateOnly today, YearMonth month, YearMonth next, IReadOnlyList<UnpaidItem> unpaidThisMonth, CancellationToken cancellationToken)
    {
        var last = today.AddDays(ComingUpDays - 1);
        var items = unpaidThisMonth.Select(u => u.Item).ToList();
        if (YearMonth.FromDate(last) == next)
        {
            items.AddRange(Unpaid(
                await bills.HandleAsync(next, cancellationToken),
                await loanPayments.HandleAsync(next, cancellationToken),
                await cardPayments.HandleAsync(next, cancellationToken)).Select(u => u.Item));
        }
        foreach (var inputMonth in YearMonth.FromDate(last) == next ? new[] { month, next } : [month])
        {
            items.AddRange((await inputs.HandleAsync(inputMonth, cancellationToken)).Inputs
                .Where(i => !i.IsReceived)
                .Select(i => new DashboardItemResponse(
                    DashboardItemKind.Input,
                    string.IsNullOrWhiteSpace(i.Description) ? i.Payer.Name : i.Description,
                    i.ExpectedDate,
                    i.AmountCents)));
        }
        return items
            .Where(i => i.Date >= today && i.Date <= last)
            .OrderBy(i => i.Date)
            .ThenBy(i => i.Kind)
            .ThenBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase)
            .Take(ComingUpLimit)
            .ToList();
    }

    /// <summary>The month's outputs as the monthly report counts them, split into bills, expenses, loan and card payments.</summary>
    private async Task<DashboardOutputsByKindResponse> OutputsByKindAsync(YearMonth month, CancellationToken cancellationToken)
    {
        var expenseBook = await expenseLedger.LoadAsync(cancellationToken);
        var loanBook = await loanLedger.LoadAsync(cancellationToken);
        var cardBook = await cardPaymentLedger.LoadAsync(cancellationToken);
        bool InMonth(DateOnly date) => date >= month.FirstDay && date <= month.LastDay;
        return new DashboardOutputsByKindResponse(
            Money.Sum(expenseBook.BillOutputs().Where(o => InMonth(o.Date)).Select(o => o.Amount)).Cents,
            Money.Sum(expenseBook.ExpenseOutputs().Where(o => InMonth(o.Date)).Select(o => o.Amount)).Cents,
            Money.Sum(loanBook.PaymentsBetween(month.FirstDay, month.LastDay).Select(p => p.Amount)).Cents,
            Money.Sum(cardBook.PaymentsBetween(month.FirstDay, month.LastDay).Select(p => p.Amount)).Cents);
    }

    /// <summary>Inputs and outputs (received or paid and expected) of this month and the five before it.</summary>
    private async Task<IReadOnlyList<DashboardMonthResponse>> LastSixMonthsAsync(YearMonth month, CancellationToken cancellationToken)
    {
        var from = month.AddMonths(-5).FirstDay;
        var result = await chart.HandleAsync(from, month.LastDay, ChartGranularity.Month, new ChartFilter(null, null, null), cancellationToken);
        return result.Value.Buckets
            .Select(b => new DashboardMonthResponse(
                YearMonth.FromDate(b.Start).ToString(),
                b.ReceivedCents + b.ExpectedCents,
                b.OutputPaidCents + b.OutputExpectedCents))
            .ToList();
    }

    private async Task<DashboardLoansResponse?> LoansAsync(CancellationToken cancellationToken)
    {
        var dashboard = await loans.HandleAsync(cancellationToken);
        return dashboard.Loans.Count == 0
            ? null
            : new DashboardLoansResponse(
                dashboard.Loans.Count,
                dashboard.TotalMonthlyPaymentCents,
                dashboard.TotalRemainingCents,
                dashboard.Loans.Any(l => l.HasOverduePayment));
    }

    private async Task<DashboardCardPaymentsResponse?> CardPaymentsAsync(
        MonthCardPaymentsResponse monthCardPayments, CancellationToken cancellationToken)
    {
        var withInstallments = (await installmentCards.HandleAsync(cancellationToken)).Count(c => c.PurchasesWithInstallmentsLeft > 0);
        return monthCardPayments.Payments.Count == 0 && withInstallments == 0
            ? null
            : new DashboardCardPaymentsResponse(monthCardPayments.Totals.TotalCents, monthCardPayments.Payments.Count, withInstallments);
    }

    private async Task<DashboardSharedCostsResponse?> SharedCostsAsync(YearMonth month, CancellationToken cancellationToken)
    {
        var participants = (await sharedCosts.HandleAsync(month, cancellationToken)).Participants;
        return participants.Count == 0
            ? null
            : new DashboardSharedCostsResponse(
                participants.Sum(p => p.OutstandingCents),
                participants.Count(p => p.OutstandingCents > 0),
                participants.Any(p => p.Outstanding.Any(c => c.IsOverdue)));
    }

    private async Task<DashboardRecurringBillsResponse?> RecurringBillsAsync(MonthBillsResponse monthBills, CancellationToken cancellationToken)
    {
        var active = (await recurringBills.ListAsync(false, cancellationToken)).Count(r => r.IsActive);
        return active == 0 ? null : new DashboardRecurringBillsResponse(active, monthBills.NotIssuedCount);
    }

    private sealed record UnpaidItem(DashboardItemResponse Item, bool IsOverdue);
}
