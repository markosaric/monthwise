using Monthwise.Domain.Common;

namespace Monthwise.Domain.CashFlow;

/// <summary>
/// The kind of an input, e.g. Salary (FR-6). The user can add and rename categories; a category used
/// by inputs is hidden instead of deleted.
/// </summary>
public sealed class InputCategory
{
    public const int NameMaxLength = 50;

    private InputCategory()
    {
        Name = string.Empty;
    }

    private InputCategory(Guid id, string name, bool isPredefined)
    {
        Id = id;
        Name = name;
        IsPredefined = isPredefined;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public bool IsPredefined { get; private set; }
    public DateOnly? HiddenOn { get; private set; }

    public bool IsHidden => HiddenOn is not null;

    public static Result<InputCategory> Create(string? name, DateTimeOffset now)
    {
        var valid = ValidateName(name);
        return valid.IsSuccess
            ? Result.Success(new InputCategory(Guid.CreateVersion7(now), valid.Value, isPredefined: false))
            : Result.Failure<InputCategory>(valid.Errors);
    }

    /// <summary>For seed data only.</summary>
    public static InputCategory Predefined(Guid id, string name) => new(id, name, isPredefined: true);

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
            return Result.Failure<string>(CashFlowErrors.CategoryNameRequired);
        }
        return trimmed.Length > NameMaxLength
            ? Result.Failure<string>(CashFlowErrors.CategoryNameTooLong)
            : Result.Success(trimmed);
    }
}
