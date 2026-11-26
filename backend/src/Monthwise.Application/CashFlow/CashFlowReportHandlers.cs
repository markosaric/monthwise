using Monthwise.Application.CardPurchases;
using Monthwise.Application.Cards;
using Monthwise.Application.Expenses;
using Monthwise.Application.Loans;
using Monthwise.Application.Localization;
using Monthwise.Application.SharedCosts;
using Monthwise.Domain.CashFlow;
using Monthwise.Domain.Common;

namespace Monthwise.Application.CashFlow;

/// <summary>
/// Filters of the cash flow chart (FR-12, Appendix 1). The account applies to inputs and outputs;
/// payer and category to inputs only. The loan category shows only the payments of those loans, the
/// card only that card's card payments (installments FR-8), and the expense category only the bills
/// and expenses of that category (bills and expenses FR-10). The borrower shows only the lending with that
/// borrower (money lending FR-10); lending is left out when any other filter but the account is set.
/// </summary>
public sealed record ChartFilter(
    Guid? AccountId,
    Guid? PayerId,
    Guid? CategoryId,
    Guid? LoanCategoryId = null,
    Guid? CardId = null,
    Guid? ExpenseCategoryId = null,
    Guid? BorrowerId = null)
{
    /// <summary>Whether a filter other than the account and the borrower is set, which leaves lending out.</summary>
    public bool FiltersOtherThanLending =>
        PayerId is not null || CategoryId is not null || LoanCategoryId is not null || CardId is not null || ExpenseCategoryId is not null;
}

/// <summary>
/// Received and expected inputs, and paid and expected outputs (loan and card payments, bills and
/// expenses), per day or month (FR-12, Appendix 1; installments FR-8; bills and expenses FR-10).
/// </summary>
public sealed class GetCashFlowChartHandler(
    InputLedger ledger,
    LoanLedger loanLedger,
    CardPaymentLedger cardPaymentLedger,
    ExpenseLedger expenseLedger,
    Revolving.RevolvingLedger revolvingLedger,
    Lendings.LendingLedger lendingLedger)
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

        var onlyLending = filter.BorrowerId is not null;
        var entries = (onlyLending ? [] : await ledger.EntriesAsync(from, to, cancellationToken))
            .Where(e => filter.AccountId is null || e.Details.AccountId == filter.AccountId)
            .Where(e => filter.PayerId is null || e.Details.PayerId == filter.PayerId)
            .Where(e => filter.CategoryId is null || e.Details.CategoryId == filter.CategoryId)
            .Select(e => new ChartInput(e.MonthDate, e.IsReceived, e.Details.Amount))
            .ToList();
        // Lending: repayments and amounts expected back are inputs, money lent an output (money lending FR-10).
        var lendingBook = await lendingLedger.LoadAsync(cancellationToken);
        var lendingIncluded = !filter.FiltersOtherThanLending;
        if (lendingIncluded)
        {
            entries.AddRange(lendingBook.InputsBetween(from, to)
                .Where(i => filter.AccountId is null || i.AccountId == filter.AccountId)
                .Where(i => filter.BorrowerId is null || i.PayerId == filter.BorrowerId)
                .Select(i => new ChartInput(i.Date, i.IsReceived, i.Amount)));
        }
        var inputs = entries.ToLookup(e => BucketStart(e.Date, granularity));
        var book = await loanLedger.LoadAsync(cancellationToken);
        IEnumerable<ChartOutput> loanPayments = onlyLending || filter.CardId is not null || filter.ExpenseCategoryId is not null
            ? []
            : book.PaymentsBetween(from, to)
                .Where(p => filter.AccountId is null || p.AccountId == filter.AccountId)
                .Where(p => filter.LoanCategoryId is null || book.CategoryOf(p.LoanId) == filter.LoanCategoryId)
                .Select(p => new ChartOutput(p.MonthDate, p.IsPaid, p.Amount));
        IEnumerable<ChartOutput> cardPayments = onlyLending || filter.LoanCategoryId is not null || filter.ExpenseCategoryId is not null
            ? []
            : (await cardPaymentLedger.LoadAsync(cancellationToken)).PaymentsBetween(from, to)
                .Where(p => filter.AccountId is null || p.AccountId == filter.AccountId)
                .Where(p => filter.CardId is null || p.CardId == filter.CardId)
                .Select(p => new ChartOutput(p.MonthDate, p.IsPaid, p.Amount));
        // Monthly bills and extra payments of revolving cards count like card payments (revolving FR-11).
        IEnumerable<ChartOutput> revolving = onlyLending || filter.LoanCategoryId is not null || filter.ExpenseCategoryId is not null
            ? []
            : (await revolvingLedger.LoadAsync(cancellationToken, YearMonth.FromDate(to))).OutputsBetween(from, to)
                .Where(o => filter.AccountId is null || o.AccountId == filter.AccountId)
                .Where(o => filter.CardId is null || o.CardId == filter.CardId)
                .Select(o => new ChartOutput(o.Date, o.IsPaid, o.Amount));
        IEnumerable<ChartOutput> billsAndExpenses = onlyLending || filter.LoanCategoryId is not null || filter.CardId is not null
            ? []
            : (await expenseLedger.LoadAsync(cancellationToken)).OutputsBetween(from, to)
                .Where(o => filter.AccountId is null || o.AccountId == filter.AccountId)
                .Where(o => filter.ExpenseCategoryId is null || o.CategoryId == filter.ExpenseCategoryId)
                .Select(o => new ChartOutput(o.Date, o.IsPaid, o.Amount));
        IEnumerable<ChartOutput> lent = !lendingIncluded
            ? []
            : lendingBook.OutputsBetween(from, to)
                .Where(o => filter.AccountId is null || o.AccountId == filter.AccountId)
                .Where(o => filter.BorrowerId is null || o.PayerId == filter.BorrowerId)
                .Select(o => new ChartOutput(o.Date, true, o.Amount));
        var payments = loanPayments.Concat(cardPayments).Concat(revolving).Concat(billsAndExpenses).Concat(lent)
            .ToLookup(p => BucketStart(p.Date, granularity));

        var buckets = new List<ChartBucketResponse>();
        for (var start = BucketStart(from, granularity); start <= to; start = granularity == ChartGranularity.Day ? start.AddDays(1) : start.AddMonths(1))
        {
            var bucket = inputs[start].ToList();
            var outputs = payments[start].ToList();
            buckets.Add(new ChartBucketResponse(
                start,
                Money.Sum(bucket.Where(e => e.IsReceived).Select(e => e.Amount)).Cents,
                Money.Sum(bucket.Where(e => !e.IsReceived).Select(e => e.Amount)).Cents,
                Money.Sum(outputs.Where(p => p.IsPaid).Select(p => p.Amount)).Cents,
                Money.Sum(outputs.Where(p => !p.IsPaid).Select(p => p.Amount)).Cents));
        }
        return Result.Success(new CashFlowChartResponse(from, to, granularity, buckets));
    }

    private static DateOnly BucketStart(DateOnly date, ChartGranularity granularity) =>
        granularity == ChartGranularity.Day ? date : new DateOnly(date.Year, date.Month, 1);

    private sealed record ChartOutput(DateOnly Date, bool IsPaid, Money Amount);

    private sealed record ChartInput(DateOnly Date, bool IsReceived, Money Amount);
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
    Revolving.RevolvingLedger revolvingLedger,
    Lendings.LendingLedger lendingLedger,
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

        // Repayments and amounts expected back count as inputs, money lent as an output (money lending FR-10).
        var lendingBook = await lendingLedger.LoadAsync(cancellationToken);
        var inputs = ReportInputs(entries, lendingBook, month);
        var totals = Totals(inputs);
        var previousTotal = Totals(ReportInputs(previousEntries, lendingBook, previous)).TotalCents;

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
        var revolvingBook = await revolvingLedger.LoadAsync(cancellationToken, month);
        var outputs = Outputs(book, cardBook, expenseBook, revolvingBook, lendingBook, month);
        var outputTotals = outputs.Totals;
        var previousOutputs = Outputs(book, cardBook, expenseBook, revolvingBook, lendingBook, previous).Totals.TotalCents;
        string CategoryName(Guid id) => id == Lendings.LendingBook.GroupId ? Lendings.LendingBook.GroupName : names.Category(id).Name;

        return new MonthlyReportResponse(
            month.ToString(),
            totals,
            Group(inputs, i => i.CategoryId, CategoryName),
            Group(inputs, i => i.AccountId, id => names.Account(id).Name),
            Group(inputs, i => i.PayerId, id => names.Payer(id).Name),
            monthEntries,
            previousTotal,
            totals.TotalCents - previousTotal,
            outputs,
            totals.TotalCents - outputTotals.TotalCents,
            previousOutputs,
            previousTotal - previousOutputs,
            SharedCosts(book.Shared, entries, month),
            Lending(lendingBook, month));
    }

    /// <summary>The month's inputs and the lending that comes back in it (money lending FR-10).</summary>
    private static List<ReportInput> ReportInputs(IEnumerable<InputEntry> entries, Lendings.LendingBook lendingBook, YearMonth month) =>
        entries
            .Select(e => new ReportInput(e.IsReceived, e.Details.Amount, e.Details.CategoryId, e.Details.AccountId, e.Details.PayerId))
            .Concat(lendingBook.InputsBetween(month.FirstDay, month.LastDay)
                .Select(i => new ReportInput(i.IsReceived, i.Amount, Lendings.LendingBook.GroupId, i.AccountId, i.PayerId)))
            .ToList();

    /// <summary>The month's lending by borrower: lent, returned and expected back (money lending FR-10).</summary>
    private static Lendings.ReportLendingResponse? Lending(Lendings.LendingBook lendingBook, YearMonth month)
    {
        var lent = lendingBook.OutputsBetween(month.FirstDay, month.LastDay).ToList();
        var back = lendingBook.InputsBetween(month.FirstDay, month.LastDay).ToList();
        if (lent.Count == 0 && back.Count == 0)
        {
            return null;
        }
        var byBorrower = lent.Select(o => o.PayerId).Concat(back.Select(i => i.PayerId)).Distinct()
            .Select(id => new Lendings.ReportLendingBorrowerResponse(
                id,
                lendingBook.Borrower(id).Name,
                Money.Sum(lent.Where(o => o.PayerId == id).Select(o => o.Amount)).Cents,
                Money.Sum(back.Where(i => i.PayerId == id && i.IsReceived).Select(i => i.Amount)).Cents,
                Money.Sum(back.Where(i => i.PayerId == id && !i.IsReceived).Select(i => i.Amount)).Cents))
            .OrderBy(b => b.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        return new Lendings.ReportLendingResponse(
            byBorrower.Sum(b => b.LentCents),
            byBorrower.Sum(b => b.ReturnedCents),
            byBorrower.Sum(b => b.ExpectedBackCents),
            byBorrower);
    }

    private static InputTotalsResponse Totals(IEnumerable<ReportInput> inputs)
    {
        var list = inputs.ToList();
        var expected = Money.Sum(list.Where(i => !i.IsReceived).Select(i => i.Amount));
        var received = Money.Sum(list.Where(i => i.IsReceived).Select(i => i.Amount));
        return new InputTotalsResponse(expected.Cents, received.Cents, (expected + received).Cents);
    }

    /// <summary>One input of the report: an input, a repayment or an amount expected back.</summary>
    private sealed record ReportInput(bool IsReceived, Money Amount, Guid CategoryId, Guid AccountId, Guid PayerId);

    /// <summary>
    /// The month's shared bills and loan payments (by the date they count on) with the participants' parts
    /// and the household's own part, and the month's contributions (shared bills FR-9).
    /// </summary>
    private static ReportSharedCostsResponse SharedCosts(SharedCostBook shared, IEnumerable<InputEntry> entries, YearMonth month)
    {
        var costs = shared.Costs.Where(c => YearMonth.FromDate(c.CostDate) == month).ToList();
        var total = Money.Sum(costs.Select(c => c.Amount));
        var parts = Money.Sum(costs.Select(c => c.Share.PartOf(c.Amount)));
        var contributions = entries.Where(e => e.Contribution is not null).ToList();
        return new ReportSharedCostsResponse(
            total.Cents,
            parts.Cents,
            (total - parts).Cents,
            Money.Sum(contributions.Where(e => e.IsReceived).Select(e => e.Details.Amount)).Cents,
            Money.Sum(contributions.Where(e => !e.IsReceived).Select(e => e.Details.Amount)).Cents);
    }

    /// <summary>The group name of bills paid later, which have no account yet.</summary>
    public static string NoAccountYet => ServerTexts.Translate("No account yet");

    /// <summary>The name of the output category that groups all card payments.</summary>
    public static string CardPayments => ServerTexts.Translate("Card payments");

    /// <summary>
    /// The month's outputs: loan payments, card payments (monthly bills and extra payments of revolving
    /// cards included, revolving FR-11), bills and expenses, grouped by category (loan category, one Card
    /// payments group, or expense category), account, loan and card.
    /// </summary>
    private static ReportOutputsResponse Outputs(
        LoanBook book,
        CardPaymentBook cardBook,
        ExpenseBook expenseBook,
        Revolving.RevolvingBook revolvingBook,
        Lendings.LendingBook lendingBook,
        YearMonth month)
    {
        var loanPayments = book.PaymentsBetween(month.FirstDay, month.LastDay)
            .Select(p => new ReportOutput(p.IsPaid, p.Amount, p.AccountId, book.CategoryOf(p.LoanId), p.LoanId, null))
            .ToList();
        var cardPayments = cardBook.PaymentsBetween(month.FirstDay, month.LastDay)
            .Select(p => new ReportOutput(p.IsPaid, p.Amount, p.NeedsAccount ? null : p.AccountId, null, null, p.CardId))
            .Concat(revolvingBook.OutputsBetween(month.FirstDay, month.LastDay)
                .Select(o => new ReportOutput(o.IsPaid, o.Amount, o.AccountId, null, null, o.CardId)))
            .ToList();
        var billsAndExpenses = expenseBook.OutputsBetween(month.FirstDay, month.LastDay)
            .Select(o => new ReportOutput(o.IsPaid, o.Amount, o.AccountId, null, null, null, o.CategoryId))
            .ToList();
        var lent = lendingBook.OutputsBetween(month.FirstDay, month.LastDay)
            .Select(o => new ReportOutput(true, o.Amount, o.AccountId, null, null, null))
            .ToList();
        var all = loanPayments.Concat(cardPayments).Concat(billsAndExpenses).Concat(lent).ToList();
        var byCategory = GroupOutputs(loanPayments, o => o.LoanCategoryId, id => book.Category(id!.Value).Name);
        byCategory.AddRange(GroupOutputs(billsAndExpenses, o => o.ExpenseCategoryId, id => expenseBook.Category(id!.Value).Name));
        if (lent.Count > 0)
        {
            byCategory.Add(new ReportOutputGroupResponse(Lendings.LendingBook.GroupId, Lendings.LendingBook.GroupName, Totals(lent)));
        }
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
        IEnumerable<ReportInput> inputs, Func<ReportInput, Guid> key, Func<Guid, string> name) =>
        inputs
            .GroupBy(key)
            .Select(g => new ReportGroupResponse(g.Key, name(g.Key), Totals(g)))
            .OrderByDescending(g => g.Totals.TotalCents)
            .ThenBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
}
