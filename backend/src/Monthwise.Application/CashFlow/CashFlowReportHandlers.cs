using Monthwise.Application.Cards;
using Monthwise.Domain.CashFlow;
using Monthwise.Domain.Common;

namespace Monthwise.Application.CashFlow;

/// <summary>Filters of the cash flow chart (FR-12).</summary>
public sealed record ChartFilter(Guid? AccountId, Guid? PayerId, Guid? CategoryId);

/// <summary>Received and expected inputs per day or month (FR-12).</summary>
public sealed class GetCashFlowChartHandler(InputLedger ledger)
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

        var buckets = new List<ChartBucketResponse>();
        for (var start = BucketStart(from, granularity); start <= to; start = granularity == ChartGranularity.Day ? start.AddDays(1) : start.AddMonths(1))
        {
            var bucket = entries[start].ToList();
            buckets.Add(new ChartBucketResponse(
                start,
                Money.Sum(bucket.Where(e => e.IsReceived).Select(e => e.Details.Amount)).Cents,
                Money.Sum(bucket.Where(e => !e.IsReceived).Select(e => e.Details.Amount)).Cents));
        }
        return Result.Success(new CashFlowChartResponse(from, to, granularity, buckets));
    }

    private static DateOnly BucketStart(DateOnly date, ChartGranularity granularity) =>
        granularity == ChartGranularity.Day ? date : new DateOnly(date.Year, date.Month, 1);
}

/// <summary>The monthly cash flow report (FR-13).</summary>
public sealed class GetMonthlyReportHandler(
    InputLedger ledger,
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

        return new MonthlyReportResponse(
            month.ToString(),
            totals,
            Group(entries, e => e.Details.CategoryId, names.Category),
            Group(entries, e => e.Details.AccountId, names.Account),
            Group(entries, e => e.Details.PayerId, names.Payer),
            monthEntries,
            previousTotal,
            totals.TotalCents - previousTotal);
    }

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
