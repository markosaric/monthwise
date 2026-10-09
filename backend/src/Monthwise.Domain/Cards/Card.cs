using Monthwise.Domain.Common;

namespace Monthwise.Domain.Cards;

/// <summary>
/// A payment card or bank account the user registers (requirements:
/// docs/feature-requirements/credit-card-settings.md). Holds the card rules and the status
/// calculation; the full numbers live in <see cref="CardSensitiveData"/>.
/// </summary>
public sealed class Card
{
    public const int NameMaxLength = 50;
    public const int ReasonMaxLength = 200;

    /// <summary>A card is "expiring soon" when its last valid day is within this many months.</summary>
    public const int ExpiringSoonMonths = 2;

    private readonly List<CardBillingTerms> _billingTerms = [];

    private Card()
    {
        Name = string.Empty;
        SensitiveData = null!;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public Guid IssuerId { get; private set; }
    public Guid BankId { get; private set; }
    /// <summary>Optional: some cards do not print it. Without it the card is never "not yet valid".</summary>
    public YearMonth? ValidFrom { get; private set; }
    public YearMonth ValidThrough { get; private set; }
    public CardType Type { get; private set; }
    public bool IsPrimary { get; private set; }
    public Money? DebtLimit { get; private set; }

    /// <summary>The check box on Debit cards; false for every other type.</summary>
    public bool AllowsInstallmentsFlag { get; private set; }

    /// <summary>Credit and Charge cards always allow monthly installments, Prepaid never; Debit follows the check box.</summary>
    public bool AllowsInstallments => Type switch
    {
        CardType.Credit or CardType.Charge => true,
        CardType.Debit => AllowsInstallmentsFlag,
        _ => false,
    };

    /// <summary>Last four digits of the card number, kept unencrypted for masked display.</summary>
    public string? CardNumberLast4 { get; private set; }
    public string? IbanCountry { get; private set; }
    public string? IbanLast4 { get; private set; }

    /// <summary>The primary account card payments are paid from (installments FR-1, B3).</summary>
    public Guid? PayingAccountId { get; private set; }

    /// <summary>Whether the bank charges card payments directly from the paying account (B4).</summary>
    public bool PaysFromAccountDirectly { get; private set; }

    /// <summary>The most installments one purchase on this card can have: 12, 24 or 36 (B5).</summary>
    public int? MaxInstallments { get; private set; }

    /// <summary>Every version of the statement and payment day, oldest first (installments FR-2).</summary>
    public IReadOnlyList<CardBillingTerms> BillingTerms => _billingTerms.OrderBy(t => t.FromMonth).ToList();

    /// <summary>
    /// Whether purchases can be recorded on the card: it allows installments and has its billing details.
    /// A card saved before the installments feature has none until the user edits it.
    /// </summary>
    public bool HasBillingDetails => AllowsInstallments && _billingTerms.Count > 0 && PayingAccountId is not null && MaxInstallments is not null;

    /// <summary>A card that allows installments but has no billing details yet.</summary>
    public bool NeedsBillingDetails => AllowsInstallments && !HasBillingDetails;

    public DateOnly? BlockedOn { get; private set; }
    public string? BlockReason { get; private set; }

    /// <summary>Set when the user marked the card as expired; this cannot be undone.</summary>
    public DateOnly? ExpiredOn { get; private set; }
    public string? ExpiryReason { get; private set; }
    public DateOnly? ArchivedOn { get; private set; }
    public Guid? ReplacedByCardId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>The full numbers. Loaded only on request; do not read it on list queries.</summary>
    public CardSensitiveData SensitiveData { get; private set; }

    public string? CardNumberMasked => CardNumberLast4 is null ? null : $"•••• {CardNumberLast4}";
    public string? IbanMasked => IbanLast4 is null ? null : $"{IbanCountry}•• … {IbanLast4}";

    /// <summary>True while the user's state (blocked, expired, archived) prevents edits.</summary>
    public bool IsReadOnly => BlockedOn is not null || ExpiredOn is not null || ArchivedOn is not null;

    /// <summary>The statement and payment dates of every payment month; null without billing details.</summary>
    public CardBillingCalendar? BillingCalendar() => HasBillingDetails ? new CardBillingCalendar(_billingTerms) : null;

    /// <summary>The billing details the user edits: the days that apply to <paramref name="month"/>.</summary>
    public CardBillingDetails? BillingFor(YearMonth month)
    {
        if (BillingCalendar() is not { } calendar)
        {
            return null;
        }
        var terms = calendar.TermsFor(month);
        return new CardBillingDetails(terms.StatementDay, terms.PaymentDay, PayingAccountId!.Value, PaysFromAccountDirectly, MaxInstallments!.Value);
    }

    public static Result<Card> Create(CardDetails details, CardNumber? cardNumber, Iban? iban, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(details);
        var errors = Validate(details, cardNumber, iban);
        if (errors.Count > 0)
        {
            return Result.Failure<Card>(errors);
        }

        var id = Guid.CreateVersion7(now);
        var card = new Card
        {
            Id = id,
            CreatedAt = now,
            SensitiveData = new CardSensitiveData(id, cardNumber, iban),
        };
        card.Apply(details, cardNumber, iban, now);
        card.ApplyBilling(details, nextMonth: CardBillingTerms.Beginning, now);
        return Result.Success(card);
    }

    /// <summary>
    /// Changes all attributes. <paramref name="cardNumber"/> and <paramref name="iban"/> are the new full
    /// values. A change of the statement or payment day applies from the month after
    /// <paramref name="today"/>'s month (installments FR-2).
    /// </summary>
    public Result Update(CardDetails details, CardNumber? cardNumber, Iban? iban, DateOnly today, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(details);
        if (IsReadOnly)
        {
            return Result.Failure(CardErrors.ReadOnly);
        }
        if (SensitiveData is null)
        {
            throw new InvalidOperationException("Load the card with its sensitive data before updating it.");
        }

        var errors = Validate(details, cardNumber, iban);
        if (errors.Count > 0)
        {
            return Result.Failure(errors);
        }

        SensitiveData.Change(cardNumber, iban);
        Apply(details, cardNumber, iban, now);
        ApplyBilling(details, YearMonth.FromDate(today).Next(), now);
        return Result.Success();
    }

    public Result Block(DateOnly today, string? reason, DateTimeOffset now)
    {
        if (ArchivedOn is not null)
        {
            return Result.Failure(CardErrors.AlreadyArchived);
        }
        if (BlockedOn is not null)
        {
            return Result.Failure(CardErrors.AlreadyBlocked);
        }
        if (!TryNormalizeReason(reason, out var normalized))
        {
            return Result.Failure(CardErrors.ReasonTooLong);
        }

        BlockedOn = today;
        BlockReason = normalized;
        IsPrimary = false;
        UpdatedAt = now;
        return Result.Success();
    }

    public Result Unblock(DateTimeOffset now)
    {
        if (ArchivedOn is not null)
        {
            return Result.Failure(CardErrors.AlreadyArchived);
        }
        if (BlockedOn is null)
        {
            return Result.Failure(CardErrors.NotBlocked);
        }

        BlockedOn = null;
        BlockReason = null;
        UpdatedAt = now;
        return Result.Success();
    }

    /// <summary>Marks the card as expired from today. Final: there is no undo.</summary>
    public Result MarkExpired(DateOnly today, string? reason, Card? replacement, DateTimeOffset now)
    {
        if (ArchivedOn is not null)
        {
            return Result.Failure(CardErrors.AlreadyArchived);
        }
        if (ExpiredOn is not null)
        {
            return Result.Failure(CardErrors.AlreadyExpired);
        }
        if (!TryNormalizeReason(reason, out var normalized))
        {
            return Result.Failure(CardErrors.ReasonTooLong);
        }
        if (replacement is not null)
        {
            var check = CheckReplacement(replacement, today);
            if (check.IsFailure)
            {
                return check;
            }
        }

        ExpiredOn = today;
        ExpiryReason = normalized;
        IsPrimary = false;
        ReplacedByCardId = replacement?.Id ?? ReplacedByCardId;
        UpdatedAt = now;
        return Result.Success();
    }

    /// <summary>Links the card that replaces this one, e.g. a new card added right after expiring this one.</summary>
    public Result LinkReplacement(Card replacement, DateOnly today, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        if (ArchivedOn is not null)
        {
            return Result.Failure(CardErrors.AlreadyArchived);
        }
        var check = CheckReplacement(replacement, today);
        if (check.IsFailure)
        {
            return check;
        }

        ReplacedByCardId = replacement.Id;
        UpdatedAt = now;
        return Result.Success();
    }

    /// <summary>Archives the card instead of deleting it. Final: an archived card cannot be restored.</summary>
    public Result Archive(DateOnly today, DateTimeOffset now)
    {
        if (ArchivedOn is not null)
        {
            return Result.Failure(CardErrors.AlreadyArchived);
        }

        ArchivedOn = today;
        IsPrimary = false;
        UpdatedAt = now;
        return Result.Success();
    }

    /// <summary>The card's status on <paramref name="today"/> (requirements FR-11).</summary>
    public CardStatus StatusOn(DateOnly today)
    {
        if (ArchivedOn is not null)
        {
            return CardStatus.Archived;
        }
        if (BlockedOn is not null)
        {
            return CardStatus.Blocked;
        }
        if (ExpiredOn is not null || today > ValidThrough.LastDay)
        {
            return CardStatus.Expired;
        }
        if (ValidFrom is { } validFrom && today < validFrom.FirstDay)
        {
            return CardStatus.NotYetValid;
        }
        if (ValidThrough.LastDay <= today.AddMonths(ExpiringSoonMonths))
        {
            return CardStatus.ExpiringSoon;
        }
        return CardStatus.Active;
    }

    /// <summary>Whether new activity (installments, charges, payments) may use this card.</summary>
    public bool CanBeUsedOn(DateOnly today) => StatusOn(today) is CardStatus.Active or CardStatus.ExpiringSoon;

    /// <summary>Shown in the Settings list without a filter (FR-1).</summary>
    public bool IsListedByDefault(DateOnly today, bool hasOngoingTransactions) => StatusOn(today) switch
    {
        CardStatus.Archived => false,
        CardStatus.Expired => hasOngoingTransactions,
        _ => true,
    };

    /// <summary>Shown on the Dashboard (FR-9).</summary>
    public bool IsShownOnDashboard(DateOnly today, bool hasOngoingTransactions) => StatusOn(today) switch
    {
        CardStatus.Active or CardStatus.ExpiringSoon => true,
        CardStatus.Blocked or CardStatus.Expired => hasOngoingTransactions,
        _ => false,
    };

    private static List<DomainError> Validate(CardDetails details, CardNumber? cardNumber, Iban? iban)
    {
        var errors = new List<DomainError>();
        var name = details.Name?.Trim() ?? string.Empty;
        if (name.Length == 0)
        {
            errors.Add(CardErrors.NameRequired);
        }
        else if (name.Length > NameMaxLength)
        {
            errors.Add(CardErrors.NameTooLong);
        }
        if (details.IssuerId == Guid.Empty)
        {
            errors.Add(CardErrors.IssuerRequired);
        }
        if (details.BankId == Guid.Empty)
        {
            errors.Add(CardErrors.BankRequired);
        }
        if (details.ValidFrom is { } validFrom && details.ValidThrough < validFrom)
        {
            errors.Add(CardErrors.ValidityOrder);
        }
        if (details.DebtLimit is { IsNegative: true })
        {
            errors.Add(CardErrors.DebtLimitNegative);
        }
        if (details.IsPrimary && details.Type != CardType.Debit)
        {
            errors.Add(CardErrors.PrimaryNotDebit);
        }
        if (details.AllowsInstallments && details.Type != CardType.Debit)
        {
            errors.Add(CardErrors.InstallmentsNotDebit);
        }
        if (AllowsInstallmentsFor(details))
        {
            ValidateBilling(details.Billing, errors);
        }
        return errors;
    }

    /// <summary>Credit and Charge cards always allow installments, Prepaid never, Debit by the check box.</summary>
    public static bool AllowsInstallmentsFor(CardDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);
        return details.Type switch
        {
            CardType.Credit or CardType.Charge => true,
            CardType.Debit => details.AllowsInstallments,
            _ => false,
        };
    }

    private static void ValidateBilling(CardBillingDetails? billing, List<DomainError> errors)
    {
        if (billing is null)
        {
            errors.Add(CardErrors.BillingRequired);
            return;
        }
        var statementValid = billing.StatementDay is >= CardBillingTerms.MinStatementDay and <= CardBillingTerms.MaxStatementDay;
        if (!statementValid)
        {
            errors.Add(CardErrors.StatementDayInvalid);
        }
        if (billing.PaymentDay is < CardBillingTerms.MinPaymentDay or > CardBillingTerms.MaxPaymentDay)
        {
            errors.Add(CardErrors.PaymentDayInvalid);
        }
        else if (statementValid && billing.PaymentDay <= billing.StatementDay)
        {
            errors.Add(CardErrors.PaymentDayNotAfterStatement);
        }
        if (billing.PayingAccountId == Guid.Empty)
        {
            errors.Add(CardErrors.PayingAccountRequired);
        }
        if (!CardBillingDetails.MaxInstallmentsChoices.Contains(billing.MaxInstallments))
        {
            errors.Add(CardErrors.MaxInstallmentsInvalid);
        }
    }

    /// <summary>
    /// Sets the billing details, or clears them when the card no longer allows installments. Changed
    /// days apply from <paramref name="nextMonth"/>: later versions are replaced and earlier months keep
    /// theirs (installments FR-2).
    /// </summary>
    private void ApplyBilling(CardDetails details, YearMonth nextMonth, DateTimeOffset now)
    {
        if (!AllowsInstallmentsFor(details) || details.Billing is not { } billing)
        {
            _billingTerms.Clear();
            PayingAccountId = null;
            PaysFromAccountDirectly = false;
            MaxInstallments = null;
            return;
        }

        PayingAccountId = billing.PayingAccountId;
        PaysFromAccountDirectly = billing.PaysFromAccountDirectly;
        MaxInstallments = billing.MaxInstallments;
        if (_billingTerms.Count == 0)
        {
            _billingTerms.Add(CardBillingTerms.Create(Id, CardBillingTerms.Beginning, billing.StatementDay, billing.PaymentDay, now));
            return;
        }

        var current = new CardBillingCalendar(_billingTerms).TermsFor(nextMonth);
        if (current.HasDays(billing.StatementDay, billing.PaymentDay))
        {
            return;
        }
        _billingTerms.RemoveAll(t => t.FromMonth > nextMonth);
        var fromNextMonth = _billingTerms.Find(t => t.FromMonth == nextMonth);
        if (fromNextMonth is not null)
        {
            fromNextMonth.ChangeDays(billing.StatementDay, billing.PaymentDay);
            // Back to the days the month before had: that version alone is enough.
            var before = _billingTerms.Where(t => t.FromMonth < nextMonth).OrderBy(t => t.FromMonth).LastOrDefault();
            if (before is not null && before.HasDays(billing.StatementDay, billing.PaymentDay))
            {
                _billingTerms.Remove(fromNextMonth);
            }
            return;
        }
        _billingTerms.Add(CardBillingTerms.Create(Id, nextMonth, billing.StatementDay, billing.PaymentDay, now));
    }

    private void Apply(CardDetails details, CardNumber? cardNumber, Iban? iban, DateTimeOffset now)
    {
        Name = details.Name.Trim();
        IssuerId = details.IssuerId;
        BankId = details.BankId;
        ValidFrom = details.ValidFrom;
        ValidThrough = details.ValidThrough;
        Type = details.Type;
        IsPrimary = details.IsPrimary;
        DebtLimit = details.DebtLimit;
        AllowsInstallmentsFlag = details.AllowsInstallments;
        CardNumberLast4 = cardNumber?.Last4;
        IbanCountry = iban?.CountryCode;
        IbanLast4 = iban?.Last4;
        UpdatedAt = now;
    }

    private Result CheckReplacement(Card replacement, DateOnly today)
    {
        if (replacement.Id == Id)
        {
            return Result.Failure(CardErrors.ReplacementIsSameCard);
        }
        // A replacement may still be "not yet valid" (a new card that starts next month).
        if (replacement.StatusOn(today) is CardStatus.Archived or CardStatus.Blocked or CardStatus.Expired)
        {
            return Result.Failure(CardErrors.ReplacementUnusable);
        }
        return Result.Success();
    }

    private static bool TryNormalizeReason(string? reason, out string? normalized)
    {
        normalized = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        return normalized is null || normalized.Length <= ReasonMaxLength;
    }
}
