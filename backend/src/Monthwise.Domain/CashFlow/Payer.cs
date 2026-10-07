using Monthwise.Domain.Common;

namespace Monthwise.Domain.CashFlow;

/// <summary>
/// Who pays an input (FR-5). Saved automatically the first time it is typed. Removing a payer only
/// hides it from suggestions; inputs keep showing its name, and typing it again brings it back.
/// </summary>
public sealed class Payer
{
    public const int NameMaxLength = 100;

    private Payer()
    {
        Name = string.Empty;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public DateOnly? RemovedOn { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public bool IsRemoved => RemovedOn is not null;

    public static Result<Payer> Create(string? name, DateTimeOffset now)
    {
        var valid = ValidateName(name, CashFlowErrors.PayerRequired, CashFlowErrors.PayerTooLong);
        return valid.IsSuccess
            ? Result.Success(new Payer { Id = Guid.CreateVersion7(now), Name = valid.Value, CreatedAt = now })
            : Result.Failure<Payer>(valid.Errors);
    }

    public Result Rename(string? name)
    {
        var valid = ValidateName(name, CashFlowErrors.PayerNameRequired, CashFlowErrors.PayerNameTooLong);
        if (valid.IsFailure)
        {
            return valid;
        }
        Name = valid.Value;
        return Result.Success();
    }

    public void Remove(DateOnly today) => RemovedOn ??= today;

    public void Restore() => RemovedOn = null;

    /// <summary>Trims and checks a payer name.</summary>
    public static Result<string> ValidateName(string? name, DomainError required, DomainError tooLong)
    {
        ArgumentNullException.ThrowIfNull(required);
        ArgumentNullException.ThrowIfNull(tooLong);
        var trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return Result.Failure<string>(required);
        }
        return trimmed.Length > NameMaxLength ? Result.Failure<string>(tooLong) : Result.Success(trimmed);
    }
}
