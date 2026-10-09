using Monthwise.Application.CashFlow;
using Monthwise.Application.Common;
using Monthwise.Domain.Common;
using Monthwise.Domain.Expenses;

namespace Monthwise.Application.Expenses;

/// <summary>
/// The Expense categories section in Settings (bills and expenses FR-1, installments Appendix 1): one
/// list for card purchases, bills, recurring bills and expenses.
/// </summary>
public sealed class ExpenseCategoryHandler(IExpenseCategoryRepository categories, IUnitOfWork unitOfWork, TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<ExpenseCategoryResponse>> ListAsync(bool includeHidden, CancellationToken cancellationToken) =>
        (await categories.ListAsync(cancellationToken))
            .Where(c => includeHidden || !c.IsHidden)
            .OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(ToResponse)
            .ToList();

    public async Task<Result<ExpenseCategoryResponse>> CreateAsync(NameRequest request, CancellationToken cancellationToken)
    {
        var created = ExpenseCategory.Create(request?.Name, timeProvider.GetUtcNow());
        if (created.IsFailure)
        {
            return Result.Failure<ExpenseCategoryResponse>(created.Errors);
        }
        if (await categories.NameInUseAsync(created.Value.Name, null, cancellationToken))
        {
            return Result.Failure<ExpenseCategoryResponse>(ExpenseErrors.CategoryNameInUse);
        }
        categories.Add(created.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(ToResponse(created.Value));
    }

    public Task<Result<ExpenseCategoryResponse>> RenameAsync(Guid id, NameRequest request, CancellationToken cancellationToken) =>
        ChangeAsync(id, async category =>
        {
            var renamed = category.Rename(request?.Name);
            if (renamed.IsFailure)
            {
                return renamed;
            }
            return await categories.NameInUseAsync(category.Name, category.Id, cancellationToken)
                ? Result.Failure(ExpenseErrors.CategoryNameInUse)
                : Result.Success();
        }, cancellationToken);

    public Task<Result<ExpenseCategoryResponse>> HideAsync(Guid id, CancellationToken cancellationToken) =>
        ChangeAsync(id, category => Task.FromResult(category.Hide(timeProvider.Today())), cancellationToken);

    public Task<Result<ExpenseCategoryResponse>> ShowAsync(Guid id, CancellationToken cancellationToken) =>
        ChangeAsync(id, category =>
        {
            category.Show();
            return Task.FromResult(Result.Success());
        }, cancellationToken);

    /// <summary>Deletes an unused category; a used one can only be hidden, and Cash withdrawal stays.</summary>
    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var category = await categories.GetAsync(id, cancellationToken);
        if (category is null)
        {
            return Result.Failure(ExpenseErrors.CategoryNotFound);
        }
        if (category.IsFixed)
        {
            return Result.Failure(ExpenseErrors.CategoryFixed);
        }
        if (await categories.IsUsedAsync(id, cancellationToken))
        {
            return Result.Failure(ExpenseErrors.CategoryInUse);
        }
        categories.Remove(category);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<Result<ExpenseCategoryResponse>> ChangeAsync(
        Guid id, Func<ExpenseCategory, Task<Result>> change, CancellationToken cancellationToken)
    {
        var category = await categories.GetAsync(id, cancellationToken);
        if (category is null)
        {
            return Result.Failure<ExpenseCategoryResponse>(ExpenseErrors.CategoryNotFound);
        }
        var result = await change(category);
        if (result.IsFailure)
        {
            return Result.Failure<ExpenseCategoryResponse>(result.Errors);
        }
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(ToResponse(category));
    }

    private static ExpenseCategoryResponse ToResponse(ExpenseCategory category) =>
        new(category.Id, category.Name, category.IsPredefined, category.IsFixed, category.IsHidden);
}
