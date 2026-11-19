using Monthwise.Application.Common;
using Monthwise.Application.Localization;
using Monthwise.Domain.CashFlow;
using Monthwise.Domain.Common;

namespace Monthwise.Application.CashFlow;

/// <summary>Saved payers for the autocomplete and the Payers section in Settings (FR-5).</summary>
public sealed class PayerHandler(IPayerRepository payers, IUnitOfWork unitOfWork, TimeProvider timeProvider)
{
    /// <summary>Payers that are not removed, by name; <paramref name="search"/> keeps those whose name contains it.</summary>
    public async Task<IReadOnlyList<PayerResponse>> ListAsync(string? search, CancellationToken cancellationToken)
    {
        var term = search?.Trim();
        return (await payers.ListAsync(cancellationToken))
            .Where(p => !p.IsRemoved)
            .Where(p => string.IsNullOrEmpty(term) || p.Name.Contains(term, StringComparison.CurrentCultureIgnoreCase))
            .OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(ToResponse)
            .ToList();
    }

    public async Task<Result<PayerResponse>> RenameAsync(Guid id, NameRequest request, CancellationToken cancellationToken)
    {
        var payer = await payers.GetAsync(id, cancellationToken);
        if (payer is null)
        {
            return Result.Failure<PayerResponse>(CashFlowErrors.PayerNotFound);
        }
        var renamed = payer.Rename(request?.Name);
        if (renamed.IsFailure)
        {
            return Result.Failure<PayerResponse>(renamed.Errors);
        }
        var sameName = await payers.FindByNameAsync(payer.Name, cancellationToken);
        if (sameName is not null && sameName.Id != payer.Id)
        {
            return Result.Failure<PayerResponse>(CashFlowErrors.PayerNameInUse);
        }
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(ToResponse(payer));
    }

    /// <summary>Hides the payer from suggestions; inputs keep showing its name.</summary>
    public async Task<Result> RemoveAsync(Guid id, CancellationToken cancellationToken)
    {
        var payer = await payers.GetAsync(id, cancellationToken);
        if (payer is null)
        {
            return Result.Failure(CashFlowErrors.PayerNotFound);
        }
        payer.Remove(timeProvider.Today());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private static PayerResponse ToResponse(Payer payer) => new(payer.Id, payer.Name, payer.IsRemoved);
}

/// <summary>The Input categories section in Settings (FR-6).</summary>
public sealed class InputCategoryHandler(IInputCategoryRepository categories, IUnitOfWork unitOfWork, TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<InputCategoryResponse>> ListAsync(bool includeHidden, CancellationToken cancellationToken) =>
        (await categories.ListAsync(cancellationToken))
            .Where(c => includeHidden || !c.IsHidden)
            .OrderBy(c => PredefinedNames.Display(c.Id, c.Name), StringComparer.CurrentCultureIgnoreCase)
            .Select(ToResponse)
            .ToList();

    public async Task<Result<InputCategoryResponse>> CreateAsync(NameRequest request, CancellationToken cancellationToken)
    {
        var created = InputCategory.Create(request?.Name, timeProvider.GetUtcNow());
        if (created.IsFailure)
        {
            return Result.Failure<InputCategoryResponse>(created.Errors);
        }
        if (await categories.NameInUseAsync(created.Value.Name, null, cancellationToken)
            || await NameClashesAsync(created.Value.Name, null, cancellationToken))
        {
            return Result.Failure<InputCategoryResponse>(CashFlowErrors.CategoryNameInUse);
        }
        categories.Add(created.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(ToResponse(created.Value));
    }

    public Task<Result<InputCategoryResponse>> RenameAsync(Guid id, NameRequest request, CancellationToken cancellationToken) =>
        ChangeAsync(id, async category =>
        {
            var renamed = category.Rename(request?.Name);
            if (renamed.IsFailure)
            {
                return renamed;
            }
            return await categories.NameInUseAsync(category.Name, category.Id, cancellationToken)
                || await NameClashesAsync(category.Name, category.Id, cancellationToken)
                ? Result.Failure(CashFlowErrors.CategoryNameInUse)
                : Result.Success();
        }, cancellationToken);

    public Task<Result<InputCategoryResponse>> HideAsync(Guid id, CancellationToken cancellationToken) =>
        ChangeAsync(id, category =>
        {
            category.Hide(timeProvider.Today());
            return Task.FromResult(Result.Success());
        }, cancellationToken);

    public Task<Result<InputCategoryResponse>> ShowAsync(Guid id, CancellationToken cancellationToken) =>
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
            return Result.Failure(CashFlowErrors.CategoryNotFound);
        }
        if (await categories.IsUsedAsync(id, cancellationToken))
        {
            return Result.Failure(CashFlowErrors.CategoryInUse);
        }
        categories.Remove(category);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<Result<InputCategoryResponse>> ChangeAsync(
        Guid id, Func<InputCategory, Task<Result>> change, CancellationToken cancellationToken)
    {
        var category = await categories.GetAsync(id, cancellationToken);
        if (category is null)
        {
            return Result.Failure<InputCategoryResponse>(CashFlowErrors.CategoryNotFound);
        }
        var result = await change(category);
        if (result.IsFailure)
        {
            return Result.Failure<InputCategoryResponse>(result.Errors);
        }
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(ToResponse(category));
    }

    /// <summary>Whether the name is another category's predefined name in Croatian (croatian-language.md).</summary>
    private async Task<bool> NameClashesAsync(string name, Guid? exceptId, CancellationToken cancellationToken) =>
        PredefinedNames.ClashesWithTranslation(
            name, exceptId, (await categories.ListAsync(cancellationToken)).Select(c => (c.Id, c.Name)));

    private static InputCategoryResponse ToResponse(InputCategory category) =>
        new(category.Id, PredefinedNames.Display(category.Id, category.Name), category.IsPredefined, category.IsHidden);
}
