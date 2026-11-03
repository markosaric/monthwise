using Monthwise.Application.Cards;
using Monthwise.Application.Common;
using Monthwise.Domain.Cards;
using Monthwise.Domain.Common;
using Monthwise.Domain.Revolving;

namespace Monthwise.Application.Revolving;

/// <summary>
/// The revolving cards of the Cards screen (revolving FR-10): cards that are not archived, and archived
/// ones while they still have debt.
/// </summary>
public sealed class GetRevolvingCardsHandler(RevolvingLedger ledger)
{
    public async Task<RevolvingCardsResponse> HandleAsync(CancellationToken cancellationToken)
    {
        var book = await ledger.LoadAsync(cancellationToken);
        var cards = book.Calculators
            .Where(c => c.Card.StatusOn(book.Today) != CardStatus.Archived || c.Debt.IsPositive)
            .Select(book.ToResponse)
            .OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        return new RevolvingCardsResponse(cards, cards.Sum(c => c.DebtCents));
    }
}

/// <summary>The revolving card page (revolving FR-10) and the debt on a date.</summary>
public sealed class GetRevolvingCardHandler(RevolvingLedger ledger)
{
    /// <param name="to">The last month of estimated bills to show; next month when null.</param>
    public async Task<Result<RevolvingCardDetailsResponse>> HandleAsync(Guid cardId, YearMonth? to, CancellationToken cancellationToken)
    {
        var book = await ledger.LoadAsync(cancellationToken, to);
        return book.Calculator(cardId) is { } calculator
            ? Result.Success(book.Details(calculator))
            : Result.Failure<RevolvingCardDetailsResponse>(RevolvingErrors.CardUnknown);
    }

    public async Task<Result<RevolvingDebtResponse>> DebtAsync(Guid cardId, DateOnly? date, CancellationToken cancellationToken)
    {
        var book = await ledger.LoadAsync(cancellationToken);
        if (book.Calculator(cardId) is not { } calculator)
        {
            return Result.Failure<RevolvingDebtResponse>(RevolvingErrors.CardUnknown);
        }
        var day = date ?? book.Today;
        return Result.Success(new RevolvingDebtResponse(cardId, day, calculator.DebtOn(day).Cents));
    }
}

/// <summary>The monthly bills and extra payments of a month (revolving FR-11): by paid date once paid, by due date before.</summary>
public sealed class GetMonthRevolvingHandler(RevolvingLedger ledger)
{
    public async Task<MonthRevolvingResponse> HandleAsync(YearMonth month, CancellationToken cancellationToken)
    {
        var book = await ledger.LoadAsync(cancellationToken, month.Next());
        bool InMonth(DateOnly date) => date >= month.FirstDay && date <= month.LastDay;
        var bills = book.Bills().Where(b => InMonth(b.MonthDate) && (b.IsPaid || b.Minimum.IsPositive)).ToList();
        var payments = book.ExtraPayments.Where(p => InMonth(p.Date)).ToList();
        return new MonthRevolvingResponse(
            month.ToString(),
            bills.Select(book.ToResponse)
                .OrderBy(b => b.PaidDate ?? b.DueDate)
                .ThenBy(b => b.CardName, StringComparer.CurrentCultureIgnoreCase)
                .ToList(),
            payments.OrderBy(p => p.Date).ThenBy(p => p.CreatedAt).Select(book.ToResponse).ToList(),
            RevolvingBook.Totals(bills, payments));
    }
}

/// <summary>Pays, unticks and corrects a monthly bill (revolving FR-4, FR-7, FR-8).</summary>
public sealed class RevolvingBillHandler(
    IRevolvingBillRepository bills,
    ICardRepository cards,
    RevolvingLedger ledger,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public Task<Result<RevolvingBillResponse>> MarkPaidAsync(
        Guid cardId, YearMonth month, RevolvingBillPaidRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ApplyAsync(cardId, month, async (bill, stored, now) =>
        {
            var today = timeProvider.Today();
            if ((request.AccountId ?? bill.AccountId) is not { } accountId || accountId == Guid.Empty)
            {
                return Result.Failure(RevolvingErrors.AccountRequired);
            }
            if (accountId != stored.PaidAccountId)
            {
                var account = await cards.GetAsync(accountId, includeSensitiveData: false, cancellationToken);
                if (account is null || !account.IsAccount)
                {
                    return Result.Failure(RevolvingErrors.AccountUnknown);
                }
                if (!account.CanBeUsedOn(today) && accountId != bill.AccountId)
                {
                    return Result.Failure(RevolvingErrors.AccountUnusable);
                }
            }
            var amount = request.AmountCents is { } cents ? Money.FromCents(cents) : bill.Minimum;
            if (amount > bill.Debt)
            {
                return Result.Failure(RevolvingErrors.AmountOverDebt);
            }
            return stored.MarkPaid(request.PaidDate ?? today, accountId, amount, bill.BillDate, now);
        }, cancellationToken);
    }

    public Task<Result<RevolvingBillResponse>> MarkUnpaidAsync(Guid cardId, YearMonth month, CancellationToken cancellationToken) =>
        ApplyAsync(cardId, month, (_, stored, now) =>
        {
            stored.MarkUnpaid(now);
            return Task.FromResult(Result.Success());
        }, cancellationToken);

    public Task<Result<RevolvingBillResponse>> CorrectAsync(
        Guid cardId, YearMonth month, RevolvingBillCorrectionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ApplyAsync(cardId, month, (_, stored, now) => Task.FromResult(stored.Correct(
            request.MinimumCents is { } minimum ? Money.FromCents(minimum) : null,
            request.InterestCents is { } interest ? Money.FromCents(interest) : null,
            now)), cancellationToken);
    }

    public Task<Result<RevolvingBillResponse>> ClearCorrectionAsync(Guid cardId, YearMonth month, CancellationToken cancellationToken) =>
        ApplyAsync(cardId, month, (_, stored, now) => Task.FromResult(stored.Correct(null, null, now)), cancellationToken);

    private async Task<Result<RevolvingBillResponse>> ApplyAsync(
        Guid cardId,
        YearMonth month,
        Func<ScheduledRevolvingBill, RevolvingBill, DateTimeOffset, Task<Result>> apply,
        CancellationToken cancellationToken)
    {
        var book = await ledger.LoadAsync(cancellationToken, month);
        if (book.Calculator(cardId) is not { } calculator)
        {
            return Result.Failure<RevolvingBillResponse>(RevolvingErrors.CardUnknown);
        }
        if (calculator.Bill(month) is not { } bill)
        {
            return Result.Failure<RevolvingBillResponse>(RevolvingErrors.BillNotFound);
        }
        if (bill.IsEstimated)
        {
            return Result.Failure<RevolvingBillResponse>(RevolvingErrors.BillEstimated);
        }

        var now = timeProvider.GetUtcNow();
        var stored = await bills.GetAsync(cardId, month, cancellationToken);
        var isNew = stored is null;
        stored ??= RevolvingBill.Create(cardId, month, now);
        var result = await apply(bill, stored, now);
        if (result.IsFailure)
        {
            return Result.Failure<RevolvingBillResponse>(result.Errors);
        }
        if (isNew)
        {
            bills.Add(stored);
        }
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var updated = await ledger.LoadAsync(cancellationToken, month);
        return Result.Success(updated.ToResponse(updated.Calculator(cardId)!.Bill(month)!));
    }
}

/// <summary>Records, edits and deletes extra payments (revolving FR-8).</summary>
public sealed class ExtraPaymentHandler(
    IExtraPaymentRepository payments,
    ICardRepository cards,
    RevolvingLedger ledger,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<Result<ExtraPaymentResponse>> CreateAsync(Guid cardId, ExtraPaymentRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var details = ToDetails(cardId, request);
        var errors = await CheckAsync(details, existing: null, cancellationToken);
        if (errors.Count > 0)
        {
            return Result.Failure<ExtraPaymentResponse>(errors);
        }
        var created = ExtraPayment.Create(details, timeProvider.Today(), timeProvider.GetUtcNow());
        if (created.IsFailure)
        {
            return Result.Failure<ExtraPaymentResponse>(created.Errors);
        }
        payments.Add(created.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success((await ledger.LoadAsync(cancellationToken)).ToResponse(created.Value));
    }

    public async Task<Result<ExtraPaymentResponse>> UpdateAsync(Guid id, ExtraPaymentRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var payment = await payments.GetAsync(id, cancellationToken);
        if (payment is null)
        {
            return Result.Failure<ExtraPaymentResponse>(RevolvingErrors.PaymentNotFound);
        }
        var details = ToDetails(payment.CardId, request);
        var errors = await CheckAsync(details, payment, cancellationToken);
        if (errors.Count > 0)
        {
            return Result.Failure<ExtraPaymentResponse>(errors);
        }
        var updated = payment.Update(details, timeProvider.Today(), timeProvider.GetUtcNow());
        if (updated.IsFailure)
        {
            return Result.Failure<ExtraPaymentResponse>(updated.Errors);
        }
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success((await ledger.LoadAsync(cancellationToken)).ToResponse(payment));
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var payment = await payments.GetAsync(id, cancellationToken);
        if (payment is null)
        {
            return Result.Failure(RevolvingErrors.PaymentNotFound);
        }
        payments.Remove(payment);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private static ExtraPaymentDetails ToDetails(Guid cardId, ExtraPaymentRequest request) =>
        new(cardId, request.AccountId, request.Date, Money.FromCents(request.AmountCents), request.Description);

    /// <summary>
    /// The card must be revolving (usable for a new payment), the account a usable account, and the
    /// amount not more than the debt on the payment's date. A card or account the payment already had
    /// stays allowed.
    /// </summary>
    private async Task<List<DomainError>> CheckAsync(ExtraPaymentDetails details, ExtraPayment? existing, CancellationToken cancellationToken)
    {
        var today = timeProvider.Today();
        var errors = new List<DomainError>();
        var book = await ledger.LoadAsync(cancellationToken, withoutExtraPaymentId: existing?.Id);
        if (book.Calculator(details.CardId) is not { } calculator)
        {
            errors.Add(RevolvingErrors.CardUnknown);
            return errors;
        }
        if (existing is null && calculator.Card.StatusOn(today) == CardStatus.Archived)
        {
            errors.Add(RevolvingErrors.CardUnusable);
        }
        if (details.AccountId != Guid.Empty && details.AccountId != existing?.AccountId)
        {
            var account = await cards.GetAsync(details.AccountId, includeSensitiveData: false, cancellationToken);
            if (account is null || !account.IsAccount)
            {
                errors.Add(RevolvingErrors.AccountUnknown);
            }
            else if (!account.CanBeUsedOn(today))
            {
                errors.Add(RevolvingErrors.AccountUnusable);
            }
        }
        if (details.Amount.IsPositive && details.Date <= today && details.Amount > calculator.DebtOn(details.Date))
        {
            errors.Add(RevolvingErrors.AmountOverDebt);
        }
        return errors;
    }
}

/// <summary>Records, edits and deletes starting debts and corrections (revolving FR-5).</summary>
public sealed class DebtEntryHandler(
    IDebtEntryRepository entries,
    RevolvingLedger ledger,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<Result<DebtEntryResponse>> CreateAsync(Guid cardId, DebtEntryRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if ((await ledger.LoadAsync(cancellationToken)).Calculator(cardId) is null)
        {
            return Result.Failure<DebtEntryResponse>(RevolvingErrors.CardUnknown);
        }
        if (request.Kind is not (DebtEntryKind.Starting or DebtEntryKind.Correction))
        {
            return Result.Failure<DebtEntryResponse>(RevolvingErrors.TransferReadOnly);
        }
        var created = DebtEntry.Create(
            cardId, request.Kind, request.Date, Money.FromCents(request.DebtCents), request.Note, timeProvider.GetUtcNow());
        if (created.IsFailure)
        {
            return Result.Failure<DebtEntryResponse>(created.Errors);
        }
        entries.Add(created.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ResponseAsync(created.Value, cancellationToken);
    }

    public async Task<Result<DebtEntryResponse>> UpdateAsync(Guid id, DebtEntryRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var entry = await entries.GetAsync(id, cancellationToken);
        if (entry is null)
        {
            return Result.Failure<DebtEntryResponse>(RevolvingErrors.EntryNotFound);
        }
        var updated = entry.Update(request.Date, Money.FromCents(request.DebtCents), request.Note, timeProvider.GetUtcNow());
        if (updated.IsFailure)
        {
            return Result.Failure<DebtEntryResponse>(updated.Errors);
        }
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ResponseAsync(entry, cancellationToken);
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var entry = await entries.GetAsync(id, cancellationToken);
        if (entry is null)
        {
            return Result.Failure(RevolvingErrors.EntryNotFound);
        }
        if (entry.IsTransfer)
        {
            return Result.Failure(RevolvingErrors.TransferReadOnly);
        }
        entries.Remove(entry);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<Result<DebtEntryResponse>> ResponseAsync(DebtEntry entry, CancellationToken cancellationToken)
    {
        var book = await ledger.LoadAsync(cancellationToken);
        var details = book.Details(book.Calculator(entry.CardId)!);
        return Result.Success(details.DebtEntries.First(e => e.Id == entry.Id));
    }
}

/// <summary>
/// The revolving side of card changes (revolving FR-1, FR-9): the type lock, the archive rule and the
/// debt that moves to a replacement card.
/// </summary>
public sealed class RevolvingCardRules(
    RevolvingLedger ledger,
    IDebtEntryRepository entries,
    IExtraPaymentRepository payments,
    IRevolvingBillRepository bills)
{
    /// <summary>Whether the card has debt entries, charges, extra payments or stored bills, so its type cannot change to or from Revolving.</summary>
    public async Task<bool> HasActivityAsync(Guid cardId, CancellationToken cancellationToken)
    {
        if ((await entries.ListAsync(cardId, cancellationToken)).Count > 0
            || (await payments.ListAsync(cardId, cancellationToken)).Count > 0
            || (await bills.ListAsync(cardId, cancellationToken)).Count > 0)
        {
            return true;
        }
        var book = await ledger.LoadAsync(cancellationToken);
        return book.Calculator(cardId)?.Movements.Count > 0;
    }

    /// <summary>Today's debt of the card; zero for a card that is not revolving.</summary>
    public async Task<Money> DebtAsync(Guid cardId, CancellationToken cancellationToken) =>
        (await ledger.LoadAsync(cancellationToken)).DebtOf(cardId);

    /// <summary>Moves today's debt of <paramref name="from"/> to its replacement <paramref name="to"/> (FR-9).</summary>
    public async Task MoveDebtAsync(Card from, Card to, DateOnly today, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);
        if (!from.IsRevolving || !to.IsRevolving)
        {
            return;
        }
        var debt = await DebtAsync(from.Id, cancellationToken);
        if (!debt.IsPositive)
        {
            return;
        }
        var (moveOut, moveIn) = DebtEntry.CreateTransfer(from.Id, to.Id, today, debt, now);
        entries.Add(moveOut);
        entries.Add(moveIn);
    }
}
