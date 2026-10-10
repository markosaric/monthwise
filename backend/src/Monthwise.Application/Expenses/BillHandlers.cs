using Monthwise.Application.CashFlow;
using Monthwise.Application.Common;
using Monthwise.Application.SharedCosts;
using Monthwise.Domain.Common;
using Monthwise.Domain.Expenses;
using Monthwise.Domain.SharedCosts;

namespace Monthwise.Application.Expenses;

/// <summary>The bills of a month for the Month screen, with totals (FR-4, FR-9).</summary>
public sealed class GetMonthBillsHandler(IBillRepository bills, ExpenseLedger ledger)
{
    public async Task<MonthBillsResponse> HandleAsync(YearMonth month, CancellationToken cancellationToken)
    {
        var book = await ledger.LoadAsync(cancellationToken);
        var monthBills = await bills.ListByMonthAsync(month, cancellationToken);
        return new MonthBillsResponse(
            month.ToString(),
            book.ToSortedResponses(monthBills),
            book.BillTotals(monthBills),
            Money.Sum(monthBills.Where(book.IsPaidWithCard).Select(b => b.Amount)).Cents,
            monthBills.Count(b => !b.IsIssued),
            book.OwnPart(monthBills).Cents);
    }
}

/// <summary>
/// Add recurring bills (FR-3): one Not issued bill for every active recurring bill that has no bill in
/// the month yet, so running it again adds nothing new.
/// </summary>
public sealed class AddRecurringBillsHandler(
    IRecurringBillRepository recurringBills,
    IBillRepository bills,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<AddRecurringBillsResponse> HandleAsync(YearMonth month, CancellationToken cancellationToken)
    {
        var existing = (await bills.ListByMonthAsync(month, cancellationToken)).Select(b => b.RecurringBillId).ToHashSet();
        var now = timeProvider.GetUtcNow();
        var added = 0;
        foreach (var recurringBill in await recurringBills.ListAsync(cancellationToken))
        {
            if (!recurringBill.IsActive || existing.Contains(recurringBill.Id))
            {
                continue;
            }
            bills.Add(Bill.CreateFromRecurring(recurringBill, month, now));
            added++;
        }
        if (added > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        return new AddRecurringBillsResponse(month.ToString(), added);
    }
}

/// <summary>Views, adds, edits, pays and deletes bills (FR-4, FR-5, FR-6), and shares them (shared bills FR-3).</summary>
public sealed class BillHandler(
    IBillRepository bills,
    IRecurringBillRepository recurringBills,
    ICashFlowInputRepository inputs,
    ExpenseInputChecker checker,
    ShareResolver shareResolver,
    ContributionCleaner contributionCleaner,
    ExpenseLedger ledger,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<Result<BillResponse>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var bill = await bills.GetAsync(id, cancellationToken);
        return bill is null
            ? Result.Failure<BillResponse>(ExpenseErrors.BillNotFound)
            : Result.Success((await ledger.LoadAsync(cancellationToken)).ToResponse(bill));
    }

    /// <summary>Adds a one-off bill to <paramref name="month"/>, expected or already paid (FR-4).</summary>
    public async Task<Result<BillResponse>> CreateAsync(YearMonth month, BillRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var details = ToDetails(request);
        var now = timeProvider.GetUtcNow();
        var created = Bill.CreateOneOff(month, details, request.PaidDate, now);
        var errors = created.IsFailure ? created.Errors.ToList() : [];
        errors.AddRange(await CheckAsync(details, request.PaidDate, existing: null, cancellationToken));
        var share = await shareResolver.ResolveAsync(request.Share, existing: null, cancellationToken);
        errors.AddRange(share.Errors);
        if (errors.Count > 0)
        {
            return Result.Failure<BillResponse>(errors);
        }
        created.Value.SetShare(share.Value, recurringShare: null, now);
        bills.Add(created.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success((await ledger.LoadAsync(cancellationToken)).ToResponse(created.Value));
    }

    /// <summary>Edits a bill: issues it, changes it, or sets its paid state (FR-4).</summary>
    public async Task<Result<BillResponse>> UpdateAsync(Guid id, BillRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var bill = await bills.GetAsync(id, cancellationToken);
        if (bill is null)
        {
            return Result.Failure<BillResponse>(ExpenseErrors.BillNotFound);
        }
        var details = ToDetails(request);
        var now = timeProvider.GetUtcNow();
        var errors = await CheckAsync(details, request.PaidDate, bill, cancellationToken);
        var share = await shareResolver.ResolveAsync(request.Share, bill.Share, cancellationToken);
        errors.AddRange(share.Errors);
        var updated = bill.Update(details, request.PaidDate, now);
        errors.InsertRange(0, updated.Errors);
        if (errors.Count > 0)
        {
            return Result.Failure<BillResponse>(errors);
        }
        var recurringShare = bill.RecurringBillId is { } recurringId
            ? (await recurringBills.GetAsync(recurringId, cancellationToken))?.Share
            : null;
        bill.SetShare(share.Value, recurringShare, now);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        // A bill that is no longer shared or no longer issued has no contribution (shared bills FR-6).
        await contributionCleaner.CleanUpAsync(cancellationToken);
        return Result.Success((await ledger.LoadAsync(cancellationToken)).ToResponse(bill));
    }

    /// <summary>
    /// Ticks Paid (FR-5): today unless a date is given, with what it was paid with (the bill's own when
    /// not given). Paid with a credit card, it goes into that card's card payment (FR-6).
    /// </summary>
    public async Task<Result<BillResponse>> MarkPaidAsync(Guid id, BillPaidRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var bill = await bills.GetAsync(id, cancellationToken);
        if (bill is null)
        {
            return Result.Failure<BillResponse>(ExpenseErrors.BillNotFound);
        }
        var paidDate = request.PaidDate ?? timeProvider.Today();
        var paidFromId = request.PaidFromId is { } given && given != Guid.Empty ? given : bill.PaidFromId;
        if (paidFromId is null)
        {
            return Result.Failure<BillResponse>(ExpenseErrors.PaidFromRequired);
        }
        var errors = await checker.CheckAsync(
            bill.CategoryId, bill.CategoryId, paidFromId, bill.IsPaid ? bill.PaidFromId : null, paidDate, primaryOnly: false, cancellationToken);
        if (errors.Count > 0)
        {
            return Result.Failure<BillResponse>(errors);
        }
        var paid = bill.MarkPaid(paidDate, paidFromId.Value, timeProvider.GetUtcNow());
        if (paid.IsFailure)
        {
            return Result.Failure<BillResponse>(paid.Errors);
        }
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success((await ledger.LoadAsync(cancellationToken)).ToResponse(bill));
    }

    /// <summary>Unticks Paid: the bill is expected again (FR-5).</summary>
    public async Task<Result<BillResponse>> MarkUnpaidAsync(Guid id, CancellationToken cancellationToken)
    {
        var bill = await bills.GetAsync(id, cancellationToken);
        if (bill is null)
        {
            return Result.Failure<BillResponse>(ExpenseErrors.BillNotFound);
        }
        bill.MarkUnpaid(timeProvider.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success((await ledger.LoadAsync(cancellationToken)).ToResponse(bill));
    }

    /// <summary>
    /// Deletes a bill from its month; a recurring bill it came from stays (FR-4). Its expected
    /// contribution goes with it, a received one stays as an ordinary input (shared bills FR-6).
    /// </summary>
    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var bill = await bills.GetAsync(id, cancellationToken);
        if (bill is null)
        {
            return Result.Failure(ExpenseErrors.BillNotFound);
        }
        if (await inputs.GetContributionAsync(ContributionSource.ForBill(bill.Id), cancellationToken) is { } contribution)
        {
            ContributionCleaner.Release(inputs, contribution, timeProvider.GetUtcNow());
        }
        bills.Remove(bill);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    /// <summary>
    /// The category, and what the bill is paid with: checked as of its paid date when paid, otherwise as
    /// a usual choice for today.
    /// </summary>
    private Task<List<DomainError>> CheckAsync(BillDetails details, DateOnly? paidDate, Bill? existing, CancellationToken cancellationToken) =>
        checker.CheckAsync(
            details.CategoryId,
            existing?.CategoryId,
            details.PaidFromId,
            existing?.PaidFromId,
            paidDate ?? timeProvider.Today(),
            primaryOnly: false,
            cancellationToken);

    private static BillDetails ToDetails(BillRequest request) =>
        new(
            request.Name,
            request.CategoryId,
            request.Payee,
            Money.FromCents(request.AmountCents),
            request.IssueDate,
            request.DueDate,
            request.PaidFromId,
            request.Description);
}
