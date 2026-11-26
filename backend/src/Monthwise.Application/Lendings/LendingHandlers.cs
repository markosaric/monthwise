using Monthwise.Application.Cards;
using Monthwise.Application.CashFlow;
using Monthwise.Application.Common;
using Monthwise.Domain.Cards;
using Monthwise.Domain.CashFlow;
using Monthwise.Domain.Common;
using Monthwise.Domain.Expenses;
using Monthwise.Domain.Lendings;

namespace Monthwise.Application.Lendings;

/// <summary>Which lendings the Lending screen shows (FR-1).</summary>
public enum LendingFilter
{
    /// <summary>Open and overdue lendings (the default).</summary>
    Open,

    /// <summary>Returned lendings, with their repayments.</summary>
    Returned,
}

/// <summary>The Lending screen and the totals per borrower (money lending FR-1, FR-6).</summary>
public sealed class GetLendingsHandler(LendingLedger ledger)
{
    public async Task<LendingListResponse> HandleAsync(LendingFilter filter, CancellationToken cancellationToken)
    {
        var book = await ledger.LoadAsync(cancellationToken);
        var notReturned = book.Lendings.Where(l => l.StateOn(book.Today) != LendingState.Returned).ToList();
        var shown = filter == LendingFilter.Returned
            ? book.Lendings
                .Where(l => l.StateOn(book.Today) == LendingState.Returned)
                .OrderByDescending(l => l.Repayments.Count == 0 ? l.LendDate : l.Repayments.Max(r => r.ReceivedDate))
                .ThenByDescending(l => l.LendDate)
                .ToList()
            : notReturned
                .OrderBy(l => l.ExpectedReturnDate)
                .ThenBy(l => book.Borrower(l.PayerId).Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        return new LendingListResponse(
            shown.Select(book.ToResponse).ToList(),
            Money.Sum(notReturned.Select(l => l.Outstanding)).Cents,
            Money.Sum(notReturned.Where(book.IsOverdue).Select(l => l.Outstanding)).Cents);
    }

    /// <summary>Every borrower with a lending: lent, returned and outstanding; those who still owe something first.</summary>
    public async Task<IReadOnlyList<BorrowerTotalsResponse>> BorrowersAsync(CancellationToken cancellationToken)
    {
        var book = await ledger.LoadAsync(cancellationToken);
        return book.Lendings
            .GroupBy(l => l.PayerId)
            .Select(g => new BorrowerTotalsResponse(
                g.Key,
                book.Borrower(g.Key).Name,
                Money.Sum(g.Select(l => l.Amount)).Cents,
                Money.Sum(g.Select(l => l.Returned)).Cents,
                Money.Sum(g.Select(l => l.Outstanding)).Cents,
                g.Any(book.IsOverdue)))
            .OrderByDescending(b => b.OutstandingCents > 0)
            .ThenBy(b => b.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }
}

/// <summary>The month's lending for the Month screen (FR-9).</summary>
public sealed class GetMonthLendingHandler(LendingLedger ledger)
{
    public async Task<MonthLendingResponse> HandleAsync(YearMonth month, CancellationToken cancellationToken)
    {
        var book = await ledger.LoadAsync(cancellationToken);
        var byId = book.Lendings.ToDictionary(l => l.Id);
        var lent = book.OutputsBetween(month.FirstDay, month.LastDay)
            .Select(o => new MonthLendingEntryResponse(o.LendingId, null, book.Borrower(o.PayerId), o.Date, o.Amount.Cents, book.Account(o.AccountId), false))
            .OrderBy(e => e.Date)
            .ThenBy(e => e.Borrower.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        var inputs = book.InputsBetween(month.FirstDay, month.LastDay).ToList();
        var returned = inputs.Where(i => i.IsReceived)
            .Select(i => new MonthLendingEntryResponse(i.LendingId, i.RepaymentId, book.Borrower(i.PayerId), i.Date, i.Amount.Cents, book.Account(i.AccountId), false))
            .OrderBy(e => e.Date)
            .ThenBy(e => e.Borrower.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        var expectedBack = inputs.Where(i => !i.IsReceived)
            .Select(i => new MonthLendingEntryResponse(
                i.LendingId, null, book.Borrower(i.PayerId), i.Date, i.Amount.Cents, book.Account(i.AccountId), book.IsOverdue(byId[i.LendingId])))
            .OrderBy(e => e.Date)
            .ThenBy(e => e.Borrower.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        return new MonthLendingResponse(
            month.ToString(),
            lent,
            returned,
            expectedBack,
            lent.Sum(e => e.AmountCents),
            returned.Sum(e => e.AmountCents),
            expectedBack.Sum(e => e.AmountCents));
    }
}

/// <summary>Records, edits and deletes lendings and their repayments (FR-2 to FR-4).</summary>
public sealed class LendingHandler(
    ILendingRepository lendings,
    IPayerRepository payers,
    ICardRepository cards,
    LendingLedger ledger,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<Result<LendingResponse>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var lending = await lendings.GetAsync(id, cancellationToken);
        return lending is null
            ? Result.Failure<LendingResponse>(LendingErrors.LendingNotFound)
            : Result.Success(await RespondAsync(lending, cancellationToken));
    }

    public async Task<Result<LendingResponse>> CreateAsync(LendingRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var errors = new List<DomainError>();
        var name = Payer.ValidateName(request.BorrowerName, LendingErrors.BorrowerRequired, LendingErrors.BorrowerTooLong);
        errors.AddRange(name.Errors);
        var details = ToDetails(request, Guid.Empty);
        var created = Lending.Create(details, timeProvider.GetUtcNow());
        errors.AddRange(created.Errors);
        errors.AddRange(await CheckAccountAsync(request.FromAccountId, null, from: true, cancellationToken));
        if (errors.Count > 0)
        {
            return Result.Failure<LendingResponse>(errors.Distinct());
        }
        var payer = await FindOrCreatePayerAsync(name.Value, cancellationToken);
        created.Value.Update(details with { PayerId = payer.Id }, timeProvider.GetUtcNow());
        lendings.Add(created.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(await RespondAsync(created.Value, cancellationToken));
    }

    public async Task<Result<LendingResponse>> UpdateAsync(Guid id, LendingRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var lending = await lendings.GetAsync(id, cancellationToken);
        if (lending is null)
        {
            return Result.Failure<LendingResponse>(LendingErrors.LendingNotFound);
        }
        var errors = new List<DomainError>();
        var name = Payer.ValidateName(request.BorrowerName, LendingErrors.BorrowerRequired, LendingErrors.BorrowerTooLong);
        errors.AddRange(name.Errors);
        errors.AddRange(await CheckAccountAsync(request.FromAccountId, lending.FromAccountId, from: true, cancellationToken));
        if (errors.Count > 0)
        {
            // Run the lending rules too, so every problem is reported at once.
            errors.InsertRange(0, Lending.Create(ToDetails(request, Guid.Empty), timeProvider.GetUtcNow()).Errors);
            return Result.Failure<LendingResponse>(errors.Distinct());
        }
        var payer = await FindOrCreatePayerAsync(name.Value, cancellationToken);
        var updated = lending.Update(ToDetails(request, payer.Id), timeProvider.GetUtcNow());
        if (updated.IsFailure)
        {
            return Result.Failure<LendingResponse>(updated.Errors);
        }
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(await RespondAsync(lending, cancellationToken));
    }

    /// <summary>Deletes the lending with its repayments (FR-3).</summary>
    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var lending = await lendings.GetAsync(id, cancellationToken);
        if (lending is null)
        {
            return Result.Failure(LendingErrors.LendingNotFound);
        }
        lendings.Remove(lending);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    /// <summary>Records a repayment; the answer is the new repayment's id with the whole lending.</summary>
    public async Task<Result<RepaymentCreatedResponse>> AddRepaymentAsync(Guid id, RepaymentRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var lending = await lendings.GetAsync(id, cancellationToken);
        if (lending is null)
        {
            return Result.Failure<RepaymentCreatedResponse>(LendingErrors.LendingNotFound);
        }
        var errors = (await CheckAccountAsync(request.ToAccountId, null, from: false, cancellationToken)).ToList();
        var added = lending.AddRepayment(ToDetails(request), timeProvider.GetUtcNow());
        errors.InsertRange(0, added.Errors);
        var saved = await SaveAsync(lending, errors, cancellationToken);
        return saved.IsFailure
            ? Result.Failure<RepaymentCreatedResponse>(saved.Errors)
            : Result.Success(new RepaymentCreatedResponse(added.Value.Id, saved.Value));
    }

    public async Task<Result<LendingResponse>> UpdateRepaymentAsync(
        Guid id, Guid repaymentId, RepaymentRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var lending = await lendings.GetAsync(id, cancellationToken);
        var repayment = lending?.FindRepayment(repaymentId);
        if (lending is null || repayment is null)
        {
            return Result.Failure<LendingResponse>(lending is null ? LendingErrors.LendingNotFound : LendingErrors.RepaymentNotFound);
        }
        var errors = (await CheckAccountAsync(request.ToAccountId, repayment.ToAccountId, from: false, cancellationToken)).ToList();
        if (errors.Count == 0)
        {
            errors.AddRange(lending.UpdateRepayment(repaymentId, ToDetails(request), timeProvider.GetUtcNow()).Errors);
        }
        return await SaveAsync(lending, errors, cancellationToken);
    }

    public async Task<Result<LendingResponse>> DeleteRepaymentAsync(Guid id, Guid repaymentId, CancellationToken cancellationToken)
    {
        var lending = await lendings.GetAsync(id, cancellationToken);
        if (lending is null)
        {
            return Result.Failure<LendingResponse>(LendingErrors.LendingNotFound);
        }
        return await SaveAsync(lending, lending.RemoveRepayment(repaymentId, timeProvider.GetUtcNow()).Errors.ToList(), cancellationToken);
    }

    /// <summary>
    /// Money is lent from, and returned to, an account (L4, R2): not a credit or revolving card, and not
    /// blocked, expired or archived. An account the entry already had stays allowed.
    /// </summary>
    public static bool CanBeUsed(Card account, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(account);
        return !PaidFromRules.IsCardCharge(account)
            && account.StatusOn(today) is not (CardStatus.Blocked or CardStatus.Expired or CardStatus.Archived);
    }

    private async Task<Result<LendingResponse>> SaveAsync(Lending lending, List<DomainError> errors, CancellationToken cancellationToken)
    {
        if (errors.Count > 0)
        {
            return Result.Failure<LendingResponse>(errors.Distinct());
        }
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(await RespondAsync(lending, cancellationToken));
    }

    private async Task<IReadOnlyList<DomainError>> CheckAccountAsync(Guid accountId, Guid? existing, bool from, CancellationToken cancellationToken)
    {
        if (accountId == Guid.Empty || accountId == existing)
        {
            return [];
        }
        var account = await cards.GetAsync(accountId, includeSensitiveData: false, cancellationToken);
        if (account is null)
        {
            return [from ? LendingErrors.FromAccountUnknown : LendingErrors.ToAccountUnknown];
        }
        return CanBeUsed(account, timeProvider.Today())
            ? []
            : [from ? LendingErrors.FromAccountUnusable : LendingErrors.ToAccountUnusable];
    }

    private async Task<Payer> FindOrCreatePayerAsync(string name, CancellationToken cancellationToken)
    {
        var payer = await payers.FindByNameAsync(name, cancellationToken);
        if (payer is not null)
        {
            payer.Restore();
            return payer;
        }
        payer = Payer.Create(name, timeProvider.GetUtcNow()).Value;
        payers.Add(payer);
        return payer;
    }

    private async Task<LendingResponse> RespondAsync(Lending lending, CancellationToken cancellationToken) =>
        (await ledger.LoadAsync(cancellationToken)).ToResponse(lending);

    private static LendingDetails ToDetails(LendingRequest request, Guid payerId) =>
        new(payerId, Money.FromCents(request.AmountCents), request.LendDate, request.FromAccountId, request.ExpectedReturnDate, request.Description);

    private static RepaymentDetails ToDetails(RepaymentRequest request) =>
        new(Money.FromCents(request.AmountCents), request.ToAccountId, request.ReceivedDate, request.Note);
}
