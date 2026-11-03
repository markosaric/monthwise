using Monthwise.Application.CardPurchases;
using Monthwise.Application.Cards;
using Monthwise.Application.CashFlow;
using Monthwise.Application.Common;
using Monthwise.Application.Expenses;
using Monthwise.Application.Loans;
using Monthwise.Domain.CardPurchases;
using Monthwise.Domain.Cards;
using Monthwise.Domain.Common;
using Monthwise.Domain.Expenses;
using Monthwise.Domain.Loans;
using Monthwise.Domain.Revolving;

namespace Monthwise.Application.Revolving;

/// <summary>A charge with what the card page shows of it (revolving FR-6).</summary>
public sealed record RevolvingChargeSource(RevolvingCharge Charge, string Description, Guid? CategoryId);

/// <summary>
/// A monthly bill or an extra payment as an output of the cash flow (revolving FR-8, FR-11): paid or
/// expected, on its date, from an account (null for a bill that needs an account).
/// </summary>
public sealed record RevolvingOutput(Guid CardId, DateOnly Date, bool IsPaid, Money Amount, Guid? AccountId);

/// <summary>
/// All revolving cards with their debt and monthly bills on one day, and the names to show them. The
/// revolving equivalent of <see cref="CardPaymentBook"/>.
/// </summary>
public sealed class RevolvingBook
{
    private readonly Dictionary<Guid, Card> _cards;
    private readonly Dictionary<Guid, RevolvingDebtCalculator> _calculators;
    private readonly Dictionary<Guid, ExpenseCategory> _categories;
    private readonly List<RevolvingChargeSource> _charges;
    private readonly List<ExtraPayment> _payments;
    private readonly List<DebtEntry> _entries;

    internal RevolvingBook(
        DateOnly today,
        YearMonth horizon,
        IReadOnlyList<Card> cards,
        IReadOnlyList<DebtEntry> entries,
        IReadOnlyList<RevolvingChargeSource> charges,
        IReadOnlyList<ExtraPayment> payments,
        IReadOnlyList<RevolvingBill> stored,
        IReadOnlyList<ExpenseCategory> categories)
    {
        Today = today;
        _cards = cards.ToDictionary(c => c.Id);
        _categories = categories.ToDictionary(c => c.Id);
        _charges = charges.ToList();
        _payments = payments.ToList();
        _entries = entries.ToList();
        var revolving = cards.Where(c => c.IsRevolving).Select(c => c.Id).ToHashSet();
        var entriesByCard = entries.Where(e => revolving.Contains(e.CardId)).ToLookup(e => e.CardId);
        var chargesByCard = _charges.Select(c => c.Charge).ToLookup(c => c.CardId);
        var paymentsByCard = payments.ToLookup(p => p.CardId);
        var storedByCard = stored.ToLookup(b => b.CardId);
        _calculators = cards
            .Where(c => c.IsRevolving)
            .ToDictionary(c => c.Id, c => new RevolvingDebtCalculator(
                c, entriesByCard[c.Id], chargesByCard[c.Id], paymentsByCard[c.Id], storedByCard[c.Id], today, horizon, ChargesStopOn(c)));
    }

    public DateOnly Today { get; }

    /// <summary>The calculators of every revolving card, archived ones included.</summary>
    public IReadOnlyCollection<RevolvingDebtCalculator> Calculators => _calculators.Values;

    public RevolvingDebtCalculator? Calculator(Guid cardId) => _calculators.GetValueOrDefault(cardId);

    public Card? Card(Guid id) => _cards.GetValueOrDefault(id);

    /// <summary>Today's debt of a revolving card; zero for other cards.</summary>
    public Money DebtOf(Guid cardId) => _calculators.TryGetValue(cardId, out var calculator) ? calculator.Debt : Money.Zero;

    /// <summary>Every monthly bill of every card, estimated ones included.</summary>
    public IEnumerable<ScheduledRevolvingBill> Bills() => _calculators.Values.SelectMany(c => c.Bills);

    public IEnumerable<ExtraPayment> ExtraPayments => _payments.Where(p => _calculators.ContainsKey(p.CardId));

    /// <summary>
    /// The monthly bills (on their paid or due date) and extra payments (on their date) as outputs of the
    /// cash flow. A bill without a minimum payment is not an output.
    /// </summary>
    public IEnumerable<RevolvingOutput> Outputs() =>
        Bills()
            .Where(b => b.IsPaid || b.Minimum.IsPositive)
            .Select(b => new RevolvingOutput(b.CardId, b.MonthDate, b.IsPaid, b.Amount, b.NeedsAccount ? null : b.AccountId))
            .Concat(ExtraPayments.Select(p => new RevolvingOutput(p.CardId, p.Date, true, p.Amount, p.AccountId)));

    public IEnumerable<RevolvingOutput> OutputsBetween(DateOnly from, DateOnly to) => Outputs().Where(o => o.Date >= from && o.Date <= to);

    /// <summary>Revolving cards with debt: an ongoing transaction of the card (card settings FR-1).</summary>
    public IEnumerable<Guid> CardsWithDebt => _calculators.Values.Where(c => c.Debt.IsPositive).Select(c => c.Card.Id);

    public NamedReference CardReference(Guid id) => new(id, _cards.TryGetValue(id, out var card) ? card.Name : "Unknown");

    public RevolvingBillResponse ToResponse(ScheduledRevolvingBill bill)
    {
        ArgumentNullException.ThrowIfNull(bill);
        return new RevolvingBillResponse(
            bill.CardId,
            CardReference(bill.CardId).Name,
            bill.Month.ToString(),
            bill.BillDate,
            bill.DueDate,
            bill.Debt.Cents,
            bill.Interest.Cents,
            bill.Minimum.Cents,
            bill.IsCorrected,
            bill.IsEstimated,
            bill.IsPaid,
            bill.PaidDate,
            bill.PaidAmount?.Cents,
            bill.AccountId is { } accountId ? CardReference(accountId) : null,
            bill.PaysFromAccountDirectly,
            bill.PaidAutomatically,
            bill.IsOverdue(Today),
            bill.NeedsAccount);
    }

    public ExtraPaymentResponse ToResponse(ExtraPayment payment)
    {
        ArgumentNullException.ThrowIfNull(payment);
        return new ExtraPaymentResponse(
            payment.Id, payment.CardId, CardReference(payment.CardId).Name, CardReference(payment.AccountId),
            payment.Date, payment.Amount.Cents, payment.Description);
    }

    public RevolvingCardResponse ToResponse(RevolvingDebtCalculator calculator)
    {
        ArgumentNullException.ThrowIfNull(calculator);
        var card = calculator.Card;
        var limit = card.DebtLimit ?? Money.Zero;
        var debt = calculator.Debt;
        return new RevolvingCardResponse(
            card.Id,
            card.Name,
            card.CardNumberMasked,
            card.StatusOn(Today),
            card.CanBeUsedOn(Today),
            debt.Cents,
            limit.Cents,
            (limit - debt).Cents,
            debt > limit,
            calculator.NextBill is { } next ? ToResponse(next) : null,
            Terms(card),
            ChargesStopOn(card) is not null);
    }

    /// <summary>The revolving terms the card form edits: the ones from next month on, and every version.</summary>
    public CardRevolvingResponse? Terms(Card card) => TermsOf(card, Today, CardReference);

    internal static CardRevolvingResponse? TermsOf(Card card, DateOnly today, Func<Guid, NamedReference> reference)
    {
        ArgumentNullException.ThrowIfNull(card);
        if (card.RevolvingFor(YearMonth.FromDate(today).Next()) is not { } terms)
        {
            return null;
        }
        var versions = card.RevolvingTermVersions
            .Select(t => new RevolvingTermsResponse(
                t.FromMonth == CardBillingTerms.Beginning ? null : t.FromMonth.ToString(),
                t.MinPaymentBasisPoints,
                t.MinPaymentAmount.Cents,
                t.InterestBasisPoints,
                t.MinPaymentInterestBasisPoints,
                t.BillDay,
                t.DueDay))
            .ToList();
        return new CardRevolvingResponse(
            terms.MinPaymentBasisPoints,
            terms.MinPaymentAmount.Cents,
            terms.InterestBasisPoints,
            terms.MinPaymentInterestBasisPoints,
            terms.BillDay,
            terms.DueDay,
            reference(terms.PayingAccountId),
            terms.PaysFromAccountDirectly,
            versions);
    }

    /// <summary>The card page (FR-10): newest first, the debt history up to today only.</summary>
    public RevolvingCardDetailsResponse Details(RevolvingDebtCalculator calculator)
    {
        ArgumentNullException.ThrowIfNull(calculator);
        var cardId = calculator.Card.Id;
        var changes = calculator.Movements.Where(m => m.SourceId is not null).GroupBy(m => m.SourceId!.Value)
            .ToDictionary(g => g.Key, g => g.First().Change);
        var entries = _entries
            .Where(e => e.CardId == cardId)
            .OrderByDescending(e => e.Date)
            .ThenByDescending(e => e.CreatedAt)
            .Select(e => new DebtEntryResponse(
                e.Id,
                e.CardId,
                e.Kind,
                e.Date,
                e.Amount.Cents,
                changes.GetValueOrDefault(e.Id).Cents,
                e.Note,
                e.LinkedCardId is { } linked ? CardReference(linked) : null))
            .ToList();
        var charges = _charges
            .Where(c => c.Charge.CardId == cardId)
            .OrderByDescending(c => c.Charge.Date)
            .Select(c => new RevolvingChargeResponse(
                c.Charge.SourceId,
                c.Charge.Kind,
                c.Charge.Date,
                c.Description,
                c.CategoryId is { } categoryId
                    ? new NamedReference(categoryId, _categories.TryGetValue(categoryId, out var category) ? category.Name : "Unknown")
                    : null,
                c.Charge.Amount.Cents))
            .ToList();
        return new RevolvingCardDetailsResponse(
            ToResponse(calculator),
            calculator.Bills.OrderByDescending(b => b.Month).Select(ToResponse).ToList(),
            ExtraPayments.Where(p => p.CardId == cardId).OrderByDescending(p => p.Date).ThenByDescending(p => p.CreatedAt)
                .Select(ToResponse).ToList(),
            entries,
            charges,
            calculator.Movements
                .Where(m => !m.IsEstimated && m.Date <= Today)
                .Select(m => new RevolvingMovementResponse(
                    m.Date, m.Kind, m.Change.Cents, m.DebtAfter.Cents, m.SourceId, m.BillMonth?.ToString()))
                .ToList());
    }

    /// <summary>Expected, paid and all monthly bills and extra payments.</summary>
    public static LoanPaymentTotalsResponse Totals(IEnumerable<ScheduledRevolvingBill> bills, IEnumerable<ExtraPayment> payments)
    {
        var billList = bills.Where(b => b.IsPaid || b.Minimum.IsPositive).ToList();
        var expected = Money.Sum(billList.Where(b => !b.IsPaid).Select(b => b.Amount));
        var paid = Money.Sum(billList.Where(b => b.IsPaid).Select(b => b.Amount)) + Money.Sum(payments.Select(p => p.Amount));
        return new LoanPaymentTotalsResponse(expected.Cents, paid.Cents, (expected + paid).Cents);
    }

    /// <summary>
    /// The first day monthly bills can no longer be paid automatically from the card's paying account,
    /// or null while it is a usable account. The account does not have to be primary (revolving R6).
    /// </summary>
    private DateOnly? ChargesStopOn(Card card)
    {
        if (card.PayingAccountId is not { } accountId)
        {
            return null;
        }
        var account = _cards.GetValueOrDefault(accountId);
        return account is not null && account.IsAccount && account.CanBeUsedOn(Today) ? null : LoanAccount.ChargesStopOn(account, Today);
    }
}

/// <summary>Loads the <see cref="RevolvingBook"/> for today.</summary>
public sealed class RevolvingLedger(
    ICardRepository cards,
    IDebtEntryRepository entries,
    IExtraPaymentRepository payments,
    IRevolvingBillRepository bills,
    ICardPurchaseRepository purchases,
    IBillRepository expenseBills,
    IExpenseRepository expenses,
    IExpenseCategoryRepository categories,
    TimeProvider timeProvider)
{
    /// <summary>
    /// Loads every revolving card. Bills are calculated up to <paramref name="horizon"/>, at least up to
    /// next month. <paramref name="withoutExtraPaymentId"/> leaves one extra payment out, to check an edit.
    /// </summary>
    public async Task<RevolvingBook> LoadAsync(
        CancellationToken cancellationToken, YearMonth? horizon = null, Guid? withoutExtraPaymentId = null)
    {
        var today = timeProvider.Today();
        var next = YearMonth.FromDate(today).Next();
        var allCards = await cards.ListAsync(cancellationToken);
        var allCategories = await categories.ListAsync(cancellationToken);
        var charges = ChargesOf(
            allCards,
            allCategories,
            await expenseBills.ListAsync(cancellationToken),
            await expenses.ListAsync(cancellationToken),
            await purchases.ListAsync(null, cancellationToken));
        return new RevolvingBook(
            today,
            horizon is { } h && h > next ? h : next,
            allCards,
            await entries.ListAsync(null, cancellationToken),
            charges,
            (await payments.ListAsync(null, cancellationToken)).Where(p => p.Id != withoutExtraPaymentId).ToList(),
            await bills.ListAsync(null, cancellationToken),
            allCategories);
    }

    /// <summary>
    /// Bills, expenses and purchases paid with a revolving card, and the credits of reversed purchases
    /// (revolving FR-6). An expense without a description shows its category.
    /// </summary>
    public static IReadOnlyList<RevolvingChargeSource> ChargesOf(
        IReadOnlyList<Card> cards,
        IReadOnlyList<ExpenseCategory> categories,
        IEnumerable<Bill> bills,
        IEnumerable<Expense> expenses,
        IEnumerable<CardPurchase> purchases)
    {
        var revolving = cards.Where(c => c.IsRevolving).Select(c => c.Id).ToHashSet();
        var categoryNames = categories.ToDictionary(c => c.Id, c => c.Name);
        var fromBills = bills
            .Where(b => b.IsPaid && b.PaidFromId is { } id && revolving.Contains(id))
            .Select(b => new RevolvingChargeSource(
                new RevolvingCharge(b.Id, b.PaidFromId!.Value, RevolvingChargeKind.Bill, b.PaidDate!.Value, b.Amount), b.Name, b.CategoryId));
        var fromExpenses = expenses
            .Where(e => revolving.Contains(e.PaidFromId))
            .Select(e => new RevolvingChargeSource(
                new RevolvingCharge(e.Id, e.PaidFromId, RevolvingChargeKind.Expense, e.Date, e.Amount),
                e.Description ?? categoryNames.GetValueOrDefault(e.CategoryId, "Expense"),
                e.CategoryId));
        var fromPurchases = purchases
            .Where(p => revolving.Contains(p.CardId))
            .SelectMany(p =>
            {
                var charge = new RevolvingChargeSource(
                    new RevolvingCharge(p.Id, p.CardId, RevolvingChargeKind.Purchase, p.PurchaseDate, p.Amount), p.Description, p.CategoryId);
                // A purchase paid in full has one charge; its reversal is credited on the reversal date.
                var reversedOn = p.ReversedOn ?? p.ReversalOf(1)?.ReversedOn;
                return reversedOn is { } date
                    ? [charge, new RevolvingChargeSource(
                        new RevolvingCharge(p.Id, p.CardId, RevolvingChargeKind.PurchaseCredit, date, -p.Amount), p.Description, p.CategoryId)]
                    : new[] { charge };
            });
        return fromBills.Concat(fromExpenses).Concat(fromPurchases).ToList();
    }

    /// <summary>
    /// Stores the monthly bills paid automatically, so they stay paid when the card's paying account or
    /// its connection changes.
    /// </summary>
    public static async Task KeepAutomaticPaymentsAsync(
        IRevolvingBillRepository bills, RevolvingDebtCalculator calculator, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(bills);
        ArgumentNullException.ThrowIfNull(calculator);
        foreach (var bill in calculator.AutomaticallyPaid.ToList())
        {
            var stored = await bills.GetAsync(bill.CardId, bill.Month, cancellationToken);
            if (stored is null)
            {
                stored = RevolvingBill.Create(bill.CardId, bill.Month, now);
                bills.Add(stored);
            }
            stored.MarkPaid(bill.PaidDate!.Value, bill.AccountId!.Value, bill.PaidAmount!.Value, bill.BillDate, now);
        }
    }
}
