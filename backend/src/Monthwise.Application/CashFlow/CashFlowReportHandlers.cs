using Monthwise.Application.CardPurchases;
using Monthwise.Application.Cards;
using Monthwise.Application.Expenses;
using Monthwise.Application.Loans;
using Monthwise.Domain.CashFlow;
using Monthwise.Domain.Common;

namespace Monthwise.Application.CashFlow;

/// <summary>
/// Filters of the cash flow chart (FR-12, Appendix 1). The account applies to inputs and outputs;
/// payer and category to inputs only. The loan category shows only the payments of those loans, the
/// card only that card's card payments (installments FR-8), and the expense category only the bills
/// and expenses of that category (bills and expenses FR-10).
/// </summary>
public sealed record ChartFilter(
    Guid? AccountId, Guid? PayerId, Guid? CategoryId, Guid? LoanCategoryId = null, Guid? CardId = null, Guid? ExpenseCategoryId = null);

/// <summary>
/// Received and expected inputs, and paid and expected outputs (loan and card payments, bills and
/// expenses), per day or month (FR-12, Appendix 1; installments FR-8; bills and expenses FR-10).
/// </summary>
public sealed class GetCashFlowChartHandler(
    InputLedger ledger, LoanLedger loanLedger, CardPaymentLedger cardPaymentLedger, ExpenseLedger expenseLedger)
{
    /// <summary>Longest period the chart accepts.</summary>
    public const int MaxYears = 10;

    public async Task<Result<CashFlowChartResponse>> HandleAsync(
        DateOnly from, DateOnly to, ChartGranularity granularity, ChartFilter filter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);
        if (to < from)
        {
            return Result.Failure<CashFlowChartResponse>(new DomainError("chart.to.beforeFrom", "The end of the period is before its start.", "to"));
        }
        if (to > from.AddYears(MaxYears))
        {
            return Result.Failure<CashFlowChartResponse>(new DomainError("chart.period.tooLong", $"Choose a period of at most {MaxYears} years.", "to"));
        }

        var entries = (await ledger.EntriesAsync(from, to, cancellationToken))
            .Where(e => filter.AccountId is null || e.Details.AccountId == filter.AccountId)
            .Where(e => filter.PayerId is null || e.Details.PayerId == filter.PayerId)
            .Where(e => filter.CategoryId is null || e.Details.CategoryId == filter.CategoryId)
            .ToLookup(e => BucketStart(e.MonthDate, granularity));
        var book = await loanLedger.LoadAsync(cancellationToken);
        IEnumerable<ChartOutput> loanPayments = filter.CardId is not null || filter.ExpenseCategoryId is not null
            ? []
            : book.PaymentsBetween(from, to)
                .Where(p => filter.AccountId is null || p.AccountId == filter.AccountId)
                .Where(p => filter.LoanCategoryId is null || book.CategoryOf(p.LoanId) == filter.LoanCategoryId)
                .Select(p => new ChartOutput(p.MonthDate, p.IsPaid, p.Amount));
        IEnumerable<ChartOutput> cardPayments = filter.LoanCategoryId is not null || filter.ExpenseCategoryId is not null
            ? []
            : (await cardPaymentLedger.LoadAsync(cancellationToken)).PaymentsBetween(from, to)
                .Where(p => filter.AccountId is null || p.AccountId == filter.AccountId)
                .Where(p => filter.CardId is null || p.CardId == filter.CardId)
                .Select(p => new ChartOutput(p.MonthDate, p.IsPaid, p.Amount));
        IEnumerable<ChartOutput> billsAndExpenses = filter.LoanCategoryId is not null || filter.CardId is not null
            ? []
            : (await expenseLedger.LoadAsync(cancellationToken)).OutputsBetween(from, to)
                .Where(o => filter.AccountId is null || o.AccountId == filter.AccountId)
                .Where(o => filter.ExpenseCategoryId is null || o.CategoryId == filter.ExpenseCategoryId)
                .Select(o => new ChartOutput(o.Date, o.IsPaid, o.Amount));
        var payments = loanPayments.Concat(cardPayments).Concat(billsAndExpenses).ToLookup(p => BucketStart(p.Date, granularity));

        var buckets = new List<ChartBucketResponse>();
        for (var start = BucketStart(from, granularity); start <= to; start = granularity == ChartGranularity.Day ? start.AddDays(1) : start.AddMonths(1))
        {
            var bucket = entries[start].ToList();
            var outputs = payments[start].ToList();
            buckets.Add(new ChartBucketResponse(
                start,
                Money.Sum(bucket.Where(e => e.IsReceived).Select(e => e.Details.Amount)).Cents,
                Money.Sum(bucket.Where(e => !e.IsReceived).Select(e => e.Details.Amount)).Cents,
                Money.Sum(outputs.Where(p => p.IsPaid).Select(p => p.Amount)).Cents,
                Money.Sum(outputs.Where(p => !p.IsPaid).Select(p => p.Amount)).Cents));
        }
        return Result.Success(new CashFlowChartResponse(from, to, granularity, buckets));
    }

    private static DateOnly BucketStart(DateOnly date, ChartGranularity granularity) =>
        granularity == ChartGranularity.Day ? date : new DateOnly(date.Year, date.Month, 1);

    private sealed record ChartOutput(DateOnly Date, bool IsPaid, Money Amount);
}

/// <summary>
/// The monthly cash flow report (FR-13, Appendix 1), with loan and card payments, bills and expenses as
/// outputs (installments FR-8; bills and expenses FR-10).
/// </summary>
public sealed class GetMonthlyReportHandler(
    InputLedger ledger,
    LoanLedger loanLedger,
    CardPaymentLedger cardPaymentLedger,
    ExpenseLedger expenseLedger,
    IBalanceEntryRepository balanceEntries,
    AccountBalanceQuery balances,
    IPayerRepository payers,
    IInputCategoryRepository categories,
    ICardRepository cards)
{
    public async Task<MonthlyReportResponse> HandleAsync(YearMonth month, CancellationToken cancellationToken)
    {
        var entries = await ledger.EntriesAsync(month.FirstDay, month.LastDay, cancellationToken);
        var previous = month.Previous();
        var previousEntries = await ledger.EntriesAsync(previous.FirstDay, previous.LastDay, cancellationToken);
        var names = await CashFlowNames.LoadAsync(payers, categories, cards, cancellationToken);

        var totals = InputLedger.Totals(entries);
        var previousTotal = InputLedger.Totals(previousEntries).TotalCents;

        var calculators = await balances.CalculatorsAsync(DateOnly.MaxValue, cancellationToken);
        var monthEntries = (await balanceEntries.ListAsync(null, cancellationToken))
            .Where(e => YearMonth.FromDate(e.Date) == month)
            .OrderBy(e => e.Date)
            .ThenBy(e => e.CreatedAt)
            .Select(e => new ReportBalanceEntryResponse(
                names.Account(e.AccountId), e.Kind, e.Date, e.Balance.Cents,
                calculators[e.AccountId].DifferenceOf(e.Id).Cents, e.Note))
            .ToList();

        var book = await loanLedger.LoadAsync(cancellationToken);
        var cardBook = await cardPaymentLedger.LoadAsync(cancellationToken);
        var expenseBook = await expenseLedger.LoadAsync(cancellationToken);
        var outputs = Outputs(book, cardBook, expenseBook, month);
        var outputTotals = outputs.Totals;
        var previousOutputs = Outputs(book, cardBook, expenseBook, previous).Totals.TotalCents;

        return new MonthlyReportResponse(
            month.ToString(),
            totals,
            Group(entries, e => e.Details.CategoryId, names.Category),
            Group(entries, e => e.Details.AccountId, names.Account),
            Group(entries, e => e.Details.PayerId, names.Payer),
            monthEntries,
            previousTotal,
            totals.TotalCents - previousTotal,
            outputs,
            totals.TotalCents - outputTotals.TotalCents,
            previousOutputs,
            previousTotal - previousOutputs);
    }

    /// <summary>The group name of bills paid later, which have no account yet.</summary>
    public const string NoAccountYet = "No account yet";

    /// <summary>The name of the output category that groups all card payments.</summary>
    public const string CardPayments = "Card payments";

    /// <summary>
    /// The month's outputs: loan payments, card payments, bills and expenses, grouped by category (loan
    /// category, one Card payments group, or expense category), account, loan and card.
    /// </summary>
    private static ReportOutputsResponse Outputs(LoanBook book, CardPaymentBook cardBook, ExpenseBook expenseBook, YearMonth month)
    {
        var loanPayments = book.PaymentsBetween(month.FirstDay, month.LastDay)
            .Select(p => new ReportOutput(p.IsPaid, p.Amount, p.AccountId, book.CategoryOf(p.LoanId), p.LoanId, null))
            .ToList();
        var cardPayments = cardBook.PaymentsBetween(month.FirstDay, month.LastDay)
            .Select(p => new ReportOutput(p.IsPaid, p.Amount, p.NeedsAccount ? null : p.AccountId, null, null, p.CardId))
            .ToList();
        var billsAndExpenses = expenseBook.OutputsBetween(month.FirstDay, month.LastDay)
            .Select(o => new ReportOutput(o.IsPaid, o.Amount, o.AccountId, null, null, null, o.CategoryId))
            .ToList();
        var all = loanPayments.Concat(cardPayments).Concat(billsAndExpenses).ToList();
        var byCategory = GroupOutputs(loanPayments, o => o.LoanCategoryId, id => book.Category(id!.Value).Name);
        byCategory.AddRange(GroupOutputs(billsAndExpenses, o => o.ExpenseCategoryId, id => expenseBook.Category(id!.Value).Name));
        if (cardPayments.Count > 0)
        {
            byCategory = [.. byCategory, new ReportOutputGroupResponse(null, CardPayments, Totals(cardPayments))];
        }
        byCategory = Ordered(byCategory);
        return new ReportOutputsResponse(
            Totals(all),
            byCategory,
            GroupOutputs(all, o => o.AccountId, id => id is { } accountId ? AccountName(book, cardBook, accountId) : NoAccountYet),
            GroupOutputs(loanPayments, o => o.LoanId, id => book.LoanReference(id!.Value).Name),
            GroupOutputs(cardPayments, o => o.CardId, id => cardBook.CardReference(id!.Value).Name));
    }

    private static string AccountName(LoanBook book, CardPaymentBook cardBook, Guid accountId) =>
        cardBook.Card(accountId)?.Name ?? book.Account(accountId).Name;

    /// <summary>Output groups with their totals, largest total first.</summary>
    private static List<ReportOutputGroupResponse> GroupOutputs(
        IEnumerable<ReportOutput> outputs, Func<ReportOutput, Guid?> key, Func<Guid?, string> name) =>
        Ordered(outputs
            .GroupBy(key)
            .Select(g => new ReportOutputGroupResponse(g.Key, name(g.Key), Totals(g)))
            .ToList());

    private static List<ReportOutputGroupResponse> Ordered(IEnumerable<ReportOutputGroupResponse> groups) =>
        groups
            .OrderByDescending(g => g.Totals.TotalCents)
            .ThenBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    private static LoanPaymentTotalsResponse Totals(IEnumerable<ReportOutput> outputs)
    {
        var list = outputs.ToList();
        var expected = Money.Sum(list.Where(o => !o.IsPaid).Select(o => o.Amount));
        var paid = Money.Sum(list.Where(o => o.IsPaid).Select(o => o.Amount));
        return new LoanPaymentTotalsResponse(expected.Cents, paid.Cents, (expected + paid).Cents);
    }

    /// <summary>One output of the report: a loan payment, a card payment, a bill or an expense.</summary>
    private sealed record ReportOutput(
        bool IsPaid, Money Amount, Guid? AccountId, Guid? LoanCategoryId, Guid? LoanId, Guid? CardId, Guid? ExpenseCategoryId = null);

    /// <summary>Groups with their totals, largest total first.</summary>
    private static List<ReportGroupResponse> Group(
        IEnumerable<InputEntry> entries, Func<InputEntry, Guid> key, Func<Guid, NamedReference> name) =>
        entries
            .GroupBy(key)
            .Select(g => new ReportGroupResponse(g.Key, name(g.Key).Name, InputLedger.Totals(g)))
            .OrderByDescending(g => g.Totals.TotalCents)
            .ThenBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
}
