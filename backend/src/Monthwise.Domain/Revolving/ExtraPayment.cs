using Monthwise.Domain.Common;

namespace Monthwise.Domain.Revolving;

/// <summary>What the user enters for an extra payment (revolving FR-8, X1 to X5).</summary>
public sealed record ExtraPaymentDetails(Guid CardId, Guid AccountId, DateOnly Date, Money Amount, string? Description);

/// <summary>
/// A payment from an account to a revolving card, made at any time, that lowers the debt (revolving
/// FR-8). It is always paid on its date. That the amount is not more than the debt, and that the card
/// and the account can be used, is checked by the caller.
/// </summary>
public sealed class ExtraPayment
{
    public const int DescriptionMaxLength = 100;

    private ExtraPayment()
    {
    }

    public Guid Id { get; private set; }
    public Guid CardId { get; private set; }
    public Guid AccountId { get; private set; }
    public DateOnly Date { get; private set; }
    public Money Amount { get; private set; }
    public string? Description { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static Result<ExtraPayment> Create(ExtraPaymentDetails details, DateOnly today, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(details);
        var errors = Validate(details, today, out var description);
        if (errors.Count > 0)
        {
            return Result.Failure<ExtraPayment>(errors);
        }
        var payment = new ExtraPayment { Id = Guid.CreateVersion7(now), CardId = details.CardId, CreatedAt = now };
        payment.Apply(details, description, now);
        return Result.Success(payment);
    }

    /// <summary>Changes the account, date, amount and description; the card stays.</summary>
    public Result Update(ExtraPaymentDetails details, DateOnly today, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(details);
        var errors = Validate(details, today, out var description);
        if (errors.Count > 0)
        {
            return Result.Failure(errors);
        }
        Apply(details, description, now);
        return Result.Success();
    }

    private void Apply(ExtraPaymentDetails details, string? description, DateTimeOffset now)
    {
        AccountId = details.AccountId;
        Date = details.Date;
        Amount = details.Amount;
        Description = description;
        UpdatedAt = now;
    }

    private static List<DomainError> Validate(ExtraPaymentDetails details, DateOnly today, out string? description)
    {
        var errors = new List<DomainError>();
        if (!details.Amount.IsPositive)
        {
            errors.Add(RevolvingErrors.AmountNotPositive);
        }
        if (details.Date > today)
        {
            errors.Add(RevolvingErrors.DateInFuture);
        }
        if (details.AccountId == Guid.Empty)
        {
            errors.Add(RevolvingErrors.AccountRequired);
        }
        description = string.IsNullOrWhiteSpace(details.Description) ? null : details.Description.Trim();
        if (description is { Length: > DescriptionMaxLength })
        {
            errors.Add(RevolvingErrors.DescriptionTooLong);
        }
        return errors;
    }
}
