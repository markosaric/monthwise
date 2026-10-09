using Monthwise.Application.Cards;
using Monthwise.Application.Common;
using Monthwise.Application.Expenses;
using Monthwise.Domain.CardPurchases;
using Monthwise.Domain.Cards;
using Monthwise.Domain.Common;
using Monthwise.Domain.Expenses;

namespace Monthwise.Application.CardPurchases;

/// <summary>
/// The Cards screen (FR-7): cards that allow monthly installments and are not archived, and other
/// cards while they still have installments to charge.
/// </summary>
public sealed class GetInstallmentCardsHandler(CardPaymentLedger ledger)
{
    public async Task<IReadOnlyList<InstallmentCardResponse>> HandleAsync(CancellationToken cancellationToken)
    {
        var book = await ledger.LoadAsync(cancellationToken);
        return book.Schedules
            .Where(s => (s.Card.AllowsInstallments && s.Card.StatusOn(book.Today) != CardStatus.Archived)
                || s.HasInstallmentsLeft(book.Today))
            .Select(book.ToResponse)
            .OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }
}

/// <summary>One card of the Cards screen with its card payments by month and its purchases (FR-7).</summary>
public sealed class GetInstallmentCardHandler(CardPaymentLedger ledger)
{
    public async Task<Result<InstallmentCardResponse>> HandleAsync(Guid cardId, CancellationToken cancellationToken)
    {
        var book = await ledger.LoadAsync(cancellationToken);
        return book.Schedule(cardId) is { } schedule
            ? Result.Success(book.ToResponse(schedule))
            : Result.Failure<InstallmentCardResponse>(CardErrors.NotFound);
    }

    public async Task<Result<IReadOnlyList<CardPaymentResponse>>> PaymentsAsync(Guid cardId, CancellationToken cancellationToken)
    {
        var book = await ledger.LoadAsync(cancellationToken);
        if (book.Schedule(cardId) is not { } schedule)
        {
            return Result.Failure<IReadOnlyList<CardPaymentResponse>>(CardErrors.NotFound);
        }
        // Newest month first.
        return Result.Success<IReadOnlyList<CardPaymentResponse>>(
            schedule.Payments(book.Today).OrderByDescending(p => p.Month).Select(book.ToResponse).ToList());
    }

    public async Task<Result<IReadOnlyList<CardPurchaseResponse>>> PurchasesAsync(Guid cardId, CancellationToken cancellationToken)
    {
        var book = await ledger.LoadAsync(cancellationToken);
        return book.Schedule(cardId) is { } schedule
            ? Result.Success(book.ToSortedResponses(schedule.Purchases))
            : Result.Failure<IReadOnlyList<CardPurchaseResponse>>(CardErrors.NotFound);
    }

    /// <summary>The drill-down of one card payment (FR-8).</summary>
    public async Task<Result<CardPaymentDetailsResponse>> PaymentDetailsAsync(
        Guid cardId, YearMonth month, CancellationToken cancellationToken)
    {
        var book = await ledger.LoadAsync(cancellationToken);
        if (book.Schedule(cardId) is not { } schedule)
        {
            return Result.Failure<CardPaymentDetailsResponse>(CardErrors.NotFound);
        }
        return schedule.Find(month, book.Today) is { } payment
            ? Result.Success(book.PaymentDetails(payment))
            : Result.Failure<CardPaymentDetailsResponse>(CardPurchaseErrors.PaymentNotFound);
    }
}

/// <summary>The card payments of one month for the Month screen: one line per card (FR-8).</summary>
public sealed class GetMonthCardPaymentsHandler(CardPaymentLedger ledger)
{
    public async Task<MonthCardPaymentsResponse> HandleAsync(YearMonth month, CancellationToken cancellationToken)
    {
        var book = await ledger.LoadAsync(cancellationToken);
        var payments = book.PaymentsBetween(month.FirstDay, month.LastDay).ToList();
        return new MonthCardPaymentsResponse(month.ToString(), book.ToSortedResponses(payments), CardPaymentBook.Totals(payments));
    }
}

/// <summary>A purchase with its installments (FR-4, FR-5).</summary>
public sealed class GetCardPurchaseHandler(CardPaymentLedger ledger)
{
    public async Task<Result<CardPurchaseDetailsResponse>> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        var book = await ledger.LoadAsync(cancellationToken);
        return book.Purchase(id) is { } purchase
            ? Result.Success(book.Details(purchase))
            : Result.Failure<CardPurchaseDetailsResponse>(CardPurchaseErrors.NotFound);
    }

    internal static async Task<Result<CardPurchaseDetailsResponse>> DetailsAsync(
        CardPaymentLedger ledger, Guid id, CancellationToken cancellationToken)
    {
        var book = await ledger.LoadAsync(cancellationToken);
        return Result.Success(book.Details(book.Purchase(id)!));
    }
}

/// <summary>
/// Checks a purchase request against the card (installments allowed, billing details, usable, valid
/// from) and the category, and returns the details and the card's installment limit.
/// </summary>
public sealed class CardPurchaseInputChecker(
    ICardRepository cards,
    IExpenseCategoryRepository categories,
    TimeProvider timeProvider)
{
    /// <param name="existing">The purchase being edited: its card and category stay allowed when no longer usable or hidden.</param>
    public async Task<Result<(CardPurchaseDetails Details, int MaxInstallments)>> CheckAsync(
        CardPurchaseRequest request, CardPurchase? existing, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var errors = new List<DomainError>();
        var maxInstallments = 0;
        if (request.CardId != Guid.Empty)
        {
            var card = await cards.GetAsync(request.CardId, includeSensitiveData: false, cancellationToken);
            if (card is null)
            {
                errors.Add(CardPurchaseErrors.CardUnknown);
            }
            else if (!card.AllowsInstallments)
            {
                errors.Add(CardPurchaseErrors.CardNoInstallments);
            }
            else if (!card.HasBillingDetails)
            {
                errors.Add(CardPurchaseErrors.CardNeedsBillingDetails);
            }
            else
            {
                maxInstallments = card.MaxInstallments!.Value;
                if (!card.CanBeUsedOn(timeProvider.Today()) && existing?.CardId != card.Id)
                {
                    errors.Add(CardPurchaseErrors.CardUnusable);
                }
                if (card.ValidFrom is { } validFrom && request.PurchaseDate < validFrom.FirstDay)
                {
                    errors.Add(CardPurchaseErrors.PurchaseDateBeforeValidFrom);
                }
            }
        }
        if (request.CategoryId is { } categoryId && categoryId != Guid.Empty)
        {
            var category = await categories.GetAsync(categoryId, cancellationToken);
            if (category is null)
            {
                errors.Add(CardPurchaseErrors.CategoryUnknown);
            }
            else if (category.IsHidden && existing?.CategoryId != category.Id)
            {
                errors.Add(CardPurchaseErrors.CategoryHidden);
            }
            else if (category.IsFixed)
            {
                // Cash withdrawal is only for cash withdrawals (bills and expenses FR-1).
                errors.Add(ExpenseErrors.CategoryCashWithdrawal);
            }
        }
        if (errors.Count > 0)
        {
            return Result.Failure<(CardPurchaseDetails, int)>(errors);
        }
        var details = new CardPurchaseDetails(
            request.CardId,
            request.Description,
            request.PurchaseDate,
            Money.FromCents(request.AmountCents),
            request.InInstallments,
            request.InstallmentCount,
            request.PostponeFirst,
            request.CategoryId is { } id && id != Guid.Empty ? id : null);
        return Result.Success((details, maxInstallments));
    }
}

/// <summary>Records a purchase (FR-3, FR-4).</summary>
public sealed class CreateCardPurchaseHandler(
    ICardPurchaseRepository purchases,
    CardPurchaseInputChecker checker,
    CardPaymentLedger ledger,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<Result<CardPurchaseDetailsResponse>> HandleAsync(CardPurchaseRequest request, CancellationToken cancellationToken)
    {
        var input = await checker.CheckAsync(request, existing: null, cancellationToken);
        if (input.IsFailure)
        {
            return Result.Failure<CardPurchaseDetailsResponse>(input.Errors);
        }
        var created = CardPurchase.Create(input.Value.Details, input.Value.MaxInstallments, timeProvider.GetUtcNow());
        if (created.IsFailure)
        {
            return Result.Failure<CardPurchaseDetailsResponse>(created.Errors);
        }
        purchases.Add(created.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetCardPurchaseHandler.DetailsAsync(ledger, created.Value.Id, cancellationToken);
    }
}

/// <summary>
/// Edits a purchase that is not reversed (FR-6). Its installments, and the card payments they are in,
/// are recalculated; the UI warns first when card payments whose payment date passed change.
/// </summary>
public sealed class UpdateCardPurchaseHandler(
    ICardPurchaseRepository purchases,
    CardPurchaseInputChecker checker,
    CardPaymentLedger ledger,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<Result<CardPurchaseDetailsResponse>> HandleAsync(
        Guid id, CardPurchaseRequest request, CancellationToken cancellationToken)
    {
        var purchase = await purchases.GetAsync(id, cancellationToken);
        if (purchase is null)
        {
            return Result.Failure<CardPurchaseDetailsResponse>(CardPurchaseErrors.NotFound);
        }
        if (purchase.IsReversed)
        {
            return Result.Failure<CardPurchaseDetailsResponse>(CardPurchaseErrors.Reversed);
        }
        var input = await checker.CheckAsync(request, purchase, cancellationToken);
        if (input.IsFailure)
        {
            return Result.Failure<CardPurchaseDetailsResponse>(input.Errors);
        }
        var updated = purchase.Update(input.Value.Details, input.Value.MaxInstallments, timeProvider.GetUtcNow());
        if (updated.IsFailure)
        {
            return Result.Failure<CardPurchaseDetailsResponse>(updated.Errors);
        }
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetCardPurchaseHandler.DetailsAsync(ledger, id, cancellationToken);
    }
}

/// <summary>Deletes a purchase, e.g. one entered by mistake (FR-6); its installments and credits go with it.</summary>
public sealed class DeleteCardPurchaseHandler(ICardPurchaseRepository purchases, IUnitOfWork unitOfWork)
{
    public async Task<Result> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        var purchase = await purchases.GetAsync(id, cancellationToken);
        if (purchase is null)
        {
            return Result.Failure(CardPurchaseErrors.NotFound);
        }
        purchases.Remove(purchase);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

/// <summary>Reverses a whole purchase or one installment (FR-5).</summary>
public sealed class ReverseCardPurchaseHandler(
    ICardPurchaseRepository purchases,
    CardPaymentLedger ledger,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public Task<Result<CardPurchaseDetailsResponse>> ReversePurchaseAsync(
        Guid id, ReversalRequest request, CancellationToken cancellationToken) =>
        ApplyAsync(id, purchase => purchase.Reverse(timeProvider.Today(), request?.Reason, timeProvider.GetUtcNow()), cancellationToken);

    public Task<Result<CardPurchaseDetailsResponse>> ReverseInstallmentAsync(
        Guid id, int number, ReversalRequest request, CancellationToken cancellationToken) =>
        ApplyAsync(
            id,
            purchase => purchase.ReverseInstallment(number, timeProvider.Today(), request?.Reason, timeProvider.GetUtcNow()),
            cancellationToken);

    private async Task<Result<CardPurchaseDetailsResponse>> ApplyAsync(
        Guid id, Func<CardPurchase, Result> reverse, CancellationToken cancellationToken)
    {
        var purchase = await purchases.GetAsync(id, cancellationToken);
        if (purchase is null)
        {
            return Result.Failure<CardPurchaseDetailsResponse>(CardPurchaseErrors.NotFound);
        }
        var reversed = reverse(purchase);
        if (reversed.IsFailure)
        {
            return Result.Failure<CardPurchaseDetailsResponse>(reversed.Errors);
        }
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetCardPurchaseHandler.DetailsAsync(ledger, id, cancellationToken);
    }
}

/// <summary>Ticks and unticks one card payment (FR-8).</summary>
public sealed class CardPaymentHandler(
    ICardPaymentRepository payments,
    ICardRepository cards,
    CardPaymentLedger ledger,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public Task<Result<CardPaymentResponse>> MarkPaidAsync(
        Guid cardId, YearMonth month, CardPaymentPaidRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ApplyAsync(cardId, month, async (payment, stored, now) =>
        {
            var today = timeProvider.Today();
            if ((request.AccountId ?? payment.AccountId) is not { } accountId || accountId == Guid.Empty)
            {
                return Result.Failure(CardPurchaseErrors.PaidAccountRequired);
            }
            if (accountId != stored.PaidAccountId)
            {
                var account = await cards.GetAsync(accountId, includeSensitiveData: false, cancellationToken);
                if (account is null)
                {
                    return Result.Failure(CardPurchaseErrors.PaidAccountUnknown);
                }
                if (!account.CanBeUsedOn(today) && accountId != payment.AccountId)
                {
                    return Result.Failure(CardPurchaseErrors.PaidAccountUnusable);
                }
            }
            return stored.MarkPaid(request.PaidDate ?? today, accountId, payment.StatementDate, now);
        }, cancellationToken);
    }

    public Task<Result<CardPaymentResponse>> MarkUnpaidAsync(Guid cardId, YearMonth month, CancellationToken cancellationToken) =>
        ApplyAsync(cardId, month, (payment, stored, now) =>
        {
            // A card payment that is not paid automatically is expected anyway once unticked.
            if (payment.PaysFromAccountDirectly)
            {
                stored.MarkUnpaid(now);
            }
            else
            {
                stored.ClearPaidState(now);
            }
            return Task.FromResult(Result.Success());
        }, cancellationToken);

    private async Task<Result<CardPaymentResponse>> ApplyAsync(
        Guid cardId,
        YearMonth month,
        Func<ScheduledCardPayment, CardPayment, DateTimeOffset, Task<Result>> apply,
        CancellationToken cancellationToken)
    {
        var book = await ledger.LoadAsync(cancellationToken);
        if (book.Schedule(cardId) is not { } schedule)
        {
            return Result.Failure<CardPaymentResponse>(CardErrors.NotFound);
        }
        if (schedule.Find(month, book.Today) is not { } payment)
        {
            return Result.Failure<CardPaymentResponse>(CardPurchaseErrors.PaymentNotFound);
        }

        var now = timeProvider.GetUtcNow();
        var stored = await payments.GetAsync(cardId, month, cancellationToken);
        var isNew = stored is null;
        stored ??= CardPayment.Create(cardId, month, now);
        var applied = await apply(payment, stored, now);
        if (applied.IsFailure)
        {
            return Result.Failure<CardPaymentResponse>(applied.Errors);
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
        return Result.Success(updated.ToResponse(updated.Schedule(cardId)!.Find(month, updated.Today)!));
    }
}
