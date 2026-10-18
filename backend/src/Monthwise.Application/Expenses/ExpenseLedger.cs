using Monthwise.Application.CardPurchases;
using Monthwise.Application.Cards;
using Monthwise.Application.CashFlow;
using Monthwise.Application.Common;
using Monthwise.Application.Loans;
using Monthwise.Application.SharedCosts;
using Monthwise.Domain.Cards;
using Monthwise.Domain.Common;
using Monthwise.Domain.Expenses;
using Monthwise.Domain.SharedCosts;

namespace Monthwise.Application.Expenses;

/// <summary>
/// A bill or expense as an output of the cash flow (FR-10): paid or expected, on its date, from an
/// account (null for a bill whose account is not known yet). Bills and expenses paid with a credit card
/// are not outputs of their own: they are in the card payment.
/// </summary>
public sealed record ExpenseOutput(DateOnly Date, bool IsPaid, Money Amount, Guid? AccountId, Guid CategoryId);

/// <summary>All bills and expenses on one day, with the names and card payments needed to show them.</summary>
public sealed class ExpenseBook
{
    private readonly Dictionary<Guid, Card> _cards;
    private readonly Dictionary<Guid, ExpenseCategory> _categories;
    private readonly ILookup<Guid?, Bill> _issuedByRecurringBill;
    private readonly CardPaymentBook _cardBook;

    internal ExpenseBook(
        DateOnly today,
        IReadOnlyList<Card> cards,
        IReadOnlyList<ExpenseCategory> categories,
        IReadOnlyList<Bill> bills,
        IReadOnlyList<Expense> expenses,
        CardPaymentBook cardBook,
        SharedCostBook shared)
    {
        Shared = shared;
        Today = today;
        _cards = cards.ToDictionary(c => c.Id);
        _categories = categories.ToDictionary(c => c.Id);
        Bills = bills;
        Expenses = expenses;
        _issuedByRecurringBill = bills.Where(b => b.RecurringBillId is not null && b.IsIssued).ToLookup(b => b.RecurringBillId);
        _cardBook = cardBook;
    }

    public DateOnly Today { get; }

    /// <summary>The shared bills and loan payments with their contributions (shared bills FR-5).</summary>
    public SharedCostBook Shared { get; }

    public IReadOnlyList<Bill> Bills { get; }
    public IReadOnlyList<Expense> Expenses { get; }

    public NamedReference Category(Guid id) => new(id, _categories.TryGetValue(id, out var category) ? category.Name : "Unknown");

    /// <summary>Whether what is paid with <paramref name="id"/> goes into a card payment.</summary>
    public bool IsCreditCard(Guid? id) => id is { } cardId && _cards.TryGetValue(cardId, out var card) && PaidFromRules.IsCreditCard(card);

    public PaidFromResponse? PaidFrom(Guid? id) =>
        id is { } cardId
            ? new PaidFromResponse(cardId, _cards.TryGetValue(cardId, out var card) ? card.Name : "Unknown", IsCreditCard(cardId))
            : null;

    /// <summary>A paid bill whose amount is in a card payment (FR-6).</summary>
    public bool IsPaidWithCard(Bill bill)
    {
        ArgumentNullException.ThrowIfNull(bill);
        return bill.IsPaid && IsCreditCard(bill.PaidFromId);
    }

    public bool IsPaidWithCard(Expense expense)
    {
        ArgumentNullException.ThrowIfNull(expense);
        return IsCreditCard(expense.PaidFromId);
    }

    /// <summary>
    /// The due date hint of a Not issued bill (FR-2): the due date of the last issued bill of the same
    /// recurring bill, moved to this bill's month (the last day of the month when it is shorter).
    /// </summary>
    public DateOnly? DueDateHint(Bill bill)
    {
        ArgumentNullException.ThrowIfNull(bill);
        if (bill.IsIssued || bill.RecurringBillId is null)
        {
            return null;
        }
        var last = _issuedByRecurringBill[bill.RecurringBillId]
            .Where(b => b.Month < bill.Month)
            .MaxBy(b => b.Month);
        return last?.DueDate is { } due ? due.AddMonths(last.Month.MonthsUntil(bill.Month)) : null;
    }

    public BillResponse ToResponse(Bill bill)
    {
        ArgumentNullException.ThrowIfNull(bill);
        var withCard = IsPaidWithCard(bill);
        return new BillResponse(
            bill.Id,
            bill.RecurringBillId,
            bill.Month.ToString(),
            bill.Name,
            Category(bill.CategoryId),
            bill.Payee,
            bill.Amount.Cents,
            bill.IssueDate,
            bill.DueDate,
            bill.State,
            bill.PaidDate,
            PaidFrom(bill.PaidFromId),
            bill.Description,
            bill.IsOverdue(Today),
            DueDateHint(bill),
            withCard,
            withCard && _cardBook.IsCharged(bill.PaidFromId!.Value, bill.PaidDate!.Value),
            Shared.ToResponse(bill.Share),
            bill.Share is { } share && bill.IsIssued ? share.OwnPartOf(bill.Amount).Cents : null,
            bill.IsShareChanged,
            bill.Share is null ? null : Shared.ContributionOf(ContributionSource.ForBill(bill.Id)));
    }

    /// <summary>What the household pays itself of the counted bills (FR-9): their total minus the participants' parts.</summary>
    public Money OwnPart(IEnumerable<Bill> bills) =>
        Money.Sum(bills.Where(b => b.IsIssued && !IsPaidWithCard(b)).Select(b => b.Share?.OwnPartOf(b.Amount) ?? b.Amount));

    public ExpenseResponse ToResponse(Expense expense)
    {
        ArgumentNullException.ThrowIfNull(expense);
        var withCard = IsPaidWithCard(expense);
        return new ExpenseResponse(
            expense.Id,
            expense.Date,
            expense.Amount.Cents,
            Category(expense.CategoryId),
            PaidFrom(expense.PaidFromId)!,
            expense.Description,
            expense.IsCashWithdrawal,
            withCard,
            withCard && _cardBook.IsCharged(expense.PaidFromId, expense.Date));
    }

    /// <summary>Bills of the Month screen: by due date (Not issued last), then name.</summary>
    public IReadOnlyList<BillResponse> ToSortedResponses(IEnumerable<Bill> bills) =>
        bills
            .OrderBy(b => b.IsIssued ? 0 : 1)
            .ThenBy(b => b.CashFlowDate)
            .ThenBy(b => b.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(ToResponse)
            .ToList();

    /// <summary>Expected, paid and all bills (FR-9), without Not issued bills and bills paid with a credit card.</summary>
    public LoanPaymentTotalsResponse BillTotals(IEnumerable<Bill> bills)
    {
        var counted = bills.Where(b => b.IsIssued && !IsPaidWithCard(b)).ToList();
        var expected = Money.Sum(counted.Where(b => !b.IsPaid).Select(b => b.Amount));
        var paid = Money.Sum(counted.Where(b => b.IsPaid).Select(b => b.Amount));
        return new LoanPaymentTotalsResponse(expected.Cents, paid.Cents, (expected + paid).Cents);
    }

    public ExpenseTotalsResponse ExpenseTotals(IEnumerable<Expense> expenses)
    {
        var list = expenses.ToList();
        var withCard = Money.Sum(list.Where(IsPaidWithCard).Select(e => e.Amount));
        var total = Money.Sum(list.Select(e => e.Amount));
        var cash = Money.Sum(list.Where(e => e.IsCashWithdrawal).Select(e => e.Amount));
        return new ExpenseTotalsResponse(total.Cents, (total - withCard).Cents, withCard.Cents, cash.Cents);
    }

    /// <summary>
    /// Bills and expenses as outputs of the cash flow (FR-10): issued bills and expenses that are not
    /// paid with a credit card. An expected bill counts on its due date, also when it will be paid with a
    /// credit card; it then has no account yet.
    /// </summary>
    public IEnumerable<ExpenseOutput> Outputs() => BillOutputs().Concat(ExpenseOutputs());

    /// <summary>The bills among <see cref="Outputs"/>.</summary>
    public IEnumerable<ExpenseOutput> BillOutputs() =>
        Bills
            .Where(b => b.IsIssued && !IsPaidWithCard(b))
            .Select(b => new ExpenseOutput(
                b.CashFlowDate!.Value,
                b.IsPaid,
                b.Amount,
                IsCreditCard(b.PaidFromId) ? null : b.PaidFromId,
                b.CategoryId));

    /// <summary>The expenses and cash withdrawals among <see cref="Outputs"/>.</summary>
    public IEnumerable<ExpenseOutput> ExpenseOutputs() =>
        Expenses
            .Where(e => !IsPaidWithCard(e))
            .Select(e => new ExpenseOutput(e.Date, true, e.Amount, e.PaidFromId, e.CategoryId));

    public IEnumerable<ExpenseOutput> OutputsBetween(DateOnly from, DateOnly to) => Outputs().Where(o => o.Date >= from && o.Date <= to);
}

/// <summary>Loads the <see cref="ExpenseBook"/> for today.</summary>
public sealed class ExpenseLedger(
    ICardRepository cards,
    IExpenseCategoryRepository categories,
    IBillRepository bills,
    IExpenseRepository expenses,
    CardPaymentLedger cardPaymentLedger,
    LoanLedger loanLedger,
    TimeProvider timeProvider)
{
    public async Task<ExpenseBook> LoadAsync(CancellationToken cancellationToken) =>
        new(
            timeProvider.Today(),
            await cards.ListAsync(cancellationToken),
            await categories.ListAsync(cancellationToken),
            await bills.ListAsync(cancellationToken),
            await expenses.ListAsync(cancellationToken),
            await cardPaymentLedger.LoadAsync(cancellationToken),
            (await loanLedger.LoadAsync(cancellationToken)).Shared);
}

/// <summary>
/// Checks the category and what a bill, recurring bill or expense is paid with. A hidden category, or
/// an account or card that can no longer be used, stays allowed when the entry already had it.
/// </summary>
public sealed class ExpenseInputChecker(ICardRepository cards, IExpenseCategoryRepository categories, TimeProvider timeProvider)
{
    public async Task<List<DomainError>> CheckAsync(
        Guid categoryId,
        Guid? existingCategoryId,
        Guid? paidFromId,
        Guid? existingPaidFromId,
        DateOnly date,
        bool primaryOnly,
        CancellationToken cancellationToken)
    {
        var errors = new List<DomainError>();
        if (categoryId != Guid.Empty)
        {
            var category = await categories.GetAsync(categoryId, cancellationToken);
            if (category is null)
            {
                errors.Add(ExpenseErrors.CategoryUnknown);
            }
            else if (category.IsHidden && categoryId != existingCategoryId)
            {
                errors.Add(ExpenseErrors.CategoryHidden);
            }
        }
        if (paidFromId is { } id && id != Guid.Empty)
        {
            var card = await cards.GetAsync(id, includeSensitiveData: false, cancellationToken);
            if (PaidFromRules.Check(card, date, timeProvider.Today(), primaryOnly, unchanged: id == existingPaidFromId) is { } error)
            {
                errors.Add(error);
            }
        }
        return errors;
    }
}
