using Monthwise.Domain.Common;

namespace Monthwise.Domain.Loans;

/// <summary>
/// The kind of a loan, e.g. Bank loan or Lease (FR-8). The user can add and rename categories; a
/// category used by a loan is hidden instead of deleted.
/// </summary>
public sealed class LoanCategory
{
    public const int NameMaxLength = 50;

    private LoanCategory()
    {
        Name = string.Empty;
    }

    private LoanCategory(Guid id, string name, bool allowsResidualValue, bool isPredefined)
    {
        Id = id;
        Name = name;
        AllowsResidualValue = allowsResidualValue;
        IsPredefined = isPredefined;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; }

    /// <summary>Whether loans of this category can have a residual value (A12). Unticking it does not change existing loans.</summary>
    public bool AllowsResidualValue { get; private set; }

    public bool IsPredefined { get; private set; }
    public DateOnly? HiddenOn { get; private set; }

    public bool IsHidden => HiddenOn is not null;

    public static Result<LoanCategory> Create(string? name, bool allowsResidualValue, DateTimeOffset now)
    {
        var valid = LoanNames.Validate(name, LoanErrors.CategoryNameRequired, LoanErrors.CategoryNameTooLong, NameMaxLength);
        return valid.IsSuccess
            ? Result.Success(new LoanCategory(Guid.CreateVersion7(now), valid.Value, allowsResidualValue, isPredefined: false))
            : Result.Failure<LoanCategory>(valid.Errors);
    }

    /// <summary>For seed data only.</summary>
    public static LoanCategory Predefined(Guid id, string name, bool allowsResidualValue) =>
        new(id, name, allowsResidualValue, isPredefined: true);

    public Result Rename(string? name)
    {
        var valid = LoanNames.Validate(name, LoanErrors.CategoryNameRequired, LoanErrors.CategoryNameTooLong, NameMaxLength);
        if (valid.IsFailure)
        {
            return valid;
        }
        Name = valid.Value;
        return Result.Success();
    }

    public void SetAllowsResidualValue(bool allows) => AllowsResidualValue = allows;

    public void Hide(DateOnly today) => HiddenOn ??= today;

    public void Show() => HiddenOn = null;
}

/// <summary>A finer kind of loan within its category, e.g. Mortgage (FR-8).</summary>
public sealed class LoanSubcategory
{
    public const int NameMaxLength = 50;

    private LoanSubcategory()
    {
        Name = string.Empty;
    }

    private LoanSubcategory(Guid id, Guid categoryId, string name, bool isPredefined)
    {
        Id = id;
        CategoryId = categoryId;
        Name = name;
        IsPredefined = isPredefined;
    }

    public Guid Id { get; private set; }
    public Guid CategoryId { get; private set; }
    public string Name { get; private set; }
    public bool IsPredefined { get; private set; }
    public DateOnly? HiddenOn { get; private set; }

    public bool IsHidden => HiddenOn is not null;

    public static Result<LoanSubcategory> Create(Guid categoryId, string? name, DateTimeOffset now)
    {
        var valid = LoanNames.Validate(name, LoanErrors.SubcategoryNameRequired, LoanErrors.SubcategoryNameTooLong, NameMaxLength);
        return valid.IsSuccess
            ? Result.Success(new LoanSubcategory(Guid.CreateVersion7(now), categoryId, valid.Value, isPredefined: false))
            : Result.Failure<LoanSubcategory>(valid.Errors);
    }

    /// <summary>For seed data only.</summary>
    public static LoanSubcategory Predefined(Guid id, Guid categoryId, string name) => new(id, categoryId, name, isPredefined: true);

    public Result Rename(string? name)
    {
        var valid = LoanNames.Validate(name, LoanErrors.SubcategoryNameRequired, LoanErrors.SubcategoryNameTooLong, NameMaxLength);
        if (valid.IsFailure)
        {
            return valid;
        }
        Name = valid.Value;
        return Result.Success();
    }

    public void Hide(DateOnly today) => HiddenOn ??= today;

    public void Show() => HiddenOn = null;
}

internal static class LoanNames
{
    public static Result<string> Validate(string? name, DomainError required, DomainError tooLong, int maxLength)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return Result.Failure<string>(required);
        }
        return trimmed.Length > maxLength ? Result.Failure<string>(tooLong) : Result.Success(trimmed);
    }
}
