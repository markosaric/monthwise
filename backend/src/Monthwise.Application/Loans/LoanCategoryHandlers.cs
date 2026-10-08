using Monthwise.Application.CashFlow;
using Monthwise.Application.Common;
using Monthwise.Domain.Common;
using Monthwise.Domain.Loans;

namespace Monthwise.Application.Loans;

/// <summary>The Loan categories section in Settings (FR-8): categories with their subcategories.</summary>
public sealed class LoanCategoryHandler(
    ILoanCategoryRepository categories,
    ILoanSubcategoryRepository subcategories,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<LoanCategoryResponse>> ListAsync(bool includeHidden, CancellationToken cancellationToken)
    {
        var bySubcategory = (await subcategories.ListAsync(cancellationToken)).ToLookup(s => s.CategoryId);
        return (await categories.ListAsync(cancellationToken))
            .Where(c => includeHidden || !c.IsHidden)
            .OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(c => ToResponse(c, bySubcategory[c.Id].Where(s => includeHidden || !s.IsHidden)))
            .ToList();
    }

    public async Task<Result<LoanCategoryResponse>> CreateAsync(LoanCategoryRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var created = LoanCategory.Create(request.Name, request.AllowsResidualValue, timeProvider.GetUtcNow());
        if (created.IsFailure)
        {
            return Result.Failure<LoanCategoryResponse>(created.Errors);
        }
        if (await categories.NameInUseAsync(created.Value.Name, null, cancellationToken))
        {
            return Result.Failure<LoanCategoryResponse>(LoanErrors.CategoryNameInUse);
        }
        categories.Add(created.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(ToResponse(created.Value, []));
    }

    /// <summary>Renames the category and sets Allows residual value; loans that already have a residual value keep it.</summary>
    public Task<Result<LoanCategoryResponse>> UpdateAsync(Guid id, LoanCategoryRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ChangeAsync(id, async category =>
        {
            var renamed = category.Rename(request.Name);
            if (renamed.IsFailure)
            {
                return renamed;
            }
            if (await categories.NameInUseAsync(category.Name, category.Id, cancellationToken))
            {
                return Result.Failure(LoanErrors.CategoryNameInUse);
            }
            category.SetAllowsResidualValue(request.AllowsResidualValue);
            return Result.Success();
        }, cancellationToken);
    }

    public Task<Result<LoanCategoryResponse>> HideAsync(Guid id, CancellationToken cancellationToken) =>
        ChangeAsync(id, category =>
        {
            category.Hide(timeProvider.Today());
            return Task.FromResult(Result.Success());
        }, cancellationToken);

    public Task<Result<LoanCategoryResponse>> ShowAsync(Guid id, CancellationToken cancellationToken) =>
        ChangeAsync(id, category =>
        {
            category.Show();
            return Task.FromResult(Result.Success());
        }, cancellationToken);

    /// <summary>Deletes an unused category with its unused subcategories; a used one can only be hidden.</summary>
    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var category = await categories.GetAsync(id, cancellationToken);
        if (category is null)
        {
            return Result.Failure(LoanErrors.CategoryNotFound);
        }
        if (await categories.IsUsedAsync(id, cancellationToken))
        {
            return Result.Failure(LoanErrors.CategoryInUse);
        }
        foreach (var subcategory in (await subcategories.ListAsync(cancellationToken)).Where(s => s.CategoryId == id))
        {
            subcategories.Remove(subcategory);
        }
        categories.Remove(category);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<LoanSubcategoryResponse>> CreateSubcategoryAsync(
        Guid categoryId, NameRequest request, CancellationToken cancellationToken)
    {
        if (await categories.GetAsync(categoryId, cancellationToken) is null)
        {
            return Result.Failure<LoanSubcategoryResponse>(LoanErrors.CategoryNotFound);
        }
        var created = LoanSubcategory.Create(categoryId, request?.Name, timeProvider.GetUtcNow());
        if (created.IsFailure)
        {
            return Result.Failure<LoanSubcategoryResponse>(created.Errors);
        }
        if (await subcategories.NameInUseAsync(categoryId, created.Value.Name, null, cancellationToken))
        {
            return Result.Failure<LoanSubcategoryResponse>(LoanErrors.SubcategoryNameInUse);
        }
        subcategories.Add(created.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(ToResponse(created.Value));
    }

    public Task<Result<LoanSubcategoryResponse>> RenameSubcategoryAsync(Guid id, NameRequest request, CancellationToken cancellationToken) =>
        ChangeSubcategoryAsync(id, async subcategory =>
        {
            var renamed = subcategory.Rename(request?.Name);
            if (renamed.IsFailure)
            {
                return renamed;
            }
            return await subcategories.NameInUseAsync(subcategory.CategoryId, subcategory.Name, subcategory.Id, cancellationToken)
                ? Result.Failure(LoanErrors.SubcategoryNameInUse)
                : Result.Success();
        }, cancellationToken);

    public Task<Result<LoanSubcategoryResponse>> HideSubcategoryAsync(Guid id, CancellationToken cancellationToken) =>
        ChangeSubcategoryAsync(id, subcategory =>
        {
            subcategory.Hide(timeProvider.Today());
            return Task.FromResult(Result.Success());
        }, cancellationToken);

    public Task<Result<LoanSubcategoryResponse>> ShowSubcategoryAsync(Guid id, CancellationToken cancellationToken) =>
        ChangeSubcategoryAsync(id, subcategory =>
        {
            subcategory.Show();
            return Task.FromResult(Result.Success());
        }, cancellationToken);

    public async Task<Result> DeleteSubcategoryAsync(Guid id, CancellationToken cancellationToken)
    {
        var subcategory = await subcategories.GetAsync(id, cancellationToken);
        if (subcategory is null)
        {
            return Result.Failure(LoanErrors.SubcategoryNotFound);
        }
        if (await subcategories.IsUsedAsync(id, cancellationToken))
        {
            return Result.Failure(LoanErrors.SubcategoryInUse);
        }
        subcategories.Remove(subcategory);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<Result<LoanCategoryResponse>> ChangeAsync(
        Guid id, Func<LoanCategory, Task<Result>> change, CancellationToken cancellationToken)
    {
        var category = await categories.GetAsync(id, cancellationToken);
        if (category is null)
        {
            return Result.Failure<LoanCategoryResponse>(LoanErrors.CategoryNotFound);
        }
        var result = await change(category);
        if (result.IsFailure)
        {
            return Result.Failure<LoanCategoryResponse>(result.Errors);
        }
        await unitOfWork.SaveChangesAsync(cancellationToken);
        var own = (await subcategories.ListAsync(cancellationToken)).Where(s => s.CategoryId == id);
        return Result.Success(ToResponse(category, own));
    }

    private async Task<Result<LoanSubcategoryResponse>> ChangeSubcategoryAsync(
        Guid id, Func<LoanSubcategory, Task<Result>> change, CancellationToken cancellationToken)
    {
        var subcategory = await subcategories.GetAsync(id, cancellationToken);
        if (subcategory is null)
        {
            return Result.Failure<LoanSubcategoryResponse>(LoanErrors.SubcategoryNotFound);
        }
        var result = await change(subcategory);
        if (result.IsFailure)
        {
            return Result.Failure<LoanSubcategoryResponse>(result.Errors);
        }
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(ToResponse(subcategory));
    }

    private static LoanCategoryResponse ToResponse(LoanCategory category, IEnumerable<LoanSubcategory> own) =>
        new(
            category.Id,
            category.Name,
            category.AllowsResidualValue,
            category.IsPredefined,
            category.IsHidden,
            own.OrderBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase).Select(ToResponse).ToList());

    private static LoanSubcategoryResponse ToResponse(LoanSubcategory subcategory) =>
        new(subcategory.Id, subcategory.CategoryId, subcategory.Name, subcategory.IsPredefined, subcategory.IsHidden);
}
