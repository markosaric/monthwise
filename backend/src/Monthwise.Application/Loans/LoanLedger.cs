using Monthwise.Application.Cards;
using Monthwise.Application.CashFlow;
using Monthwise.Application.Common;
using Monthwise.Application.SharedCosts;
using Monthwise.Domain.Cards;
using Monthwise.Domain.Common;
using Monthwise.Domain.Loans;
using Monthwise.Domain.SharedCosts;

namespace Monthwise.Application.Loans;

/// <summary>
/// All loans with their calculated payments on one day, and the names to show them. The loan
/// equivalent of <see cref="InputLedger"/>.
/// </summary>
public sealed class LoanBook
{
    private readonly Dictionary<Guid, LoanSchedule> _schedules;
    private readonly Dictionary<Guid, Loan> _loans;
    private readonly Dictionary<Guid, LoanCategory> _categories;
    private readonly Dictionary<Guid, LoanSubcategory> _subcategories;
    private readonly Dictionary<Guid, string> _lenders;
    private readonly Dictionary<Guid, Card> _accounts;

    internal LoanBook(
        DateOnly today,
        IReadOnlyList<Loan> loans,
        IReadOnlyList<LoanPayment> stored,
        IReadOnlyList<LoanCategory> categories,
        IReadOnlyList<LoanSubcategory> subcategories,
        IReadOnlyList<Bank> lenders,
        IReadOnlyList<Card> accounts)
    {
        Today = today;
        _loans = loans.ToDictionary(l => l.Id);
        _categories = categories.ToDictionary(c => c.Id);
        _subcategories = subcategories.ToDictionary(s => s.Id);
        _lenders = lenders.ToDictionary(b => b.Id, b => b.Name);
        _accounts = accounts.ToDictionary(c => c.Id);
        var storedByLoan = stored.ToLookup(p => p.LoanId);
        _schedules = loans.ToDictionary(l => l.Id, l => l.Schedule(storedByLoan[l.Id], ChargesStopOn(l)));
    }

    public DateOnly Today { get; }

    /// <summary>The shared bills and loan payments with their contributions (shared bills FR-5).</summary>
    public SharedCostBook Shared { get; internal set; } = null!;

    public IReadOnlyCollection<Loan> Loans => _loans.Values;

    public IReadOnlyList<LoanCategory> Categories => _categories.Values.ToList();

    public LoanSchedule Schedule(Guid loanId) => _schedules[loanId];

    /// <summary>All payments of all loans.</summary>
    public IEnumerable<ScheduledPayment> Payments() => _schedules.Values.SelectMany(s => s.Payments(Today));

    /// <summary>Payments shown between the dates (inclusive), by paid date or due date.</summary>
    public IEnumerable<ScheduledPayment> PaymentsBetween(DateOnly from, DateOnly to) =>
        Payments().Where(p => p.MonthDate >= from && p.MonthDate <= to);

    public Guid CategoryOf(Guid loanId) => _loans[loanId].CategoryId;

    public NamedReference Category(Guid id) =>
        new(id, _categories.TryGetValue(id, out var category) ? category.Name : "Unknown");

    public NamedReference Subcategory(Guid id) =>
        new(id, _subcategories.TryGetValue(id, out var subcategory) ? subcategory.Name : "Unknown");

    public NamedReference Lender(Guid id) => new(id, _lenders.TryGetValue(id, out var name) ? name : "Unknown");

    public NamedReference Account(Guid id) => new(id, _accounts.TryGetValue(id, out var card) ? card.Name : "Unknown");

    public NamedReference LoanReference(Guid loanId) => new(loanId, _loans[loanId].Name);

    public LoanResponse ToResponse(Loan loan)
    {
        ArgumentNullException.ThrowIfNull(loan);
        var summary = _schedules[loan.Id].Summary(Today);
        return new LoanResponse(
            loan.Id,
            loan.Name,
            Category(loan.CategoryId),
            Subcategory(loan.SubcategoryId),
            Lender(loan.LenderId),
            loan.PaymentMethod,
            loan.AccountId is { } accountId ? Account(accountId) : null,
            loan.StartMonth.ToString(),
            loan.EndMonth.ToString(),
            loan.PaymentDay,
            loan.MonthlyPayment.Cents,
            loan.PaymentCount,
            loan.ResidualValue?.Cents,
            loan.Description,
            loan.ClosedOn,
            summary.Status,
            summary.CurrentMonthlyPayment.Cents,
            summary.CurrentPaymentDay,
            summary.NextPaymentDate,
            summary.PaymentsPaid,
            summary.PaymentsLeft,
            summary.PaidAmount.Cents,
            summary.RemainingAmount.Cents,
            summary.HasOverduePayment,
            summary.NeedsAccount,
            Shared.ToResponse(loan.Share));
    }

    public LoanPaymentResponse ToResponse(ScheduledPayment payment)
    {
        ArgumentNullException.ThrowIfNull(payment);
        var loan = _loans[payment.LoanId];
        return new LoanPaymentResponse(
            loan.Id,
            payment.Month.ToString(),
            payment.Kind,
            loan.Name,
            Category(loan.CategoryId),
            Subcategory(loan.SubcategoryId),
            payment.Number,
            payment.Total,
            payment.Amount.Cents,
            payment.BillDate,
            payment.DueDate,
            payment.PaidDate,
            payment.AccountId is { } accountId ? Account(accountId) : null,
            payment.Method,
            payment.IsPaid,
            payment.IsChanged,
            payment.IsOverdue(Today),
            payment.NeedsAccount,
            Shared.ToResponse(payment.Share),
            payment.Share?.OwnPartOf(payment.Amount).Cents,
            payment.IsShareChanged,
            payment.Share is null
                ? null
                : Shared.ContributionOf(ContributionSource.ForLoanPayment(payment.LoanId, payment.Month, payment.Kind)));
    }

    /// <summary>Payments in list order: by paid or due date, then loan name.</summary>
    public IReadOnlyList<LoanPaymentResponse> ToSortedResponses(IEnumerable<ScheduledPayment> payments) =>
        payments
            .Select(ToResponse)
            .OrderBy(p => p.PaidDate ?? p.DueDate)
            .ThenBy(p => p.LoanName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(p => p.Kind)
            .ToList();

    /// <summary>Expected, paid and all payments (FR-4).</summary>
    public static LoanPaymentTotalsResponse Totals(IEnumerable<ScheduledPayment> payments)
    {
        var list = payments.ToList();
        var expected = Money.Sum(list.Where(p => !p.IsPaid).Select(p => p.Amount));
        var paid = Money.Sum(list.Where(p => p.IsPaid).Select(p => p.Amount));
        return new LoanPaymentTotalsResponse(expected.Cents, paid.Cents, (expected + paid).Cents);
    }

    /// <summary>What the household pays itself of the payments: the total minus the participants' parts (shared bills FR-7).</summary>
    public static Money OwnPart(IEnumerable<ScheduledPayment> payments) =>
        Money.Sum(payments.Select(p => p.Share?.OwnPartOf(p.Amount) ?? p.Amount));

    private DateOnly? ChargesStopOn(Loan loan) =>
        loan.PaymentMethod == PaymentMethod.StandingOrder && !loan.IsClosed && loan.AccountId is { } accountId
            ? LoanAccount.ChargesStopOn(_accounts.GetValueOrDefault(accountId), Today)
            : null;
}

/// <summary>Loads the <see cref="LoanBook"/> for today.</summary>
public sealed class LoanLedger(
    ILoanRepository loans,
    ILoanPaymentRepository payments,
    ILoanCategoryRepository categories,
    ILoanSubcategoryRepository subcategories,
    ICardListRepository<Bank> banks,
    ICardRepository cards,
    SharedCostLedger sharedCostLedger,
    TimeProvider timeProvider)
{
    public async Task<LoanBook> LoadAsync(CancellationToken cancellationToken)
    {
        var book = new LoanBook(
            timeProvider.Today(),
            await loans.ListAsync(cancellationToken),
            await payments.ListAsync(null, cancellationToken),
            await categories.ListAsync(cancellationToken),
            await subcategories.ListAsync(cancellationToken),
            await banks.ListAsync(cancellationToken),
            await cards.ListAsync(cancellationToken));
        book.Shared = await sharedCostLedger.LoadAsync(book, cancellationToken);
        return book;
    }

    /// <summary>
    /// Accounts with ongoing standing orders: standing order loans with expected payments (FR-3). They
    /// stay on the Dashboard and in the default Settings list while blocked or expired.
    /// </summary>
    public async Task<IReadOnlySet<Guid>> AccountsWithOngoingStandingOrdersAsync(CancellationToken cancellationToken)
    {
        var book = await LoadAsync(cancellationToken);
        return book.Loans
            .Where(l => l.PaymentMethod == PaymentMethod.StandingOrder && !l.IsClosed && l.AccountId is not null)
            .Where(l => book.Schedule(l.Id).Payments(book.Today).Any(p => !p.IsPaid && p.Kind == LoanPaymentKind.Regular))
            .Select(l => l.AccountId!.Value)
            .ToHashSet();
    }
}
