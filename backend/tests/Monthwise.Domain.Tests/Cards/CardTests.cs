using Monthwise.Domain.Cards;
using Monthwise.Domain.Common;

namespace Monthwise.Domain.Tests.Cards;

public class CardTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 4, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 10, 4);
    private static readonly CardNumber Visa = CardNumber.Parse("4111111111111111");
    private static readonly Iban CroatianIban = Iban.Parse("HR1210010051863000160");

    private static CardDetails Details(
        string name = "Family Visa",
        CardType type = CardType.Credit,
        bool isPrimary = false,
        YearMonth? validFrom = null,
        YearMonth? validThrough = null,
        Money? debtLimit = null,
        bool noValidFrom = false,
        bool allowsInstallments = false,
        CardBillingDetails? billing = null,
        bool noBilling = false) =>
        new(name, Guid.NewGuid(), Guid.NewGuid(), noValidFrom ? null : validFrom ?? new YearMonth(2025, 1),
            validThrough ?? new YearMonth(2029, 12), type, isPrimary, debtLimit, allowsInstallments,
            noBilling ? null : billing ?? Billing());

    private static readonly Guid PayingAccount = Guid.NewGuid();

    private static CardBillingDetails Billing(int statementDay = 5, int paymentDay = 20, int maxInstallments = 36) =>
        new(statementDay, paymentDay, PayingAccount, PaysFromAccountDirectly: false, maxInstallments);

    private static Card NewCard(CardDetails? details = null, CardNumber? number = null, Iban? iban = null) =>
        Card.Create(details ?? Details(), number ?? Visa, iban, Now).Value;

    private static Card PrimaryDebit() => NewCard(Details(type: CardType.Debit, isPrimary: true));

    [Fact]
    public void Create_stores_details_and_masks_the_numbers()
    {
        var card = Card.Create(Details(name: "  Family Visa  ", debtLimit: Money.FromCents(150_000)), Visa, CroatianIban, Now).Value;

        Assert.Equal("Family Visa", card.Name);
        Assert.Equal("1111", card.CardNumberLast4);
        Assert.Equal("•••• 1111", card.CardNumberMasked);
        Assert.Equal("HR•• … 0160", card.IbanMasked);
        Assert.Equal(Visa, card.SensitiveData.CardNumber);
        Assert.Equal(card.Id, card.SensitiveData.CardId);
        Assert.Equal(Money.FromCents(150_000), card.DebtLimit);
    }

    [Fact]
    public void Numbers_and_valid_from_are_optional()
    {
        Assert.True(Card.Create(Details(), Visa, null, Now).IsSuccess);
        Assert.True(Card.Create(Details(), null, CroatianIban, Now).IsSuccess);

        var bare = Card.Create(Details(noValidFrom: true), null, null, Now).Value;

        Assert.Null(bare.ValidFrom);
        Assert.Null(bare.CardNumberMasked);
        Assert.Null(bare.IbanMasked);
        Assert.Null(bare.SensitiveData.CardNumber);
        Assert.Null(bare.SensitiveData.Iban);
    }

    [Fact]
    public void A_card_without_valid_from_is_never_not_yet_valid()
    {
        var card = NewCard(Details(noValidFrom: true, validThrough: new YearMonth(2029, 12)));

        Assert.Equal(CardStatus.Active, card.StatusOn(new DateOnly(2000, 1, 1)));
        Assert.Equal(CardStatus.ExpiringSoon, card.StatusOn(new DateOnly(2029, 11, 15)));
        Assert.Equal(CardStatus.Expired, card.StatusOn(new DateOnly(2030, 1, 1)));
    }

    [Fact]
    public void Update_can_remove_numbers_and_valid_from()
    {
        var card = NewCard(number: Visa, iban: CroatianIban);

        var result = card.Update(Details(noValidFrom: true), null, null, Today, Now);

        Assert.True(result.IsSuccess);
        Assert.Null(card.ValidFrom);
        Assert.Null(card.CardNumberLast4);
        Assert.Null(card.IbanLast4);
    }

    [Fact]
    public void Monthly_installments_follow_the_check_box_for_debit_and_the_type_for_the_rest()
    {
        Assert.False(NewCard(Details(type: CardType.Debit)).AllowsInstallments);
        Assert.True(NewCard(Details(type: CardType.Debit, allowsInstallments: true)).AllowsInstallments);
        Assert.True(NewCard(Details(type: CardType.Credit)).AllowsInstallments);
        Assert.True(NewCard(Details(type: CardType.Charge)).AllowsInstallments);
        Assert.False(NewCard(Details(type: CardType.Prepaid)).AllowsInstallments);
    }

    [Fact]
    public void The_installments_check_box_is_only_for_debit_cards()
    {
        var result = Card.Create(Details(type: CardType.Credit, allowsInstallments: true), Visa, null, Now);

        Assert.Equal(CardErrors.InstallmentsNotDebit, Assert.Single(result.Errors));
    }

    [Fact]
    public void Update_changes_the_installments_check_box()
    {
        var card = NewCard(Details(type: CardType.Debit));

        Assert.True(card.Update(Details(type: CardType.Debit, allowsInstallments: true), Visa, null, Today, Now).IsSuccess);

        Assert.True(card.AllowsInstallmentsFlag);
    }

    [Fact]
    public void Create_reports_every_broken_rule_with_its_field()
    {
        var details = new CardDetails(
            "", Guid.Empty, Guid.Empty, new YearMonth(2026, 5), new YearMonth(2026, 4),
            CardType.Credit, IsPrimary: true, Money.FromCents(-1), AllowsInstallments: true);

        var result = Card.Create(details, null, null, Now);

        Assert.True(result.IsFailure);
        Assert.Equal(
            ["name", "issuerId", "bankId", "validThrough", "debtLimitCents", "isPrimary", "allowsInstallments", "statementDay"],
            result.Errors.Select(e => e.Field));
    }

    [Fact]
    public void Name_longer_than_50_characters_is_rejected()
    {
        var result = Card.Create(Details(name: new string('x', 51)), Visa, null, Now);

        Assert.Equal(CardErrors.NameTooLong, Assert.Single(result.Errors));
    }

    [Theory]
    [InlineData(CardType.Debit)]
    [InlineData(CardType.Charge)]
    [InlineData(CardType.Credit)]
    [InlineData(CardType.Prepaid)]
    public void Debt_limit_is_optional_for_every_type(CardType type)
    {
        Assert.True(Card.Create(Details(type: type, debtLimit: null), Visa, null, Now).IsSuccess);
        Assert.True(Card.Create(Details(type: type, debtLimit: Money.Zero), Visa, null, Now).IsSuccess);
    }

    [Fact]
    public void Valid_from_and_through_may_be_the_same_month()
    {
        var month = new YearMonth(2026, 10);

        Assert.True(Card.Create(Details(validFrom: month, validThrough: month), Visa, null, Now).IsSuccess);
    }

    [Fact]
    public void Several_debit_cards_can_be_primary()
    {
        Assert.True(PrimaryDebit().IsPrimary);
        Assert.True(PrimaryDebit().IsPrimary);
    }

    [Fact]
    public void Update_changes_details_and_numbers()
    {
        var card = NewCard();

        var result = card.Update(Details(name: "Renamed", type: CardType.Debit), null, CroatianIban, Today, Now.AddDays(1));

        Assert.True(result.IsSuccess);
        Assert.Equal("Renamed", card.Name);
        Assert.Null(card.CardNumberLast4);
        Assert.Null(card.SensitiveData.CardNumber);
        Assert.Equal(CroatianIban, card.SensitiveData.Iban);
        Assert.Equal(Now.AddDays(1), card.UpdatedAt);
    }

    [Fact]
    public void Blocked_expired_and_archived_cards_cannot_be_edited()
    {
        var blocked = NewCard();
        blocked.Block(Today, null, Now);
        var expired = NewCard();
        expired.MarkExpired(Today, null, null, Now);
        var archived = NewCard();
        archived.Archive(Today, Now);

        foreach (var card in new[] { blocked, expired, archived })
        {
            var result = card.Update(Details(), Visa, null, Today, Now);
            Assert.Equal(CardErrors.ReadOnly, Assert.Single(result.Errors));
        }
    }

    [Fact]
    public void Block_records_date_and_reason_and_unblock_restores_the_date_based_status()
    {
        var card = NewCard();

        Assert.True(card.Block(Today, "  lost  ", Now).IsSuccess);
        Assert.Equal(CardStatus.Blocked, card.StatusOn(Today));
        Assert.Equal(Today, card.BlockedOn);
        Assert.Equal("lost", card.BlockReason);
        Assert.False(card.CanBeUsedOn(Today));

        Assert.True(card.Unblock(Now).IsSuccess);
        Assert.Equal(CardStatus.Active, card.StatusOn(Today));
        Assert.Null(card.BlockReason);
    }

    [Fact]
    public void Block_twice_or_unblock_an_unblocked_card_is_a_conflict()
    {
        var card = NewCard();

        Assert.Equal(CardErrors.NotBlocked, Assert.Single(card.Unblock(Now).Errors));
        card.Block(Today, null, Now);
        Assert.Equal(CardErrors.AlreadyBlocked, Assert.Single(card.Block(Today, null, Now).Errors));
    }

    [Fact]
    public void Reason_longer_than_200_characters_is_rejected()
    {
        var card = NewCard();

        Assert.Equal(CardErrors.ReasonTooLong, Assert.Single(card.Block(Today, new string('x', 201), Now).Errors));
        Assert.Equal(CardErrors.ReasonTooLong, Assert.Single(card.MarkExpired(Today, new string('x', 201), null, Now).Errors));
    }

    [Fact]
    public void Expiry_is_final_and_links_the_replacement()
    {
        var card = NewCard();
        var replacement = NewCard(Details(name: "New Visa"));

        Assert.True(card.MarkExpired(Today, "replaced by bank", replacement, Now).IsSuccess);

        Assert.Equal(CardStatus.Expired, card.StatusOn(Today));
        Assert.Equal(Today, card.ExpiredOn);
        Assert.Equal(replacement.Id, card.ReplacedByCardId);
        Assert.Equal(CardErrors.AlreadyExpired, Assert.Single(card.MarkExpired(Today, null, null, Now).Errors));
    }

    [Fact]
    public void Replacement_must_be_another_usable_card()
    {
        var card = NewCard();
        var archived = NewCard(Details(name: "Old"));
        archived.Archive(Today, Now);
        var notYetValid = NewCard(Details(name: "Next", validFrom: new YearMonth(2026, 11)));

        Assert.Equal(CardErrors.ReplacementIsSameCard, Assert.Single(card.MarkExpired(Today, null, card, Now).Errors));
        Assert.Equal(CardErrors.ReplacementUnusable, Assert.Single(card.MarkExpired(Today, null, archived, Now).Errors));
        Assert.True(card.MarkExpired(Today, null, notYetValid, Now).IsSuccess);
    }

    [Fact]
    public void A_replacement_added_after_expiry_can_be_linked()
    {
        var card = NewCard();
        card.MarkExpired(Today, null, null, Now);
        var replacement = NewCard(Details(name: "New Visa"));

        Assert.True(card.LinkReplacement(replacement, Today, Now).IsSuccess);
        Assert.Equal(replacement.Id, card.ReplacedByCardId);
    }

    [Fact]
    public void Archive_is_final()
    {
        var card = NewCard();

        Assert.True(card.Archive(Today, Now).IsSuccess);

        Assert.Equal(CardStatus.Archived, card.StatusOn(Today));
        Assert.Equal(CardErrors.AlreadyArchived, Assert.Single(card.Archive(Today, Now).Errors));
        Assert.Equal(CardErrors.AlreadyArchived, Assert.Single(card.Unblock(Now).Errors));
        Assert.Equal(CardErrors.AlreadyArchived, Assert.Single(card.Block(Today, null, Now).Errors));
    }

    [Fact]
    public void Block_expire_and_archive_remove_the_primary_mark()
    {
        var blocked = PrimaryDebit();
        blocked.Block(Today, null, Now);
        var expired = PrimaryDebit();
        expired.MarkExpired(Today, null, null, Now);
        var archived = PrimaryDebit();
        archived.Archive(Today, Now);

        Assert.False(blocked.IsPrimary);
        Assert.False(expired.IsPrimary);
        Assert.False(archived.IsPrimary);
    }

    [Theory]
    [InlineData("2026-12-27", CardStatus.Active)] // + 2 months = 2027-02-27, before the last valid day
    [InlineData("2026-12-28", CardStatus.ExpiringSoon)] // + 2 months = 2027-02-28, the last valid day
    [InlineData("2027-02-28", CardStatus.ExpiringSoon)] // last valid day
    [InlineData("2027-03-01", CardStatus.Expired)]
    [InlineData("2025-12-31", CardStatus.NotYetValid)]
    [InlineData("2026-01-01", CardStatus.Active)] // first valid day
    public void Status_follows_the_validity_dates(string today, CardStatus expected)
    {
        var card = NewCard(Details(validFrom: new YearMonth(2026, 1), validThrough: new YearMonth(2027, 2)));

        Assert.Equal(expected, card.StatusOn(DateOnly.Parse(today, System.Globalization.CultureInfo.InvariantCulture)));
    }

    [Fact]
    public void User_set_statuses_take_priority_archived_then_blocked_then_expired()
    {
        var card = NewCard(Details(validThrough: new YearMonth(2026, 10)));
        var afterValidity = new DateOnly(2026, 11, 15);

        Assert.Equal(CardStatus.Expired, card.StatusOn(afterValidity));
        card.Block(Today, null, Now);
        Assert.Equal(CardStatus.Blocked, card.StatusOn(afterValidity));
        card.Archive(Today, Now);
        Assert.Equal(CardStatus.Archived, card.StatusOn(afterValidity));
    }

    [Fact]
    public void Manual_expiry_overrides_a_valid_through_in_the_future()
    {
        var card = NewCard(Details(validThrough: new YearMonth(2030, 1)));

        card.MarkExpired(Today, null, null, Now);

        Assert.Equal(CardStatus.Expired, card.StatusOn(Today));
    }

    [Fact]
    public void Only_active_and_expiring_cards_can_be_used()
    {
        var active = NewCard();
        var expiring = NewCard(Details(validThrough: new YearMonth(2026, 11)));
        var notYetValid = NewCard(Details(validFrom: new YearMonth(2027, 1)));
        var expired = NewCard(Details(validThrough: new YearMonth(2026, 9)));

        Assert.True(active.CanBeUsedOn(Today));
        Assert.True(expiring.CanBeUsedOn(Today));
        Assert.False(notYetValid.CanBeUsedOn(Today));
        Assert.False(expired.CanBeUsedOn(Today));
    }

    [Fact]
    public void Default_list_hides_archived_and_expired_cards_without_ongoing_transactions()
    {
        var active = NewCard();
        var blocked = NewCard();
        blocked.Block(Today, null, Now);
        var expired = NewCard(Details(validThrough: new YearMonth(2026, 9)));
        var archived = NewCard();
        archived.Archive(Today, Now);

        Assert.True(active.IsListedByDefault(Today, hasOngoingTransactions: false));
        Assert.True(blocked.IsListedByDefault(Today, hasOngoingTransactions: false));
        Assert.False(expired.IsListedByDefault(Today, hasOngoingTransactions: false));
        Assert.True(expired.IsListedByDefault(Today, hasOngoingTransactions: true));
        Assert.False(archived.IsListedByDefault(Today, hasOngoingTransactions: true));
    }

    [Fact]
    public void Dashboard_shows_valid_cards_and_blocked_or_expired_ones_only_with_ongoing_transactions()
    {
        var active = NewCard();
        var expiring = NewCard(Details(validThrough: new YearMonth(2026, 11)));
        var notYetValid = NewCard(Details(validFrom: new YearMonth(2027, 1)));
        var blocked = NewCard();
        blocked.Block(Today, null, Now);
        var expired = NewCard();
        expired.MarkExpired(Today, null, null, Now);
        var archived = NewCard();
        archived.Archive(Today, Now);

        Assert.True(active.IsShownOnDashboard(Today, hasOngoingTransactions: false));
        Assert.True(expiring.IsShownOnDashboard(Today, hasOngoingTransactions: false));
        Assert.False(notYetValid.IsShownOnDashboard(Today, hasOngoingTransactions: false));
        Assert.False(blocked.IsShownOnDashboard(Today, hasOngoingTransactions: false));
        Assert.True(blocked.IsShownOnDashboard(Today, hasOngoingTransactions: true));
        Assert.False(expired.IsShownOnDashboard(Today, hasOngoingTransactions: false));
        Assert.True(expired.IsShownOnDashboard(Today, hasOngoingTransactions: true));
        Assert.False(archived.IsShownOnDashboard(Today, hasOngoingTransactions: true));
    }
}
