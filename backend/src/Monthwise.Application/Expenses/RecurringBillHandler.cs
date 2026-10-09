using Monthwise.Application.Common;
using Monthwise.Domain.Common;
using Monthwise.Domain.Expenses;

namespace Monthwise.Application.Expenses;

/// <summary>The recurring bills page (FR-2): list, add, edit, stop, restart and delete.</summary>
public sealed class RecurringBillHandler(
    IRecurringBillRepository recurringBills,
    IBillRepository bills,
    ExpenseInputChecker checker,
    ExpenseLedger ledger,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    /// <summary>Active recurring bills by name, then stopped ones when <paramref name="includeStopped"/>.</summary>
    public async Task<IReadOnlyList<RecurringBillResponse>> ListAsync(bool includeStopped, CancellationToken cancellationToken)
    {
        var book = await ledger.LoadAsync(cancellationToken);
        var used = book.Bills.Select(b => b.RecurringBillId).ToHashSet();
        return (await recurringBills.ListAsync(cancellationToken))
            .Where(r => includeStopped || r.IsActive)
            .OrderBy(r => r.IsActive ? 0 : 1)
            .ThenBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(r => ToResponse(book, r, canBeDeleted: !used.Contains(r.Id)))
            .ToList();
    }

    public async Task<Result<RecurringBillResponse>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var recurringBill = await recurringBills.GetAsync(id, cancellationToken);
        return recurringBill is null
            ? Result.Failure<RecurringBillResponse>(ExpenseErrors.RecurringBillNotFound)
            : Result.Success(await ResponseAsync(recurringBill, cancellationToken));
    }

    public async Task<Result<RecurringBillResponse>> CreateAsync(RecurringBillRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var details = ToDetails(request);
        var created = RecurringBill.Create(details, timeProvider.GetUtcNow());
        var errors = created.IsFailure ? created.Errors.ToList() : [];
        errors.AddRange(await CheckAsync(details, existing: null, cancellationToken));
        if (errors.Count > 0)
        {
            return Result.Failure<RecurringBillResponse>(errors);
        }
        recurringBills.Add(created.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(await ResponseAsync(created.Value, cancellationToken));
    }

    public async Task<Result<RecurringBillResponse>> UpdateAsync(Guid id, RecurringBillRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var recurringBill = await recurringBills.GetAsync(id, cancellationToken);
        if (recurringBill is null)
        {
            return Result.Failure<RecurringBillResponse>(ExpenseErrors.RecurringBillNotFound);
        }
        var details = ToDetails(request);
        var errors = await CheckAsync(details, recurringBill, cancellationToken);
        var updated = recurringBill.Update(details, timeProvider.GetUtcNow());
        errors.InsertRange(0, updated.Errors);
        if (errors.Count > 0)
        {
            return Result.Failure<RecurringBillResponse>(errors);
        }
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(await ResponseAsync(recurringBill, cancellationToken));
    }

    public Task<Result<RecurringBillResponse>> StopAsync(Guid id, CancellationToken cancellationToken) =>
        ChangeAsync(id, r =>
        {
            r.Stop(timeProvider.Today(), timeProvider.GetUtcNow());
            return Task.FromResult(Result.Success());
        }, cancellationToken);

    /// <summary>Restarts a stopped recurring bill; its name must still be unique among the active ones.</summary>
    public Task<Result<RecurringBillResponse>> RestartAsync(Guid id, CancellationToken cancellationToken) =>
        ChangeAsync(id, async r =>
        {
            if (!r.IsActive && await recurringBills.NameInUseAsync(r.Name, r.Id, cancellationToken))
            {
                return Result.Failure(ExpenseErrors.NameInUse);
            }
            r.Restart(timeProvider.GetUtcNow());
            return Result.Success();
        }, cancellationToken);

    /// <summary>Deletes a recurring bill that was never added to a month; otherwise it can only be stopped.</summary>
    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var recurringBill = await recurringBills.GetAsync(id, cancellationToken);
        if (recurringBill is null)
        {
            return Result.Failure(ExpenseErrors.RecurringBillNotFound);
        }
        if (await bills.AnyForRecurringBillAsync(id, cancellationToken))
        {
            return Result.Failure(ExpenseErrors.RecurringBillInUse);
        }
        recurringBills.Remove(recurringBill);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<List<DomainError>> CheckAsync(RecurringBillDetails details, RecurringBill? existing, CancellationToken cancellationToken)
    {
        var errors = await checker.CheckAsync(
            details.CategoryId,
            existing?.CategoryId,
            details.PaidFromId,
            existing?.PaidFromId,
            timeProvider.Today(),
            primaryOnly: false,
            cancellationToken);
        var name = details.Name?.Trim();
        if (!string.IsNullOrEmpty(name) && (existing?.IsActive ?? true)
            && await recurringBills.NameInUseAsync(name, existing?.Id, cancellationToken))
        {
            errors.Insert(0, ExpenseErrors.NameInUse);
        }
        return errors;
    }

    private async Task<Result<RecurringBillResponse>> ChangeAsync(
        Guid id, Func<RecurringBill, Task<Result>> change, CancellationToken cancellationToken)
    {
        var recurringBill = await recurringBills.GetAsync(id, cancellationToken);
        if (recurringBill is null)
        {
            return Result.Failure<RecurringBillResponse>(ExpenseErrors.RecurringBillNotFound);
        }
        var result = await change(recurringBill);
        if (result.IsFailure)
        {
            return Result.Failure<RecurringBillResponse>(result.Errors);
        }
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(await ResponseAsync(recurringBill, cancellationToken));
    }

    private async Task<RecurringBillResponse> ResponseAsync(RecurringBill recurringBill, CancellationToken cancellationToken) =>
        ToResponse(
            await ledger.LoadAsync(cancellationToken),
            recurringBill,
            canBeDeleted: !await bills.AnyForRecurringBillAsync(recurringBill.Id, cancellationToken));

    private static RecurringBillResponse ToResponse(ExpenseBook book, RecurringBill r, bool canBeDeleted) =>
        new(r.Id, r.Name, book.Category(r.CategoryId), r.Payee, book.PaidFrom(r.PaidFromId), r.Description, r.IsActive, r.StoppedOn, canBeDeleted);

    private static RecurringBillDetails ToDetails(RecurringBillRequest request) =>
        new(request.Name, request.CategoryId, request.Payee, request.PaidFromId, request.Description);
}
