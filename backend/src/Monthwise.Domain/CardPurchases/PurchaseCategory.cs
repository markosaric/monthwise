using Monthwise.Domain.Common;

namespace Monthwise.Domain.CardPurchases;

/// <summary>
/// The optional kind of a card purchase, e.g. Home or Car (installments FR-10). The user adds and
/// renames categories; a category used by purchases is hidden instead of deleted.
/// </summary>
public sealed class PurchaseCategory
{
    public const int NameMaxLength = 50;

    private PurchaseCategory()
    {
        Name = string.Empty;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public DateOnly? HiddenOn { get; private set; }

    public bool IsHidden => HiddenOn is not null;

    public static Result<PurchaseCategory> Create(string? name, DateTimeOffset now)
    {
        var valid = ValidateName(name);
        return valid.IsSuccess
            ? Result.Success(new PurchaseCategory { Id = Guid.CreateVersion7(now), Name = valid.Value })
            : Result.Failure<PurchaseCategory>(valid.Errors);
    }

    public Result Rename(string? name)
    {
        var valid = ValidateName(name);
        if (valid.IsFailure)
        {
            return valid;
        }
        Name = valid.Value;
        return Result.Success();
    }

    public void Hide(DateOnly today) => HiddenOn ??= today;

    public void Show() => HiddenOn = null;

    private static Result<string> ValidateName(string? name)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return Result.Failure<string>(CardPurchaseErrors.CategoryNameRequired);
        }
        return trimmed.Length > NameMaxLength
            ? Result.Failure<string>(CardPurchaseErrors.CategoryNameTooLong)
            : Result.Success(trimmed);
    }
}
