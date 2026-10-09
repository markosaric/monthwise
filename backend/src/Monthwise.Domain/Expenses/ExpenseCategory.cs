using Monthwise.Domain.Common;

namespace Monthwise.Domain.Expenses;

/// <summary>
/// The kind of a bill, an expense or a card purchase, e.g. Electricity or Groceries (bills and
/// expenses FR-1; installments Appendix 1). One shared list: the user adds and renames categories, and
/// a category in use is hidden instead of deleted. <see cref="IsFixed"/> marks Cash withdrawal, which
/// cannot be renamed or hidden.
/// </summary>
public sealed class ExpenseCategory
{
    public const int NameMaxLength = 50;

    private ExpenseCategory()
    {
        Name = string.Empty;
    }

    private ExpenseCategory(Guid id, string name, bool isPredefined, bool isFixed)
    {
        Id = id;
        Name = name;
        IsPredefined = isPredefined;
        IsFixed = isFixed;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; }

    /// <summary>One of the starting categories (FR-1).</summary>
    public bool IsPredefined { get; private set; }

    /// <summary>Cash withdrawal: always in the list, never renamed or hidden (FR-1, FR-8).</summary>
    public bool IsFixed { get; private set; }

    public DateOnly? HiddenOn { get; private set; }

    public bool IsHidden => HiddenOn is not null;

    public static Result<ExpenseCategory> Create(string? name, DateTimeOffset now)
    {
        var valid = ValidateName(name);
        return valid.IsSuccess
            ? Result.Success(new ExpenseCategory(Guid.CreateVersion7(now), valid.Value, isPredefined: false, isFixed: false))
            : Result.Failure<ExpenseCategory>(valid.Errors);
    }

    /// <summary>For seed data and tests only.</summary>
    public static ExpenseCategory Predefined(Guid id, string name, bool isFixed = false) => new(id, name, isPredefined: true, isFixed);

    public Result Rename(string? name)
    {
        if (IsFixed)
        {
            return Result.Failure(ExpenseErrors.CategoryFixed);
        }
        var valid = ValidateName(name);
        if (valid.IsFailure)
        {
            return valid;
        }
        Name = valid.Value;
        return Result.Success();
    }

    public Result Hide(DateOnly today)
    {
        if (IsFixed)
        {
            return Result.Failure(ExpenseErrors.CategoryFixed);
        }
        HiddenOn ??= today;
        return Result.Success();
    }

    public void Show() => HiddenOn = null;

    private static Result<string> ValidateName(string? name)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return Result.Failure<string>(ExpenseErrors.CategoryNameRequired);
        }
        return trimmed.Length > NameMaxLength
            ? Result.Failure<string>(ExpenseErrors.CategoryNameTooLong)
            : Result.Success(trimmed);
    }
}

/// <summary>The starting expense categories (FR-1), added by the migration that creates the shared list.</summary>
public static class ExpenseCategorySeedData
{
    /// <summary>The fixed Cash withdrawal category (FR-8).</summary>
    public static readonly Guid CashWithdrawalId = new("0199c8b0-0000-7000-8000-000000000499");

    public const string CashWithdrawalName = "Cash withdrawal";

    public static readonly (Guid Id, string Name)[] Starting =
    [
        (new Guid("0199c8b0-0000-7000-8000-000000000401"), "Electricity"),
        (new Guid("0199c8b0-0000-7000-8000-000000000402"), "Gas"),
        (new Guid("0199c8b0-0000-7000-8000-000000000403"), "Water and waste"),
        (new Guid("0199c8b0-0000-7000-8000-000000000404"), "Building reserve"),
        (new Guid("0199c8b0-0000-7000-8000-000000000405"), "Internet"),
        (new Guid("0199c8b0-0000-7000-8000-000000000406"), "Television"),
        (new Guid("0199c8b0-0000-7000-8000-000000000407"), "Telephone"),
        (new Guid("0199c8b0-0000-7000-8000-000000000408"), "Mobile phone"),
        (new Guid("0199c8b0-0000-7000-8000-000000000409"), "Gym membership"),
        (new Guid("0199c8b0-0000-7000-8000-000000000410"), "Other bills"),
        (new Guid("0199c8b0-0000-7000-8000-000000000421"), "Groceries"),
        (new Guid("0199c8b0-0000-7000-8000-000000000422"), "Personal care"),
        (new Guid("0199c8b0-0000-7000-8000-000000000423"), "Kindergarten"),
        (new Guid("0199c8b0-0000-7000-8000-000000000424"), "Household"),
        (new Guid("0199c8b0-0000-7000-8000-000000000425"), "Transport"),
        (new Guid("0199c8b0-0000-7000-8000-000000000426"), "Other expenses"),
    ];
}
