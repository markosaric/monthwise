using Monthwise.Application.Cards;
using Monthwise.Application.Common;
using Monthwise.Domain.Cards;
using Monthwise.Domain.Common;
using Monthwise.Domain.Loans;

namespace Monthwise.Application.Loans;

/// <summary>The Loans list (FR-1): by category, then by name.</summary>
public sealed class GetLoansHandler(LoanLedger ledger)
{
    public async Task<IReadOnlyList<LoanResponse>> HandleAsync(LoanListFilter filter, CancellationToken cancellationToken)
    {
        var book = await ledger.LoadAsync(cancellationToken);
        return Sorted(book, book.Loans.Where(l => filter == LoanListFilter.Closed ? l.IsClosed : !l.IsClosed));
    }

    internal static List<LoanResponse> Sorted(LoanBook book, IEnumerable<Loan> loans) =>
        loans
            .Select(book.ToResponse)
            .OrderBy(l => l.Category.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(l => l.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
}

/// <summary>Loan details (FR-7, FR-9): figures, payment change history and payments.</summary>
public sealed class GetLoanHandler(LoanLedger ledger)
{
    public async Task<Result<LoanDetailsResponse>> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        var book = await ledger.LoadAsync(cancellationToken);
        var loan = book.Loans.FirstOrDefault(l => l.Id == id);
        return loan is null
            ? Result.Failure<LoanDetailsResponse>(LoanErrors.LoanNotFound)
            : Result.Success(Details(book, loan));
    }

    internal static LoanDetailsResponse Details(LoanBook book, Loan loan)
    {
        var schedule = book.Schedule(loan.Id);
        var latestPaid = schedule.LatestPaidMonth(book.Today);
        var changes = loan.Changes
            .Select(c => new PaymentChangeResponse(
                c.Id,
                c.FromMonth.ToString(),
                c.MonthlyPayment?.Cents,
                c.PaymentDay,
                c.Note,
                !loan.IsClosed && (latestPaid is null || latestPaid < c.FromMonth)))
            .ToList();
        var payments = schedule.Payments(book.Today).Select(book.ToResponse).ToList();
        return new LoanDetailsResponse(book.ToResponse(loan), changes, payments);
    }
}

/// <summary>The loan payments of one month with totals (FR-4).</summary>
public sealed class GetMonthLoanPaymentsHandler(LoanLedger ledger)
{
    public async Task<MonthLoanPaymentsResponse> HandleAsync(YearMonth month, CancellationToken cancellationToken)
    {
        var book = await ledger.LoadAsync(cancellationToken);
        var payments = book.PaymentsBetween(month.FirstDay, month.LastDay).ToList();
        return new MonthLoanPaymentsResponse(month.ToString(), book.ToSortedResponses(payments), LoanBook.Totals(payments));
    }
}

/// <summary>The Dashboard Loans widget (FR-13): loans that are not closed or paid off, with totals.</summary>
public sealed class GetLoansDashboardHandler(LoanLedger ledger)
{
    public async Task<LoansDashboardResponse> HandleAsync(CancellationToken cancellationToken)
    {
        var book = await ledger.LoadAsync(cancellationToken);
        var shown = GetLoansHandler.Sorted(book, book.Loans).Where(l => l.Status == LoanStatus.Active).ToList();
        return new LoansDashboardResponse(
            book.Loans.Count > 0,
            shown,
            Money.Sum(shown.Select(l => Money.FromCents(l.CurrentMonthlyPaymentCents))).Cents,
            Money.Sum(shown.Select(l => Money.FromCents(l.RemainingAmountCents))).Cents);
    }
}

/// <summary>Adds a loan (FR-2).</summary>
public sealed class CreateLoanHandler(
    ILoanRepository loans,
    LoanInputChecker checker,
    LoanLedger ledger,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<Result<LoanDetailsResponse>> HandleAsync(LoanRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var input = await checker.CheckAsync(request, existing: null, cancellationToken);
        if (input.IsFailure)
        {
            return Result.Failure<LoanDetailsResponse>(input.Errors);
        }
        var created = Loan.Create(input.Value.Details, input.Value.ResidualValueAllowed, timeProvider.GetUtcNow());
        if (created.IsFailure)
        {
            return Result.Failure<LoanDetailsResponse>(created.Errors);
        }
        if (await loans.NameInUseAsync(created.Value.Name, null, cancellationToken))
        {
            return Result.Failure<LoanDetailsResponse>(LoanErrors.NameInUse);
        }
        loans.Add(created.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await LoanResponder.DetailsAsync(ledger, created.Value.Id, cancellationToken);
    }
}

/// <summary>
/// Edits a loan (FR-10). Changes apply to expected payments only: standing order payments paid
/// automatically are stored first, so they keep their values.
/// </summary>
public sealed class UpdateLoanHandler(
    ILoanRepository loans,
    ILoanPaymentRepository payments,
    LoanInputChecker checker,
    LoanLedger ledger,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<Result<LoanDetailsResponse>> HandleAsync(Guid id, LoanRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var loan = await loans.GetAsync(id, cancellationToken);
        if (loan is null)
        {
            return Result.Failure<LoanDetailsResponse>(LoanErrors.LoanNotFound);
        }
        if (loan.IsClosed)
        {
            return Result.Failure<LoanDetailsResponse>(LoanErrors.LoanClosed);
        }
        var input = await checker.CheckAsync(request, loan, cancellationToken);
        if (input.IsFailure)
        {
            return Result.Failure<LoanDetailsResponse>(input.Errors);
        }
        var name = input.Value.Details.Name?.Trim() ?? string.Empty;
        if (name.Length > 0 && await loans.NameInUseAsync(name, loan.Id, cancellationToken))
        {
            return Result.Failure<LoanDetailsResponse>(LoanErrors.NameInUse);
        }

        var book = await ledger.LoadAsync(cancellationToken);
        var schedule = book.Schedule(loan.Id);
        var now = timeProvider.GetUtcNow();
        var paid = schedule.Paid(book.Today);
        // Before the loan changes, while the schedule still has its old values.
        await LoanResponder.KeepAutomaticPaymentsAsync(payments, schedule, book.Today, now, cancellationToken);
        var updated = loan.Update(input.Value.Details, input.Value.ResidualValueAllowed, paid, now);
        if (updated.IsFailure)
        {
            return Result.Failure<LoanDetailsResponse>(updated.Errors);
        }
        await RemoveStaleAsync(loan, now, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await LoanResponder.DetailsAsync(ledger, loan.Id, cancellationToken);
    }

    /// <summary>Stored expected payments the changed loan no longer has are removed.</summary>
    private async Task RemoveStaleAsync(Loan loan, DateTimeOffset now, CancellationToken cancellationToken)
    {
        foreach (var stored in await payments.ListAsync(loan.Id, cancellationToken))
        {
            if (stored.IsPaid == true)
            {
                continue;
            }
            var inSchedule = stored.Kind == LoanPaymentKind.Regular
                ? stored.Month >= loan.StartMonth && stored.Month <= loan.EndMonth
                : loan.ResidualValue is not null && stored.Month == loan.EndMonth.Next();
            if (!inSchedule)
            {
                payments.Remove(stored);
            }
            else if (stored.Kind == LoanPaymentKind.Regular && loan.PaymentMethod == PaymentMethod.Bill && stored.IsPaid == false)
            {
                // An unticked standing order payment is simply expected once the loan is a bill.
                stored.ClearPaidState(now);
                if (stored.IsEmpty)
                {
                    payments.Remove(stored);
                }
            }
        }
    }
}

/// <summary>Closes a loan early (FR-10): its expected payments are removed, its paid payments stay.</summary>
public sealed class CloseLoanHandler(
    ILoanRepository loans,
    ILoanPaymentRepository payments,
    LoanLedger ledger,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<Result<LoanDetailsResponse>> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        var loan = await loans.GetAsync(id, cancellationToken);
        if (loan is null)
        {
            return Result.Failure<LoanDetailsResponse>(LoanErrors.LoanNotFound);
        }
        if (loan.IsClosed)
        {
            return Result.Failure<LoanDetailsResponse>(LoanErrors.LoanClosed);
        }
        var book = await ledger.LoadAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        await LoanResponder.KeepAutomaticPaymentsAsync(payments, book.Schedule(loan.Id), book.Today, now, cancellationToken);
        var closed = loan.Close(book.Today, now);
        if (closed.IsFailure)
        {
            return Result.Failure<LoanDetailsResponse>(closed.Errors);
        }
        foreach (var stored in await payments.ListAsync(loan.Id, cancellationToken))
        {
            if (stored.IsPaid != true)
            {
                payments.Remove(stored);
            }
        }
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await LoanResponder.DetailsAsync(ledger, loan.Id, cancellationToken);
    }
}

/// <summary>Deletes a loan that has no paid payments (FR-10); its changes and stored payments go with it.</summary>
public sealed class DeleteLoanHandler(ILoanRepository loans, LoanLedger ledger, IUnitOfWork unitOfWork)
{
    public async Task<Result> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        var loan = await loans.GetAsync(id, cancellationToken);
        if (loan is null)
        {
            return Result.Failure(LoanErrors.LoanNotFound);
        }
        var book = await ledger.LoadAsync(cancellationToken);
        if (book.Schedule(loan.Id).HasPaidPayments(book.Today))
        {
            return Result.Failure(LoanErrors.LoanHasPaidPayments);
        }
        loans.Remove(loan);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

/// <summary>Records, edits and deletes payment changes (FR-7).</summary>
public sealed class PaymentChangeHandler(
    ILoanRepository loans,
    ILoanPaymentRepository payments,
    LoanLedger ledger,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public Task<Result<LoanDetailsResponse>> CreateAsync(Guid loanId, PaymentChangeRequest request, CancellationToken cancellationToken) =>
        ChangeAsync(loanId, request, (loan, details, latestPaid, now) =>
        {
            var added = loan.AddChange(details, latestPaid, now);
            return added.IsSuccess ? Result.Success() : Result.Failure(added.Errors);
        }, earliestOld: null, cancellationToken);

    public async Task<Result<LoanDetailsResponse>> UpdateAsync(
        Guid loanId, Guid changeId, PaymentChangeRequest request, CancellationToken cancellationToken)
    {
        var loan = await loans.GetAsync(loanId, cancellationToken);
        var old = loan?.Changes.FirstOrDefault(c => c.Id == changeId);
        if (loan is not null && old is null)
        {
            return Result.Failure<LoanDetailsResponse>(LoanErrors.ChangeNotFound);
        }
        return await ChangeAsync(
            loanId,
            request,
            (l, details, latestPaid, now) => l.UpdateChange(changeId, details, latestPaid, now),
            old?.FromMonth,
            cancellationToken);
    }

    public async Task<Result<LoanDetailsResponse>> DeleteAsync(Guid loanId, Guid changeId, CancellationToken cancellationToken)
    {
        var loan = await loans.GetAsync(loanId, cancellationToken);
        if (loan is null)
        {
            return Result.Failure<LoanDetailsResponse>(LoanErrors.LoanNotFound);
        }
        var book = await ledger.LoadAsync(cancellationToken);
        var removed = loan.RemoveChange(changeId, book.Schedule(loan.Id).LatestPaidMonth(book.Today), timeProvider.GetUtcNow());
        if (removed.IsFailure)
        {
            return Result.Failure<LoanDetailsResponse>(removed.Errors);
        }
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await LoanResponder.DetailsAsync(ledger, loan.Id, cancellationToken);
    }

    private async Task<Result<LoanDetailsResponse>> ChangeAsync(
        Guid loanId,
        PaymentChangeRequest request,
        Func<Loan, PaymentChangeDetails, YearMonth?, DateTimeOffset, Result> change,
        YearMonth? earliestOld,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var loan = await loans.GetAsync(loanId, cancellationToken);
        if (loan is null)
        {
            return Result.Failure<LoanDetailsResponse>(LoanErrors.LoanNotFound);
        }
        if (!YearMonth.TryParse(request.FromMonth, out var fromMonth))
        {
            return Result.Failure<LoanDetailsResponse>(LoanErrors.ChangeFromMonthRequired);
        }
        var details = new PaymentChangeDetails(
            fromMonth,
            request.MonthlyPaymentCents is { } cents ? Money.FromCents(cents) : null,
            request.PaymentDay,
            request.Note);

        var book = await ledger.LoadAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        var changed = change(loan, details, book.Schedule(loan.Id).LatestPaidMonth(book.Today), now);
        if (changed.IsFailure)
        {
            return Result.Failure<LoanDetailsResponse>(changed.Errors);
        }
        if (!request.KeepChangedPayments)
        {
            // Hand-changed expected payments from the change on take the new values (FR-7).
            var from = earliestOld is { } old && old < fromMonth ? old : fromMonth;
            foreach (var stored in await payments.ListAsync(loan.Id, cancellationToken))
            {
                if (stored.IsChanged && stored.IsPaid != true && stored.Month >= from)
                {
                    stored.ClearChanges(now);
                    if (stored.IsEmpty)
                    {
                        payments.Remove(stored);
                    }
                }
            }
        }
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await LoanResponder.DetailsAsync(ledger, loan.Id, cancellationToken);
    }
}

/// <summary>Changes, ticks and unticks one payment (FR-4, FR-5).</summary>
public sealed class LoanPaymentHandler(
    ILoanRepository loans,
    ILoanPaymentRepository payments,
    ICardRepository cards,
    LoanLedger ledger,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public Task<Result<LoanPaymentResponse>> ChangeAsync(
        Guid loanId, YearMonth month, LoanPaymentKind kind, LoanPaymentChangeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ApplyAsync(loanId, month, kind, (_, stored, now) =>
            Task.FromResult(stored.Change(Money.FromCents(request.AmountCents), request.DueDate, now)), cancellationToken);
    }

    public Task<Result<LoanPaymentResponse>> MarkPaidAsync(
        Guid loanId, YearMonth month, LoanPaymentKind kind, LoanPaymentPaidRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ApplyAsync(loanId, month, kind, async (payment, stored, now) =>
        {
            var today = timeProvider.Today();
            var accountId = request.AccountId
                ?? (payment.Method == PaymentMethod.StandingOrder ? payment.AccountId : null);
            if (accountId is not { } id || id == Guid.Empty)
            {
                return Result.Failure(LoanErrors.PaidAccountRequired);
            }
            if (id != stored.PaidAccountId)
            {
                var account = await cards.GetAsync(id, includeSensitiveData: false, cancellationToken);
                if (account is null)
                {
                    return Result.Failure(LoanErrors.PaidAccountUnknown);
                }
                if (!account.CanBeUsedOn(today) && id != payment.AccountId)
                {
                    return Result.Failure(LoanErrors.PaidAccountUnusable);
                }
            }
            return stored.MarkPaid(request.PaidDate ?? today, id, payment.Amount, payment.DueDate, now);
        }, cancellationToken);
    }

    public Task<Result<LoanPaymentResponse>> MarkUnpaidAsync(
        Guid loanId, YearMonth month, LoanPaymentKind kind, CancellationToken cancellationToken) =>
        ApplyAsync(loanId, month, kind, (payment, stored, now) =>
        {
            stored.MarkUnpaid(now);
            return Task.FromResult(Result.Success());
        }, cancellationToken);

    private async Task<Result<LoanPaymentResponse>> ApplyAsync(
        Guid loanId,
        YearMonth month,
        LoanPaymentKind kind,
        Func<ScheduledPayment, LoanPayment, DateTimeOffset, Task<Result>> apply,
        CancellationToken cancellationToken)
    {
        var loan = await loans.GetAsync(loanId, cancellationToken);
        if (loan is null)
        {
            return Result.Failure<LoanPaymentResponse>(LoanErrors.LoanNotFound);
        }
        if (loan.IsClosed)
        {
            return Result.Failure<LoanPaymentResponse>(LoanErrors.LoanClosed);
        }
        var book = await ledger.LoadAsync(cancellationToken);
        var payment = book.Schedule(loan.Id).Find(month, kind, book.Today);
        if (payment is null)
        {
            return Result.Failure<LoanPaymentResponse>(LoanErrors.PaymentNotFound);
        }

        var now = timeProvider.GetUtcNow();
        var stored = await payments.GetAsync(loan.Id, month, kind, cancellationToken);
        var isNew = stored is null;
        stored ??= LoanPayment.Create(loan.Id, month, kind, now);
        if (payment.PaidAutomatically)
        {
            stored.KeepAutomaticPayment(payment.Amount, payment.DueDate, payment.AccountId!.Value, now);
        }
        var applied = await apply(payment, stored, now);
        if (applied.IsFailure)
        {
            return Result.Failure<LoanPaymentResponse>(applied.Errors);
        }
        // An unticked bill is expected anyway; nothing needs to stay stored for it.
        if (payment.Method == PaymentMethod.Bill && stored.IsPaid == false)
        {
            stored.ClearPaidState(now);
        }
        if (isNew && !stored.IsEmpty)
        {
            payments.Add(stored);
        }
        else if (!isNew && stored.IsEmpty)
        {
            payments.Remove(stored);
        }
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var updated = await ledger.LoadAsync(cancellationToken);
        return Result.Success(updated.ToResponse(updated.Schedule(loan.Id).Find(month, kind, updated.Today)!));
    }
}

/// <summary>Shared steps of the loan handlers.</summary>
internal static class LoanResponder
{
    public static async Task<Result<LoanDetailsResponse>> DetailsAsync(LoanLedger ledger, Guid loanId, CancellationToken cancellationToken)
    {
        var book = await ledger.LoadAsync(cancellationToken);
        return Result.Success(GetLoanHandler.Details(book, book.Loans.First(l => l.Id == loanId)));
    }

    /// <summary>Stores the standing order payments paid automatically, so they stay paid when the loan changes (FR-5, FR-10).</summary>
    public static async Task KeepAutomaticPaymentsAsync(
        ILoanPaymentRepository payments, LoanSchedule schedule, DateOnly today, DateTimeOffset now, CancellationToken cancellationToken)
    {
        foreach (var payment in schedule.AutomaticallyPaid(today).ToList())
        {
            var stored = await payments.GetAsync(payment.LoanId, payment.Month, payment.Kind, cancellationToken);
            if (stored is null)
            {
                stored = LoanPayment.Create(payment.LoanId, payment.Month, payment.Kind, now);
                payments.Add(stored);
            }
            stored.KeepAutomaticPayment(payment.Amount, payment.DueDate, payment.AccountId!.Value, now);
        }
    }
}
