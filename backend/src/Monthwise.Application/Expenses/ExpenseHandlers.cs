using Monthwise.Application.Common;
using Monthwise.Domain.Common;
using Monthwise.Domain.Expenses;

namespace Monthwise.Application.Expenses;

/// <summary>The expenses of a month for the Month screen, newest first, with totals (FR-7, FR-8, FR-9).</summary>
public sealed class GetMonthExpensesHandler(IExpenseRepository expenses, ExpenseLedger ledger)
{
    public async Task<MonthExpensesResponse> HandleAsync(YearMonth month, CancellationToken cancellationToken)
    {
        var book = await ledger.LoadAsync(cancellationToken);
        var monthExpenses = await expenses.ListBetweenAsync(month.FirstDay, month.LastDay, cancellationToken);
        return new MonthExpensesResponse(
            month.ToString(),
            monthExpenses
                .OrderByDescending(e => e.Date)
                .ThenByDescending(e => e.CreatedAt)
                .Select(book.ToResponse)
                .ToList(),
            book.ExpenseTotals(monthExpenses));
    }
}

/// <summary>Records, edits and deletes expenses and cash withdrawals (FR-7, FR-8).</summary>
public sealed class ExpenseHandler(
    IExpenseRepository expenses,
    ExpenseInputChecker checker,
    ExpenseLedger ledger,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<Result<ExpenseResponse>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var expense = await expenses.GetAsync(id, cancellationToken);
        return expense is null
            ? Result.Failure<ExpenseResponse>(ExpenseErrors.ExpenseNotFound)
            : Result.Success((await ledger.LoadAsync(cancellationToken)).ToResponse(expense));
    }

    /// <summary>What the last expense was paid with, to prefill a new one (E4).</summary>
    public async Task<LastPaidFromResponse> LastPaidFromAsync(CancellationToken cancellationToken)
    {
        var latest = await expenses.LatestAsync(cancellationToken);
        return new LastPaidFromResponse(latest is null ? null : (await ledger.LoadAsync(cancellationToken)).PaidFrom(latest.PaidFromId));
    }

    public async Task<Result<ExpenseResponse>> CreateAsync(ExpenseRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var details = ToDetails(request);
        var created = Expense.Create(details, timeProvider.GetUtcNow());
        return await AddAsync(created, await CheckAsync(details, existing: null, cancellationToken), cancellationToken);
    }

    public async Task<Result<ExpenseResponse>> CreateCashWithdrawalAsync(CashWithdrawalRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var created = Expense.CreateCashWithdrawal(
            request.Date, Money.FromCents(request.AmountCents), request.AccountId, request.Description, timeProvider.GetUtcNow());
        return await AddAsync(created, await CheckCashWithdrawalAsync(request, existing: null, cancellationToken), cancellationToken);
    }

    /// <summary>Edits an expense; a cash withdrawal keeps its category (FR-8).</summary>
    public Task<Result<ExpenseResponse>> UpdateAsync(Guid id, ExpenseRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ChangeAsync(id, async expense =>
        {
            var details = ToDetails(request);
            if (expense.IsCashWithdrawal)
            {
                details = details with { CategoryId = expense.CategoryId };
            }
            var errors = expense.IsCashWithdrawal
                ? await CheckCashWithdrawalAsync(
                    new CashWithdrawalRequest(request.Date, request.AmountCents, request.PaidFromId, request.Description), expense, cancellationToken)
                : await CheckAsync(details, expense, cancellationToken);
            var updated = expense.Update(details, timeProvider.GetUtcNow());
            errors.InsertRange(0, updated.Errors);
            return errors;
        }, cancellationToken);
    }

    public Task<Result<ExpenseResponse>> UpdateCashWithdrawalAsync(Guid id, CashWithdrawalRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return UpdateAsync(
            id,
            new ExpenseRequest(request.Date, request.AmountCents, ExpenseCategorySeedData.CashWithdrawalId, request.AccountId, request.Description),
            cancellationToken);
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var expense = await expenses.GetAsync(id, cancellationToken);
        if (expense is null)
        {
            return Result.Failure(ExpenseErrors.ExpenseNotFound);
        }
        expenses.Remove(expense);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<Result<ExpenseResponse>> AddAsync(Result<Expense> created, List<DomainError> checks, CancellationToken cancellationToken)
    {
        var errors = created.IsFailure ? created.Errors.ToList() : [];
        errors.AddRange(checks);
        if (errors.Count > 0)
        {
            return Result.Failure<ExpenseResponse>(errors);
        }
        expenses.Add(created.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success((await ledger.LoadAsync(cancellationToken)).ToResponse(created.Value));
    }

    private async Task<Result<ExpenseResponse>> ChangeAsync(
        Guid id, Func<Expense, Task<List<DomainError>>> change, CancellationToken cancellationToken)
    {
        var expense = await expenses.GetAsync(id, cancellationToken);
        if (expense is null)
        {
            return Result.Failure<ExpenseResponse>(ExpenseErrors.ExpenseNotFound);
        }
        var errors = await change(expense);
        if (errors.Count > 0)
        {
            return Result.Failure<ExpenseResponse>(errors);
        }
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success((await ledger.LoadAsync(cancellationToken)).ToResponse(expense));
    }

    private Task<List<DomainError>> CheckAsync(ExpenseDetails details, Expense? existing, CancellationToken cancellationToken) =>
        checker.CheckAsync(
            details.CategoryId,
            existing?.CategoryId,
            details.PaidFromId,
            existing?.PaidFromId,
            details.Date,
            primaryOnly: false,
            cancellationToken);

    private Task<List<DomainError>> CheckCashWithdrawalAsync(CashWithdrawalRequest request, Expense? existing, CancellationToken cancellationToken) =>
        checker.CheckAsync(
            Guid.Empty,
            null,
            request.AccountId,
            existing?.PaidFromId,
            request.Date,
            primaryOnly: true,
            cancellationToken);

    private static ExpenseDetails ToDetails(ExpenseRequest request) =>
        new(request.Date, Money.FromCents(request.AmountCents), request.CategoryId, request.PaidFromId, request.Description);
}
