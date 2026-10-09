using Monthwise.Application.CashFlow;
using Monthwise.Application.Common;
using Monthwise.Domain.CardPurchases;
using Monthwise.Domain.Common;

namespace Monthwise.Application.CardPurchases;

/// <summary>The Purchase categories section in Settings (FR-10).</summary>
public sealed class PurchaseCategoryHandler(IPurchaseCategoryRepository categories, IUnitOfWork unitOfWork, TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<PurchaseCategoryResponse>> ListAsync(bool includeHidden, CancellationToken cancellationToken) =>
        (await categories.ListAsync(cancellationToken))
            .Where(c => includeHidden || !c.IsHidden)
            .OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(ToResponse)
            .ToList();

    public async Task<Result<PurchaseCategoryResponse>> CreateAsync(NameRequest request, CancellationToken cancellationToken)
    {
        var created = PurchaseCategory.Create(request?.Name, timeProvider.GetUtcNow());
        if (created.IsFailure)
        {
            return Result.Failure<PurchaseCategoryResponse>(created.Errors);
        }
        if (await categories.NameInUseAsync(created.Value.Name, null, cancellationToken))
        {
            return Result.Failure<PurchaseCategoryResponse>(CardPurchaseErrors.CategoryNameInUse);
        }
        categories.Add(created.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(ToResponse(created.Value));
    }

    public Task<Result<PurchaseCategoryResponse>> RenameAsync(Guid id, NameRequest request, CancellationToken cancellationToken) =>
        ChangeAsync(id, async category =>
        {
            var renamed = category.Rename(request?.Name);
            if (renamed.IsFailure)
            {
                return renamed;
            }
            return await categories.NameInUseAsync(category.Name, category.Id, cancellationToken)
                ? Result.Failure(CardPurchaseErrors.CategoryNameInUse)
                : Result.Success();
        }, cancellationToken);

    public Task<Result<PurchaseCategoryResponse>> HideAsync(Guid id, CancellationToken cancellationToken) =>
        ChangeAsync(id, category =>
        {
            category.Hide(timeProvider.Today());
            return Task.FromResult(Result.Success());
        }, cancellationToken);

    public Task<Result<PurchaseCategoryResponse>> ShowAsync(Guid id, CancellationToken cancellationToken) =>
        ChangeAsync(id, category =>
        {
            category.Show();
            return Task.FromResult(Result.Success());
        }, cancellationToken);

    /// <summary>Deletes an unused category; a used one can only be hidden.</summary>
    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var category = await categories.GetAsync(id, cancellationToken);
        if (category is null)
        {
            return Result.Failure(CardPurchaseErrors.CategoryNotFound);
        }
        if (await categories.IsUsedAsync(id, cancellationToken))
        {
            return Result.Failure(CardPurchaseErrors.CategoryInUse);
        }
        categories.Remove(category);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<Result<PurchaseCategoryResponse>> ChangeAsync(
        Guid id, Func<PurchaseCategory, Task<Result>> change, CancellationToken cancellationToken)
    {
        var category = await categories.GetAsync(id, cancellationToken);
        if (category is null)
        {
            return Result.Failure<PurchaseCategoryResponse>(CardPurchaseErrors.CategoryNotFound);
        }
        var result = await change(category);
        if (result.IsFailure)
        {
            return Result.Failure<PurchaseCategoryResponse>(result.Errors);
        }
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(ToResponse(category));
    }

    private static PurchaseCategoryResponse ToResponse(PurchaseCategory category) => new(category.Id, category.Name, category.IsHidden);
}
