using Monthwise.Domain.Common;

namespace Monthwise.Domain.CardPurchases;

/// <summary>
/// The stored paid state of one card payment (installments FR-8), created the first time the user
/// ticks, unticks or corrects it. Its amount is always calculated from the purchases.
/// <see cref="IsPaid"/> is the user's explicit state; when it is null, the card payment of a card
/// connected to its paying account is paid automatically after its payment date.
/// </summary>
public sealed class CardPayment
{
    private CardPayment()
    {
    }

    public Guid Id { get; private set; }
    public Guid CardId { get; private set; }
    public YearMonth Month { get; private set; }
    public bool? IsPaid { get; private set; }
    public DateOnly? PaidDate { get; private set; }
    public Guid? PaidAccountId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>True when nothing is stored that the calculation does not already give.</summary>
    public bool IsEmpty => IsPaid is null;

    public static CardPayment Create(Guid cardId, YearMonth month, DateTimeOffset now) =>
        new()
        {
            Id = Guid.CreateVersion7(now),
            CardId = cardId,
            Month = month,
            CreatedAt = now,
            UpdatedAt = now,
        };

    /// <summary>Marks the card payment as paid on <paramref name="paidDate"/> from <paramref name="accountId"/>.</summary>
    public Result MarkPaid(DateOnly paidDate, Guid accountId, DateOnly statementDate, DateTimeOffset now)
    {
        var errors = new List<DomainError>();
        if (paidDate < statementDate)
        {
            errors.Add(CardPurchaseErrors.PaidBeforeStatement);
        }
        if (accountId == Guid.Empty)
        {
            errors.Add(CardPurchaseErrors.PaidAccountRequired);
        }
        if (errors.Count > 0)
        {
            return Result.Failure(errors);
        }
        IsPaid = true;
        PaidDate = paidDate;
        PaidAccountId = accountId;
        UpdatedAt = now;
        return Result.Success();
    }

    /// <summary>Unticks the card payment: it is expected again, also when it would be paid automatically.</summary>
    public void MarkUnpaid(DateTimeOffset now)
    {
        IsPaid = false;
        PaidDate = null;
        PaidAccountId = null;
        UpdatedAt = now;
    }

    /// <summary>Forgets the explicit state, so the card payment follows the calculation again.</summary>
    public void ClearPaidState(DateTimeOffset now)
    {
        IsPaid = null;
        PaidDate = null;
        PaidAccountId = null;
        UpdatedAt = now;
    }
}
