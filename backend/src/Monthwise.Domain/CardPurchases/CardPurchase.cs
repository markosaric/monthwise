using Monthwise.Domain.Cards;
using Monthwise.Domain.Common;

namespace Monthwise.Domain.CardPurchases;

/// <summary>What the user enters for a card purchase (installments FR-3, P1 to P8).</summary>
public sealed record CardPurchaseDetails(
    Guid CardId,
    string? Description,
    DateOnly PurchaseDate,
    Money Amount,
    bool InInstallments,
    int? InstallmentCount,
    bool PostponeFirst,
    Guid? CategoryId);

/// <summary>One monthly installment of a purchase and the payment month of the card payment it is in.</summary>
public sealed record PurchaseInstallment(int Number, int Total, Money Amount, YearMonth PaymentMonth);

/// <summary>
/// A purchase made with a card, paid in full or in monthly installments (requirements:
/// docs/feature-requirements/credit-card-installments.md). Its installments are calculated, not
/// stored. A reversal of the whole purchase is kept on the purchase; reversals of single installments
/// are <see cref="InstallmentReversal"/>s.
/// </summary>
public sealed class CardPurchase
{
    public const int DescriptionMaxLength = 100;
    public const int ReasonMaxLength = 200;
    public const int MinInstallments = 2;

    private readonly List<InstallmentReversal> _reversals = [];

    private CardPurchase()
    {
        Description = string.Empty;
    }

    public Guid Id { get; private set; }
    public Guid CardId { get; private set; }
    public string Description { get; private set; }
    public DateOnly PurchaseDate { get; private set; }
    public Money Amount { get; private set; }

    /// <summary>1 for a purchase paid in full, otherwise the number of monthly installments.</summary>
    public int InstallmentCount { get; private set; }

    /// <summary>The first installment is one billing period later (P7).</summary>
    public bool PostponeFirst { get; private set; }

    public Guid? CategoryId { get; private set; }

    /// <summary>Set when the whole purchase is reversed (FR-5); a reversal cannot be undone.</summary>
    public DateOnly? ReversedOn { get; private set; }
    public string? ReversalReason { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Reversals of single installments, by installment number.</summary>
    public IReadOnlyList<InstallmentReversal> Reversals => _reversals.OrderBy(r => r.InstallmentNumber).ToList();

    public bool InInstallments => InstallmentCount > 1;

    public bool IsReversed => ReversedOn is not null;

    public CardPurchaseDetails Details => new(
        CardId, Description, PurchaseDate, Amount, InInstallments, InInstallments ? InstallmentCount : null, PostponeFirst, CategoryId);

    /// <summary>
    /// Records a purchase. <paramref name="maxInstallments"/> is the card's limit (B5). The card itself
    /// (installments allowed, usable, valid from) and the category are checked by the caller.
    /// </summary>
    public static Result<CardPurchase> Create(CardPurchaseDetails details, int maxInstallments, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(details);
        var errors = Validate(details, maxInstallments);
        if (errors.Count > 0)
        {
            return Result.Failure<CardPurchase>(errors);
        }
        var purchase = new CardPurchase { Id = Guid.CreateVersion7(now), CreatedAt = now };
        purchase.Apply(details, now);
        return Result.Success(purchase);
    }

    /// <summary>Edits a purchase that is not reversed (FR-6). Its installments are recalculated.</summary>
    public Result Update(CardPurchaseDetails details, int maxInstallments, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(details);
        if (IsReversed)
        {
            return Result.Failure(CardPurchaseErrors.Reversed);
        }
        var errors = Validate(details, maxInstallments);
        if (errors.Count > 0)
        {
            return Result.Failure(errors);
        }
        Apply(details, now);
        // Reversals of installments the purchase no longer has are dropped.
        _reversals.RemoveAll(r => r.InstallmentNumber > InstallmentCount);
        return Result.Success();
    }

    /// <summary>Reverses the whole purchase on <paramref name="today"/> (FR-5).</summary>
    public Result Reverse(DateOnly today, string? reason, DateTimeOffset now)
    {
        if (IsReversed)
        {
            return Result.Failure(CardPurchaseErrors.Reversed);
        }
        if (!TryNormalizeReason(reason, out var normalized))
        {
            return Result.Failure(CardPurchaseErrors.ReasonTooLong);
        }
        ReversedOn = today;
        ReversalReason = normalized;
        UpdatedAt = now;
        return Result.Success();
    }

    /// <summary>Reverses one installment on <paramref name="today"/> (FR-5).</summary>
    public Result ReverseInstallment(int number, DateOnly today, string? reason, DateTimeOffset now)
    {
        if (IsReversed)
        {
            return Result.Failure(CardPurchaseErrors.Reversed);
        }
        if (number < 1 || number > InstallmentCount)
        {
            return Result.Failure(CardPurchaseErrors.InstallmentNotFound);
        }
        if (_reversals.Exists(r => r.InstallmentNumber == number))
        {
            return Result.Failure(CardPurchaseErrors.InstallmentReversed);
        }
        if (!TryNormalizeReason(reason, out var normalized))
        {
            return Result.Failure(CardPurchaseErrors.ReasonTooLong);
        }
        _reversals.Add(InstallmentReversal.Create(Id, number, today, normalized, now));
        UpdatedAt = now;
        return Result.Success();
    }

    /// <summary>
    /// The installments of the purchase (FR-4): equal amounts with the remaining cents on the last one,
    /// one per payment month from the billing period that contains the purchase date, or the one after
    /// it when the first installment is postponed. A purchase paid in full has one.
    /// </summary>
    public IReadOnlyList<PurchaseInstallment> Installments(CardBillingCalendar calendar)
    {
        ArgumentNullException.ThrowIfNull(calendar);
        var first = calendar.PaymentMonthOf(PurchaseDate);
        if (PostponeFirst && InInstallments)
        {
            first = first.Next();
        }
        var regular = Money.FromCents(Amount.Cents / InstallmentCount);
        var last = Amount - regular * (InstallmentCount - 1);
        return Enumerable.Range(1, InstallmentCount)
            .Select(n => new PurchaseInstallment(n, InstallmentCount, n == InstallmentCount ? last : regular, first.AddMonths(n - 1)))
            .ToList();
    }

    /// <summary>The day an installment was reversed: the purchase's reversal, or the installment's own.</summary>
    public InstallmentReversalInfo? ReversalOf(int number)
    {
        if (ReversedOn is { } reversedOn)
        {
            return new InstallmentReversalInfo(reversedOn, ReversalReason, WholePurchase: true);
        }
        var own = _reversals.Find(r => r.InstallmentNumber == number);
        return own is null ? null : new InstallmentReversalInfo(own.ReversedOn, own.Reason, WholePurchase: false);
    }

    private static List<DomainError> Validate(CardPurchaseDetails details, int maxInstallments)
    {
        var errors = new List<DomainError>();
        if (details.CardId == Guid.Empty)
        {
            errors.Add(CardPurchaseErrors.CardRequired);
        }
        var description = details.Description?.Trim() ?? string.Empty;
        if (description.Length == 0)
        {
            errors.Add(CardPurchaseErrors.DescriptionRequired);
        }
        else if (description.Length > DescriptionMaxLength)
        {
            errors.Add(CardPurchaseErrors.DescriptionTooLong);
        }
        if (!details.Amount.IsPositive)
        {
            errors.Add(CardPurchaseErrors.AmountNotPositive);
        }
        if (details.InInstallments)
        {
            if (details.InstallmentCount is not { } count)
            {
                errors.Add(CardPurchaseErrors.InstallmentCountRequired);
            }
            else if (count < MinInstallments)
            {
                errors.Add(CardPurchaseErrors.InstallmentCountTooSmall);
            }
            else if (count > maxInstallments)
            {
                errors.Add(CardPurchaseErrors.InstallmentCountTooLarge);
            }
        }
        return errors;
    }

    private void Apply(CardPurchaseDetails details, DateTimeOffset now)
    {
        CardId = details.CardId;
        Description = details.Description!.Trim();
        PurchaseDate = details.PurchaseDate;
        Amount = details.Amount;
        InstallmentCount = details.InInstallments ? details.InstallmentCount!.Value : 1;
        PostponeFirst = details.InInstallments && details.PostponeFirst;
        CategoryId = details.CategoryId;
        UpdatedAt = now;
    }

    private static bool TryNormalizeReason(string? reason, out string? normalized)
    {
        normalized = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        return normalized is null || normalized.Length <= ReasonMaxLength;
    }
}

/// <summary>When and why an installment was reversed.</summary>
public sealed record InstallmentReversalInfo(DateOnly ReversedOn, string? Reason, bool WholePurchase);

/// <summary>The reversal of one installment of a purchase (FR-5).</summary>
public sealed class InstallmentReversal
{
    private InstallmentReversal()
    {
    }

    public Guid Id { get; private set; }
    public Guid PurchaseId { get; private set; }
    public int InstallmentNumber { get; private set; }
    public DateOnly ReversedOn { get; private set; }
    public string? Reason { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    internal static InstallmentReversal Create(Guid purchaseId, int number, DateOnly reversedOn, string? reason, DateTimeOffset now) =>
        new()
        {
            Id = Guid.CreateVersion7(now),
            PurchaseId = purchaseId,
            InstallmentNumber = number,
            ReversedOn = reversedOn,
            Reason = reason,
            CreatedAt = now,
        };
}
