using Monthwise.Domain.Common;

namespace Monthwise.Domain.Expenses;

/// <summary>What the user enters for an expense (FR-7, E1 to E5).</summary>
public sealed record ExpenseDetails(DateOnly Date, Money Amount, Guid CategoryId, Guid PaidFromId, string? Description);

/// <summary>
/// A one-time output recorded when it happens, e.g. a grocery shop (FR-7). It is always paid: from an
/// account, or with a credit card, where it becomes a line of the card payment. A cash withdrawal
/// (FR-8) is an expense with the fixed Cash withdrawal category, taken from a primary account.
/// </summary>
public sealed class Expense
{
    public const int DescriptionMaxLength = 100;

    private Expense()
    {
    }

    public Guid Id { get; private set; }
    public DateOnly Date { get; private set; }
    public Money Amount { get; private set; }
    public Guid CategoryId { get; private set; }
    public Guid PaidFromId { get; private set; }
    public string? Description { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public bool IsCashWithdrawal => CategoryId == ExpenseCategorySeedData.CashWithdrawalId;

    public ExpenseDetails Details => new(Date, Amount, CategoryId, PaidFromId, Description);

    /// <summary>
    /// Records an expense. The category (not Cash withdrawal) and what it was paid with are checked by
    /// the caller, which loads them.
    /// </summary>
    public static Result<Expense> Create(ExpenseDetails details, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(details);
        var errors = Validate(details, cashWithdrawal: false);
        if (errors.Count > 0)
        {
            return Result.Failure<Expense>(errors);
        }
        var expense = new Expense { Id = Guid.CreateVersion7(now), CreatedAt = now };
        expense.Apply(details, now);
        return Result.Success(expense);
    }

    /// <summary>Records a cash withdrawal (FR-8); the primary account is checked by the caller.</summary>
    public static Result<Expense> CreateCashWithdrawal(DateOnly date, Money amount, Guid accountId, string? description, DateTimeOffset now)
    {
        var details = new ExpenseDetails(date, amount, ExpenseCategorySeedData.CashWithdrawalId, accountId, description);
        var errors = Validate(details, cashWithdrawal: true);
        if (errors.Count > 0)
        {
            return Result.Failure<Expense>(errors);
        }
        var expense = new Expense { Id = Guid.CreateVersion7(now), CreatedAt = now };
        expense.Apply(details, now);
        return Result.Success(expense);
    }

    /// <summary>
    /// Edits the expense. A cash withdrawal stays a cash withdrawal and other expenses cannot become one,
    /// so the category of a cash withdrawal is kept.
    /// </summary>
    public Result Update(ExpenseDetails details, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(details);
        if (IsCashWithdrawal)
        {
            details = details with { CategoryId = ExpenseCategorySeedData.CashWithdrawalId };
        }
        var errors = Validate(details, IsCashWithdrawal);
        if (errors.Count > 0)
        {
            return Result.Failure(errors);
        }
        Apply(details, now);
        return Result.Success();
    }

    private static List<DomainError> Validate(ExpenseDetails details, bool cashWithdrawal)
    {
        var errors = new List<DomainError>();
        if (!details.Amount.IsPositive)
        {
            errors.Add(ExpenseErrors.AmountNotPositive);
        }
        if (details.CategoryId == Guid.Empty)
        {
            errors.Add(ExpenseErrors.CategoryRequired);
        }
        else if (!cashWithdrawal && details.CategoryId == ExpenseCategorySeedData.CashWithdrawalId)
        {
            errors.Add(ExpenseErrors.CategoryCashWithdrawal);
        }
        if (details.PaidFromId == Guid.Empty)
        {
            errors.Add(ExpenseErrors.PaidFromRequired);
        }
        if (Text.Normalize(details.Description)?.Length > DescriptionMaxLength)
        {
            errors.Add(ExpenseErrors.ExpenseDescriptionTooLong);
        }
        return errors;
    }

    private void Apply(ExpenseDetails details, DateTimeOffset now)
    {
        Date = details.Date;
        Amount = details.Amount;
        CategoryId = details.CategoryId;
        PaidFromId = details.PaidFromId;
        Description = Text.Normalize(details.Description);
        UpdatedAt = now;
    }
}
