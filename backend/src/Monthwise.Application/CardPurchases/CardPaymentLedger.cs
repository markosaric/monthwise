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

namespace Monthwise.Application.CardPurchases;

/// <summary>
/// A bill or an expense paid with a credit card, with what the drill-down shows of it (bills and
/// expenses FR-6, FR-7).
/// </summary>
public sealed record CardChargeSource(CardCharge Charge, string Description, Guid CategoryId);

/// <summary>
/// All cards with their purchases, charges (bills and expenses paid with them) and calculated card
/// payments on one day, and the names to show them. The card equivalent of <see cref="LoanBook"/>.
/// </summary>
public sealed class CardPaymentBook
{
    private readonly Dictionary<Guid, Card> _cards;
    private readonly Dictionary<Guid, CardPaymentSchedule> _schedules;
    private readonly Dictionary<Guid, ExpenseCategory> _categories;
    private readonly Dictionary<Guid, CardChargeSource> _charges;

    internal CardPaymentBook(
        DateOnly today,
        IReadOnlyList<Card> cards,
        IReadOnlyList<CardPurchase> purchases,
        IReadOnlyList<CardPayment> stored,
        IReadOnlyList<ExpenseCategory> categories,
        IReadOnlyList<CardChargeSource> charges)
    {
        Today = today;
        _cards = cards.ToDictionary(c => c.Id);
        _categories = categories.ToDictionary(c => c.Id);
        _charges = charges.ToDictionary(c => c.Charge.SourceId);
        var purchasesByCard = purchases.ToLookup(p => p.CardId);
        var storedByCard = stored.ToLookup(p => p.CardId);
        var chargesByCard = charges.Select(c => c.Charge).ToLookup(c => c.CardId);
        _schedules = cards
            .Where(c => c.AllowsInstallments || purchasesByCard[c.Id].Any())
            .ToDictionary(c => c.Id, c => new CardPaymentSchedule(
                c, purchasesByCard[c.Id], storedByCard[c.Id], ChargesStopOn(c), chargesByCard[c.Id]));
    }

    public DateOnly Today { get; }

    /// <summary>Cards that allow installments or have purchases.</summary>
    public IReadOnlyCollection<CardPaymentSchedule> Schedules => _schedules.Values;

    public CardPaymentSchedule? Schedule(Guid cardId) => _schedules.GetValueOrDefault(cardId);

    public Card? Card(Guid cardId) => _cards.GetValueOrDefault(cardId);

    public IEnumerable<CardPurchase> Purchases => _schedules.Values.SelectMany(s => s.Purchases);

    public CardPurchase? Purchase(Guid id) => Purchases.FirstOrDefault(p => p.Id == id);

    /// <summary>All card payments of all cards.</summary>
    public IEnumerable<ScheduledCardPayment> Payments() => _schedules.Values.SelectMany(s => s.Payments(Today));

    /// <summary>Card payments shown between the dates (inclusive), by paid date or payment date.</summary>
    public IEnumerable<ScheduledCardPayment> PaymentsBetween(DateOnly from, DateOnly to) =>
        Payments().Where(p => p.MonthDate >= from && p.MonthDate <= to);

    public NamedReference CardReference(Guid id) => new(id, _cards.TryGetValue(id, out var card) ? card.Name : "Unknown");

    public NamedReference? Category(Guid? id) =>
        id is { } categoryId ? new NamedReference(categoryId, _categories.TryGetValue(categoryId, out var c) ? c.Name : "Unknown") : null;

    public CardPaymentResponse ToResponse(ScheduledCardPayment payment)
    {
        ArgumentNullException.ThrowIfNull(payment);
        return new CardPaymentResponse(
            payment.CardId,
            CardReference(payment.CardId).Name,
            payment.Month.ToString(),
            payment.PeriodFrom,
            payment.StatementDate,
            payment.PaymentDate,
            payment.Amount.Cents,
            payment.LineCount,
            payment.IsPaid,
            payment.PaidDate,
            payment.AccountId is { } accountId ? CardReference(accountId) : null,
            payment.PaysFromAccountDirectly,
            payment.IsOverdue(Today),
            payment.NeedsAccount);
    }

    /// <summary>Card payments in list order: by paid or payment date, then card name.</summary>
    public IReadOnlyList<CardPaymentResponse> ToSortedResponses(IEnumerable<ScheduledCardPayment> payments) =>
        payments
            .Select(ToResponse)
            .OrderBy(p => p.PaidDate ?? p.PaymentDate)
            .ThenBy(p => p.CardName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    public CardPurchaseResponse ToResponse(CardPurchase purchase)
    {
        ArgumentNullException.ThrowIfNull(purchase);
        var schedule = _schedules[purchase.CardId];
        var summary = schedule.Summary(purchase, Today);
        return new CardPurchaseResponse(
            purchase.Id,
            CardReference(purchase.CardId),
            purchase.Description,
            purchase.PurchaseDate,
            purchase.Amount.Cents,
            purchase.InInstallments,
            purchase.InstallmentCount,
            purchase.PostponeFirst,
            Category(purchase.CategoryId),
            purchase.ReversedOn,
            purchase.ReversalReason,
            summary.InstallmentsCharged,
            summary.InstallmentsLeft,
            summary.ChargedAmount.Cents,
            summary.RemainingAmount.Cents,
            schedule.HasChargedInstallments(purchase, Today));
    }

    /// <summary>Purchases newest first.</summary>
    public IReadOnlyList<CardPurchaseResponse> ToSortedResponses(IEnumerable<CardPurchase> purchases) =>
        purchases
            .OrderByDescending(p => p.PurchaseDate)
            .ThenByDescending(p => p.CreatedAt)
            .Select(ToResponse)
            .ToList();

    public CardPurchaseDetailsResponse Details(CardPurchase purchase)
    {
        ArgumentNullException.ThrowIfNull(purchase);
        var installments = _schedules[purchase.CardId].InstallmentsOf(purchase, Today)
            .Select(i => new InstallmentResponse(
                i.Installment.Number,
                i.Installment.Total,
                i.Installment.Amount.Cents,
                i.Installment.PaymentMonth.ToString(),
                i.StatementDate,
                i.PaymentDate,
                i.IsCharged,
                i.Reversal?.ReversedOn,
                i.Reversal?.Reason,
                i.Reversal?.WholePurchase ?? false,
                i.CreditMonth?.ToString()))
            .ToList();
        return new CardPurchaseDetailsResponse(ToResponse(purchase), installments);
    }

    /// <summary>Whether a bill or expense paid with <paramref name="cardId"/> on <paramref name="date"/> is in a card payment that is already due (FR-6).</summary>
    public bool IsCharged(Guid cardId, DateOnly date) =>
        _schedules.TryGetValue(cardId, out var schedule) && schedule.IsChargedBy(date, Today);

    /// <summary>
    /// The drill-down of one card payment (FR-8): its lines, purchases, bills and expenses first by date,
    /// credits last.
    /// </summary>
    public CardPaymentDetailsResponse PaymentDetails(ScheduledCardPayment payment)
    {
        ArgumentNullException.ThrowIfNull(payment);
        var schedule = _schedules[payment.CardId];
        var purchases = schedule.Purchases.ToDictionary(p => p.Id);
        var lines = schedule.LinesOf(payment.Month)
            .Select(l =>
            {
                if (_charges.TryGetValue(l.SourceId, out var charge))
                {
                    return new CardPaymentLineResponse(
                        l.SourceId,
                        charge.Description,
                        charge.Charge.Date,
                        l.Kind,
                        l.InstallmentNumber,
                        l.InstallmentTotal,
                        Category(charge.CategoryId),
                        l.Amount.Cents);
                }
                var purchase = purchases[l.SourceId];
                return new CardPaymentLineResponse(
                    purchase.Id,
                    purchase.Description,
                    purchase.PurchaseDate,
                    l.Kind,
                    l.InstallmentNumber,
                    l.InstallmentTotal,
                    Category(purchase.CategoryId),
                    l.Amount.Cents);
            })
            .OrderBy(l => l.Kind == CardPaymentLineKind.Credit)
            .ThenBy(l => l.Date)
            .ThenBy(l => l.Description, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        return new CardPaymentDetailsResponse(ToResponse(payment), lines);
    }

    public InstallmentCardResponse ToResponse(CardPaymentSchedule schedule)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        var card = schedule.Card;
        var billing = card.BillingFor(YearMonth.FromDate(Today).Next());
        var next = schedule.Payments(Today).Where(p => !p.IsPaid).OrderBy(p => p.PaymentDate).FirstOrDefault();
        var summaries = schedule.Purchases.Select(p => schedule.Summary(p, Today)).ToList();
        return new InstallmentCardResponse(
            card.Id,
            card.Name,
            card.CardNumberMasked,
            card.Type,
            card.StatusOn(Today),
            card.CanBeUsedOn(Today),
            card.NeedsBillingDetails,
            billing?.StatementDay,
            billing?.PaymentDay,
            card.PayingAccountId is { } accountId ? CardReference(accountId) : null,
            card.PaysFromAccountDirectly,
            card.MaxInstallments,
            schedule.NeedsAccount,
            next is null ? null : ToResponse(next),
            Money.Sum(summaries.Select(s => s.RemainingAmount)).Cents,
            summaries.Count(s => s.InstallmentsLeft > 0));
    }

    /// <summary>Expected, paid and all card payments (FR-8).</summary>
    public static LoanPaymentTotalsResponse Totals(IEnumerable<ScheduledCardPayment> payments)
    {
        var list = payments.ToList();
        var expected = Money.Sum(list.Where(p => !p.IsPaid).Select(p => p.Amount));
        var paid = Money.Sum(list.Where(p => p.IsPaid).Select(p => p.Amount));
        return new LoanPaymentTotalsResponse(expected.Cents, paid.Cents, (expected + paid).Cents);
    }

    /// <summary>The first day card payments can no longer be charged from the card's paying account (FR-9).</summary>
    private DateOnly? ChargesStopOn(Card card) =>
        card.PayingAccountId is { } accountId
            ? LoanAccount.ChargesStopOn(_cards.GetValueOrDefault(accountId), Today)
            : null;
}

/// <summary>Loads the <see cref="CardPaymentBook"/> for today.</summary>
public sealed class CardPaymentLedger(
    ICardRepository cards,
    ICardPurchaseRepository purchases,
    ICardPaymentRepository payments,
    IExpenseCategoryRepository categories,
    IBillRepository bills,
    IExpenseRepository expenses,
    TimeProvider timeProvider)
{
    public async Task<CardPaymentBook> LoadAsync(CancellationToken cancellationToken)
    {
        var allCards = await cards.ListAsync(cancellationToken);
        var allCategories = await categories.ListAsync(cancellationToken);
        var charges = ChargesOf(
            allCards, allCategories, await bills.ListAsync(cancellationToken), await expenses.ListAsync(cancellationToken));
        return new(
            timeProvider.Today(),
            allCards,
            await purchases.ListAsync(null, cancellationToken),
            await payments.ListAsync(null, cancellationToken),
            allCategories,
            charges);
    }

    /// <summary>
    /// Bills paid with a credit card and expenses paid with one: lines of that card's card payments
    /// (bills and expenses FR-6, FR-7). An expense without a description shows its category.
    /// </summary>
    public static IReadOnlyList<CardChargeSource> ChargesOf(
        IReadOnlyList<Card> cards, IReadOnlyList<ExpenseCategory> categories, IEnumerable<Bill> bills, IEnumerable<Expense> expenses)
    {
        var creditCards = cards.Where(PaidFromRules.IsCreditCard).Select(c => c.Id).ToHashSet();
        var categoryNames = categories.ToDictionary(c => c.Id, c => c.Name);
        var fromBills = bills
            .Where(b => b.IsPaid && b.PaidFromId is { } id && creditCards.Contains(id))
            .Select(b => new CardChargeSource(
                new CardCharge(b.Id, b.PaidFromId!.Value, CardChargeKind.Bill, b.PaidDate!.Value, b.Amount), b.Name, b.CategoryId));
        var fromExpenses = expenses
            .Where(e => creditCards.Contains(e.PaidFromId))
            .Select(e => new CardChargeSource(
                new CardCharge(e.Id, e.PaidFromId, CardChargeKind.Expense, e.Date, e.Amount),
                e.Description ?? categoryNames.GetValueOrDefault(e.CategoryId, "Expense"),
                e.CategoryId));
        return fromBills.Concat(fromExpenses).ToList();
    }

    /// <summary>
    /// Cards with ongoing transactions (card settings, Appendix 2): cards with installments left, and
    /// the paying accounts of connected cards with expected card payments. They stay on the Dashboard
    /// and in the default Settings list while blocked or expired.
    /// </summary>
    public async Task<IReadOnlySet<Guid>> CardsWithOngoingTransactionsAsync(CancellationToken cancellationToken)
    {
        var book = await LoadAsync(cancellationToken);
        var withInstallments = book.Schedules.Where(s => s.HasInstallmentsLeft(book.Today)).Select(s => s.Card.Id);
        var payingAccounts = book.Schedules
            .Where(s => s.Card.PaysFromAccountDirectly && s.Card.PayingAccountId is not null)
            .Where(s => s.Payments(book.Today).Any(p => !p.IsPaid))
            .Select(s => s.Card.PayingAccountId!.Value);
        return withInstallments.Concat(payingAccounts).ToHashSet();
    }

    /// <summary>
    /// Stores the card payments paid automatically, so they stay paid when the card's paying account or
    /// its connection changes.
    /// </summary>
    public static async Task KeepAutomaticPaymentsAsync(
        ICardPaymentRepository payments, CardPaymentSchedule schedule, DateOnly today, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payments);
        ArgumentNullException.ThrowIfNull(schedule);
        foreach (var payment in schedule.AutomaticallyPaid(today).ToList())
        {
            var stored = await payments.GetAsync(payment.CardId, payment.Month, cancellationToken);
            if (stored is null)
            {
                stored = CardPayment.Create(payment.CardId, payment.Month, now);
                payments.Add(stored);
            }
            stored.MarkPaid(payment.PaymentDate, payment.AccountId!.Value, payment.StatementDate, now);
        }
    }
}
