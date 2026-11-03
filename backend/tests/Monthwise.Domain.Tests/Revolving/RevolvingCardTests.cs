using Monthwise.Domain.Cards;
using Monthwise.Domain.Common;
using Monthwise.Domain.Expenses;
using static Monthwise.Domain.Tests.Revolving.RevolvingTestData;

namespace Monthwise.Domain.Tests.Revolving;

public class RevolvingCardTests
{
    [Fact]
    public void A_revolving_card_needs_a_debt_limit_and_its_terms_and_cannot_be_primary()
    {
        var noLimit = Card.Create(Details(Terms(), debtLimitCents: null), null, null, Now);
        var noTerms = Card.Create(Details(null), null, null, Now);
        var primary = Card.Create(Details(Terms(), isPrimary: true), null, null, Now);

        Assert.Contains(CardErrors.DebtLimitRequired, noLimit.Errors);
        Assert.Contains(CardErrors.RevolvingRequired, noTerms.Errors);
        Assert.Contains(CardErrors.PrimaryNotDebit, primary.Errors);
    }

    [Theory]
    [InlineData(0, 1_327, 1_275, null, 5, 20, "card.minPaymentPercent.invalid")]
    [InlineData(10_001, 1_327, 1_275, null, 5, 20, "card.minPaymentPercent.invalid")]
    [InlineData(833, -1, 1_275, null, 5, 20, "card.minPaymentAmount.negative")]
    [InlineData(833, 1_327, -1, null, 5, 20, "card.interestRate.negative")]
    [InlineData(833, 1_327, 1_275, 900, 5, 20, "card.minPaymentInterest.invalid")]
    [InlineData(833, 1_327, 1_275, null, 0, 20, "card.billDay.invalid")]
    [InlineData(833, 1_327, 1_275, null, 5, 32, "card.dueDay.invalid")]
    public void Invalid_terms_are_refused(int percent, long amount, int interest, int? interestPart, int billDay, int dueDay, string code)
    {
        var result = Card.Create(Details(Terms(percent, amount, interest, interestPart, billDay, dueDay)), null, null, Now);

        Assert.Contains(result.Errors, e => e.Code == code);
    }

    [Fact]
    public void A_revolving_card_is_not_an_account_and_what_it_pays_is_a_charge_on_it()
    {
        var card = RevolvingCard(Terms(interestPartBasisPoints: 500));

        Assert.False(card.IsAccount);
        Assert.False(card.AllowsInstallments);
        Assert.False(PaidFromRules.IsCreditCard(card));
        Assert.True(PaidFromRules.IsCardCharge(card));
        Assert.Null(PaidFromRules.Check(card, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 1), primaryOnly: false, unchanged: false));
        Assert.Equal(
            ExpenseErrors.CashWithdrawalNotPrimary,
            PaidFromRules.Check(card, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 1), primaryOnly: true, unchanged: false));
        Assert.Equal(500, card.RevolvingFor(new YearMonth(2026, 11))!.MinPaymentInterestBasisPoints);
    }

    [Fact]
    public void Changed_terms_apply_from_next_month()
    {
        var card = RevolvingCard();

        var updated = card.Update(Details(Terms(billDay: 10, dueDay: 25)), null, null, new DateOnly(2026, 10, 10), Now);

        Assert.True(updated.IsSuccess);
        var calendar = card.RevolvingDates()!;
        Assert.Equal(new DateOnly(2026, 10, 5), calendar.BillDate(new YearMonth(2026, 10)));
        Assert.Equal(new DateOnly(2026, 11, 10), calendar.BillDate(new YearMonth(2026, 11)));
        Assert.Equal(2, card.RevolvingTermVersions.Count);
    }

    [Fact]
    public void Due_dates_fall_in_the_next_month_when_not_later_than_the_bill_day_and_move_off_weekends()
    {
        var nextMonth = RevolvingCard(Terms(billDay: 25, dueDay: 10)).RevolvingDates()!;
        var saturday = RevolvingCard(Terms(billDay: 5, dueDay: 17)).RevolvingDates()!;
        var endOfMonth = RevolvingCard(Terms(billDay: 5, dueDay: 31)).RevolvingDates()!;
        var shortMonth = RevolvingCard(Terms(billDay: 31, dueDay: 10)).RevolvingDates()!;

        Assert.Equal(new DateOnly(2026, 11, 10), nextMonth.DueDate(new YearMonth(2026, 10)));
        Assert.Equal(new DateOnly(2026, 10, 19), saturday.DueDate(new YearMonth(2026, 10))); // 17 October 2026 is a Saturday
        Assert.Equal(new DateOnly(2026, 10, 31), endOfMonth.DueDate(new YearMonth(2026, 10))); // Monday would be in November
        Assert.Equal(new DateOnly(2027, 2, 28), shortMonth.BillDate(new YearMonth(2027, 2)));
    }

    [Fact]
    public void A_revolving_card_with_debt_cannot_be_archived_and_is_replaced_only_by_a_revolving_card()
    {
        var card = RevolvingCard();
        var debit = Card.Create(
            new CardDetails("Main", Guid.NewGuid(), Guid.NewGuid(), null, new YearMonth(2030, 12), CardType.Debit, true, null),
            null, null, Now).Value;
        var today = new DateOnly(2026, 10, 1);

        Assert.Equal(CardErrors.ArchiveWithDebt, card.Archive(today, Now, Money.FromCents(1)).Errors[0]);
        Assert.Equal(CardErrors.ReplacementNotRevolving, card.MarkExpired(today, null, debit, Now).Errors[0]);
        Assert.True(card.MarkExpired(today, null, RevolvingCard(), Now).IsSuccess);
        Assert.True(card.Archive(today, Now, Money.Zero).IsSuccess);
    }
}
