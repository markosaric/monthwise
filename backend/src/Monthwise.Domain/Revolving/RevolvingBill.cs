using Monthwise.Domain.Common;

namespace Monthwise.Domain.Revolving;

/// <summary>
/// The stored state of one monthly bill of a revolving card (revolving FR-4, FR-8), created the first
/// time the user pays, unticks or corrects it. Its debt, interest and minimum payment are calculated;
/// <see cref="Minimum"/> and <see cref="Interest"/> are kept only when the user corrected them.
/// <see cref="IsPaid"/> is the user's explicit state; when it is null, the bill of a card connected to
/// its paying account is paid automatically after its due date.
/// </summary>
public sealed class RevolvingBill
{
    private RevolvingBill()
    {
    }

    public Guid Id { get; private set; }
    public Guid CardId { get; private set; }
    public YearMonth Month { get; private set; }

    /// <summary>The minimum payment as the bank's bill shows it, when it differs from the calculation.</summary>
    public Money? Minimum { get; private set; }

    /// <summary>The interest as the bank's bill shows it, when it differs from the calculation.</summary>
    public Money? Interest { get; private set; }

    public bool? IsPaid { get; private set; }
    public DateOnly? PaidDate { get; private set; }
    public Money? PaidAmount { get; private set; }
    public Guid? PaidAccountId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public bool IsCorrected => Minimum is not null || Interest is not null;

    /// <summary>True when nothing is stored that the calculation does not already give.</summary>
    public bool IsEmpty => IsPaid is null && !IsCorrected;

    public static RevolvingBill Create(Guid cardId, YearMonth month, DateTimeOffset now) =>
        new()
        {
            Id = Guid.CreateVersion7(now),
            CardId = cardId,
            Month = month,
            CreatedAt = now,
            UpdatedAt = now,
        };

    /// <summary>Marks the bill as paid on <paramref name="paidDate"/> with <paramref name="amount"/> from <paramref name="accountId"/>.</summary>
    public Result MarkPaid(DateOnly paidDate, Guid accountId, Money amount, DateOnly billDate, DateTimeOffset now)
    {
        var errors = new List<DomainError>();
        if (paidDate < billDate)
        {
            errors.Add(RevolvingErrors.PaidBeforeBill);
        }
        if (accountId == Guid.Empty)
        {
            errors.Add(RevolvingErrors.AccountRequired);
        }
        if (!amount.IsPositive)
        {
            errors.Add(RevolvingErrors.AmountNotPositive);
        }
        if (errors.Count > 0)
        {
            return Result.Failure(errors);
        }
        IsPaid = true;
        PaidDate = paidDate;
        PaidAccountId = accountId;
        PaidAmount = amount;
        UpdatedAt = now;
        return Result.Success();
    }

    /// <summary>Unticks the bill: it is expected again, also when it would be paid automatically.</summary>
    public void MarkUnpaid(DateTimeOffset now)
    {
        IsPaid = false;
        PaidDate = null;
        PaidAccountId = null;
        PaidAmount = null;
        UpdatedAt = now;
    }

    /// <summary>Sets the minimum payment and interest of the bank's bill (FR-4, FR-7); null keeps the calculated value.</summary>
    public Result Correct(Money? minimum, Money? interest, DateTimeOffset now)
    {
        if (minimum is { IsNegative: true } || interest is { IsNegative: true })
        {
            return Result.Failure(RevolvingErrors.CorrectionNegative);
        }
        Minimum = minimum;
        Interest = interest;
        UpdatedAt = now;
        return Result.Success();
    }
}
