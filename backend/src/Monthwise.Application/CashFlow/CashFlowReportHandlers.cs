using Monthwise.Application.Cards;
using Monthwise.Application.Loans;
using Monthwise.Domain.CashFlow;
using Monthwise.Domain.Common;
using Monthwise.Domain.Loans;

namespace Monthwise.Application.CashFlow;

/// <summary>
/// Filters of the cash flow chart (FR-12, Appendix 1). The account applies to inputs and outputs;
/// payer and category to inputs only, the loan category to outputs only.
/// </summary>
public sealed record ChartFilter(Guid? AccountId, Guid? PayerId, Guid? CategoryId, Guid? LoanCategoryId = null);

/// <summary>Received and expected inputs, and paid and expected outputs, per day or month (FR-12, Appendix 1).</summary>
public sealed class GetCashFlowChartHandler(InputLedger ledger, LoanLedger loanLedger)
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
        var payments = book.PaymentsBetween(from, to)
            .Where(p => filter.AccountId is null || p.AccountId == filter.AccountId)
            .Where(p => filter.LoanCategoryId is null || book.CategoryOf(p.LoanId) == filter.LoanCategoryId)
            .ToLookup(p => BucketStart(p.MonthDate, granularity));

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
}

/// <summary>The monthly cash flow report (FR-13, Appendix 1).</summary>
public sealed class GetMonthlyReportHandler(
    InputLedger ledger,
    LoanLedger loanLedger,
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
        var payments = book.PaymentsBetween(month.FirstDay, month.LastDay).ToList();
        var outputTotals = LoanBook.Totals(payments);
        var previousOutputs = LoanBook.Totals(book.PaymentsBetween(previous.FirstDay, previous.LastDay)).TotalCents;
        var outputs = new ReportOutputsResponse(
            outputTotals,
            GroupOutputs(payments, p => book.CategoryOf(p.LoanId), id => book.Category(id!.Value).Name),
            GroupOutputs(payments, p => p.AccountId, id => id is { } accountId ? book.Account(accountId).Name : NoAccountYet),
            GroupOutputs(payments, p => p.LoanId, id => book.LoanReference(id!.Value).Name));

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

    /// <summary>Output groups with their totals, largest total first.</summary>
    private static List<ReportOutputGroupResponse> GroupOutputs(
        IEnumerable<ScheduledPayment> payments, Func<ScheduledPayment, Guid?> key, Func<Guid?, string> name) =>
        payments
            .GroupBy(key)
            .Select(g => new ReportOutputGroupResponse(g.Key, name(g.Key), LoanBook.Totals(g)))
            .OrderByDescending(g => g.Totals.TotalCents)
            .ThenBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

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
