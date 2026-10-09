using Monthwise.Domain.CardPurchases;
using Monthwise.Domain.Cards;
using Monthwise.Domain.Common;
using Monthwise.Domain.Expenses;
using static Monthwise.Domain.Tests.CardPurchases.CardPurchaseTestData;

namespace Monthwise.Domain.Tests.Expenses;

public class ExpenseTests
{
    private static readonly Guid Groceries = Guid.NewGuid();
    private static readonly DateOnly Today = new(2026, 10, 9);

    private static ExpenseDetails Groceries40(Guid paidFrom) => new(Today, Money.FromCents(4_000), Groceries, paidFrom, "Konzum");

    private static Card Account(bool isPrimary = true, CardType type = CardType.Debit, bool allowsInstallments = false) =>
        Card.Create(
            new CardDetails(
                "Main account", Guid.NewGuid(), Guid.NewGuid(), null, new YearMonth(2030, 12), type,
                isPrimary, DebtLimit: null, allowsInstallments, allowsInstallments ? Billing() : null),
            null,
            null,
            Now).Value;

    [Fact]
    public void An_expense_needs_a_positive_amount_a_category_and_what_it_was_paid_with()
    {
        var result = Expense.Create(new ExpenseDetails(Today, Money.Zero, Guid.Empty, Guid.Empty, new string('x', 101)), Now);

        Assert.Equal(
            [ExpenseErrors.AmountNotPositive, ExpenseErrors.CategoryRequired, ExpenseErrors.PaidFromRequired, ExpenseErrors.ExpenseDescriptionTooLong],
            result.Errors);
    }

    [Fact]
    public void Cash_withdrawal_is_only_used_by_cash_withdrawals_and_stays_on_them()
    {
        var account = Guid.NewGuid();
        var asExpense = Expense.Create(Groceries40(account) with { CategoryId = ExpenseCategorySeedData.CashWithdrawalId }, Now);
        var withdrawal = Expense.CreateCashWithdrawal(Today, Money.FromCents(10_000), account, null, Now).Value;

        Assert.Equal(ExpenseErrors.CategoryCashWithdrawal, Assert.Single(asExpense.Errors));
        Assert.True(withdrawal.IsCashWithdrawal);
        Assert.True(withdrawal.Update(Groceries40(account), Now).IsSuccess);
        Assert.True(withdrawal.IsCashWithdrawal);
        Assert.Equal(4_000, withdrawal.Amount.Cents);
    }

    [Fact]
    public void Only_credit_and_charge_cards_are_credit_cards_and_debit_cards_are_always_accounts()
    {
        Assert.True(PaidFromRules.IsCreditCard(CreditCard()));
        Assert.False(PaidFromRules.IsCreditCard(Account(type: CardType.Debit, isPrimary: true, allowsInstallments: true)));
        Assert.False(PaidFromRules.IsCreditCard(Account(type: CardType.Prepaid, isPrimary: false)));
    }

    [Fact]
    public void A_cash_withdrawal_needs_a_primary_account()
    {
        Assert.Null(PaidFromRules.Check(Account(), Today, Today, primaryOnly: true, unchanged: false));
        Assert.Equal(
            ExpenseErrors.CashWithdrawalNotPrimary,
            PaidFromRules.Check(Account(isPrimary: false), Today, Today, primaryOnly: true, unchanged: false));
        Assert.Equal(ExpenseErrors.CashWithdrawalNotPrimary, PaidFromRules.Check(CreditCard(), Today, Today, primaryOnly: true, unchanged: false));
    }

    [Fact]
    public void A_blocked_account_can_no_longer_be_chosen_but_stays_on_entries_that_have_it()
    {
        var account = Account();
        account.Block(Today, null, Now);

        Assert.Equal(ExpenseErrors.PaidFromUnusable, PaidFromRules.Check(account, Today, Today, primaryOnly: false, unchanged: false));
        Assert.Null(PaidFromRules.Check(account, Today, Today, primaryOnly: false, unchanged: true));
        Assert.Equal(ExpenseErrors.PaidFromUnknown, PaidFromRules.Check(null, Today, Today, primaryOnly: false, unchanged: false));
    }

    [Fact]
    public void Cash_withdrawal_cannot_be_renamed_or_hidden()
    {
        var fixedCategory = ExpenseCategory.Predefined(ExpenseCategorySeedData.CashWithdrawalId, "Cash withdrawal", isFixed: true);
        var own = ExpenseCategory.Create("Pets", Now).Value;

        Assert.Equal(ExpenseErrors.CategoryFixed, Assert.Single(fixedCategory.Rename("Cash").Errors));
        Assert.Equal(ExpenseErrors.CategoryFixed, Assert.Single(fixedCategory.Hide(Today).Errors));
        Assert.True(own.Hide(Today).IsSuccess);
        Assert.True(own.IsHidden);
        Assert.Equal(ExpenseErrors.CategoryNameTooLong, Assert.Single(own.Rename(new string('c', 51)).Errors));
    }

    [Fact]
    public void Bills_and_expenses_paid_with_a_credit_card_are_lines_of_the_card_payment_of_their_billing_period()
    {
        var card = CreditCard();
        var purchase = NewPurchase(Purchase(card.Id, date: new DateOnly(2026, 10, 3), amountCents: 2_500, installments: null));
        var onStatementDate = new CardCharge(Guid.NewGuid(), card.Id, CardChargeKind.Bill, new DateOnly(2026, 10, 5), Money.FromCents(4_250));
        var dayAfter = new CardCharge(Guid.NewGuid(), card.Id, CardChargeKind.Expense, new DateOnly(2026, 10, 6), Money.FromCents(1_000));
        var otherCard = new CardCharge(Guid.NewGuid(), Guid.NewGuid(), CardChargeKind.Expense, new DateOnly(2026, 10, 6), Money.FromCents(9_999));

        var schedule = new CardPaymentSchedule(card, [purchase], [], charges: [onStatementDate, dayAfter, otherCard]);

        var october = schedule.Find(new YearMonth(2026, 10), new DateOnly(2026, 10, 1))!;
        Assert.Equal(2_500 + 4_250, october.Amount.Cents);
        Assert.Contains(schedule.LinesOf(new YearMonth(2026, 10)), l => l.Kind == CardPaymentLineKind.Bill && l.SourceId == onStatementDate.SourceId);
        var november = schedule.Find(new YearMonth(2026, 11), new DateOnly(2026, 10, 1))!;
        Assert.Equal(1_000, november.Amount.Cents);
        Assert.Equal(CardPaymentLineKind.Expense, Assert.Single(schedule.LinesOf(new YearMonth(2026, 11))).Kind);
    }

    [Fact]
    public void A_charge_is_charged_once_its_card_payment_date_has_passed()
    {
        var schedule = new CardPaymentSchedule(CreditCard(), [], []);

        Assert.False(schedule.IsChargedBy(new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 20)));
        Assert.True(schedule.IsChargedBy(new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 21)));
        Assert.False(schedule.IsChargedBy(new DateOnly(2026, 10, 6), new DateOnly(2026, 10, 21)));
    }
}
