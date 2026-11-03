using Monthwise.Application.CardPurchases;
using Monthwise.Application.Common;
using Monthwise.Application.Revolving;
using Monthwise.Domain.Cards;
using Monthwise.Domain.Common;
using Monthwise.Domain.Revolving;

namespace Monthwise.Application.Cards;

/// <summary>
/// Adds a card (FR-2), optionally as the replacement of an expired card (FR-8). A revolving card can
/// start with a debt (revolving R8); a revolving replacement takes over the old card's debt (FR-9).
/// </summary>
public sealed class CreateCardHandler(
    ICardRepository cards,
    CardInputChecker checker,
    IDebtEntryRepository debtEntries,
    RevolvingCardRules revolvingRules,
    IUnitOfWork unitOfWork,
    GetCardHandler getCard,
    TimeProvider timeProvider)
{
    public async Task<Result<CardResponse>> HandleAsync(
        CardRequest request, Guid? replacesCardId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var input = await checker.CheckAsync(request, existing: null, cancellationToken);
        if (input.IsFailure)
        {
            return Result.Failure<CardResponse>(input.Errors);
        }

        var now = timeProvider.GetUtcNow();
        var created = Card.Create(input.Value.Details, input.Value.CardNumber, input.Value.Iban, now);
        if (created.IsFailure)
        {
            return Result.Failure<CardResponse>(created.Errors);
        }

        if (created.Value.IsRevolving && request.StartingDebtCents is { } startingDebt)
        {
            var entry = DebtEntry.Create(
                created.Value.Id, DebtEntryKind.Starting, request.StartingDebtDate ?? timeProvider.Today(),
                Money.FromCents(startingDebt), null, now);
            if (entry.IsFailure)
            {
                return Result.Failure<CardResponse>(entry.Errors.Select(e => e with { Field = "startingDebtCents" }));
            }
            debtEntries.Add(entry.Value);
        }

        if (replacesCardId is { } oldId)
        {
            var old = await cards.GetAsync(oldId, includeSensitiveData: false, cancellationToken);
            if (old is null)
            {
                return Result.Failure<CardResponse>(CardErrors.ReplacementUnknown with { Field = "replacesCardId" });
            }
            var linked = old.LinkReplacement(created.Value, timeProvider.Today(), now);
            if (linked.IsFailure)
            {
                return Result.Failure<CardResponse>(linked.Errors);
            }
            await revolvingRules.MoveDebtAsync(old, created.Value, timeProvider.Today(), now, cancellationToken);
        }

        cards.Add(created.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await getCard.HandleAsync(created.Value.Id, cancellationToken);
    }
}

/// <summary>
/// Edits a card (FR-4). A card with purchases keeps allowing installments. When its paying account or
/// connection changes, card payments already paid automatically are stored first, so they stay paid
/// (installments FR-8).
/// </summary>
public sealed class UpdateCardHandler(
    ICardRepository cards,
    CardInputChecker checker,
    ICardPurchaseRepository purchases,
    ICardPaymentRepository cardPayments,
    CardPaymentLedger cardPaymentLedger,
    RevolvingCardRules revolvingRules,
    RevolvingLedger revolvingLedger,
    IRevolvingBillRepository revolvingBills,
    IUnitOfWork unitOfWork,
    GetCardHandler getCard,
    TimeProvider timeProvider)
{
    public async Task<Result<CardResponse>> HandleAsync(Guid id, CardRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var card = await cards.GetAsync(id, includeSensitiveData: true, cancellationToken);
        if (card is null)
        {
            return Result.Failure<CardResponse>(CardErrors.NotFound);
        }
        if (card.IsReadOnly)
        {
            return Result.Failure<CardResponse>(CardErrors.ReadOnly);
        }

        var input = await checker.CheckAsync(request, card, cancellationToken);
        if (input.IsFailure)
        {
            return Result.Failure<CardResponse>(input.Errors);
        }

        var details = input.Value.Details;
        if (card.AllowsInstallments && !Card.AllowsInstallmentsFor(details) && await purchases.AnyForCardAsync(card.Id, cancellationToken))
        {
            return Result.Failure<CardResponse>(CardErrors.InstallmentsInUse);
        }
        // The type cannot change to or from Revolving once the card has revolving activity (revolving FR-1).
        if ((card.IsRevolving != (details.Type == CardType.Revolving)) && await revolvingRules.HasActivityAsync(card.Id, cancellationToken))
        {
            return Result.Failure<CardResponse>(CardErrors.TypeLocked);
        }

        var today = timeProvider.Today();
        var now = timeProvider.GetUtcNow();
        if (card.PaysFromAccountDirectly
            && (details.Billing?.PaysFromAccountDirectly != true || details.Billing.PayingAccountId != card.PayingAccountId))
        {
            // Before the card changes, while the schedule still pays from the old account.
            var book = await cardPaymentLedger.LoadAsync(cancellationToken);
            if (book.Schedule(card.Id) is { } schedule)
            {
                await CardPaymentLedger.KeepAutomaticPaymentsAsync(cardPayments, schedule, today, now, cancellationToken);
            }
        }

        if (card.IsRevolving && card.PaysFromAccountDirectly
            && (details.Revolving?.PaysFromAccountDirectly != true || details.Revolving.PayingAccountId != card.PayingAccountId))
        {
            // Monthly bills paid automatically stay paid when the paying account or connection changes.
            if ((await revolvingLedger.LoadAsync(cancellationToken)).Calculator(card.Id) is { } calculator)
            {
                await RevolvingLedger.KeepAutomaticPaymentsAsync(revolvingBills, calculator, now, cancellationToken);
            }
        }

        var updated = card.Update(details, input.Value.CardNumber, input.Value.Iban, today, now);
        if (updated.IsFailure)
        {
            return Result.Failure<CardResponse>(updated.Errors);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await getCard.HandleAsync(id, cancellationToken);
    }
}

/// <summary>Block (FR-7). A revolving card can name a revolving replacement that takes over its debt (revolving FR-9).</summary>
public sealed class BlockCardHandler(
    ICardRepository cards, CardStateChanger changer, RevolvingCardRules revolvingRules, TimeProvider timeProvider)
{
    public async Task<Result<CardResponse>> HandleAsync(Guid id, CardStateRequest request, CancellationToken cancellationToken)
    {
        Card? replacement = null;
        if (request?.ReplacementCardId is { } replacementId)
        {
            replacement = await cards.GetAsync(replacementId, includeSensitiveData: false, cancellationToken);
            if (replacement is null)
            {
                return Result.Failure<CardResponse>(CardErrors.ReplacementUnknown);
            }
        }

        return await changer.ChangeAsync(id, async card =>
        {
            var today = timeProvider.Today();
            var now = timeProvider.GetUtcNow();
            if (replacement is not null && !card.IsRevolving)
            {
                return Result.Failure(CardErrors.ReplacementNotRevolving with { Message = "Only a revolving card names its replacement when it is blocked." });
            }
            var blocked = card.Block(today, request?.Reason, now);
            if (blocked.IsFailure || replacement is null)
            {
                return blocked;
            }
            var linked = card.LinkReplacement(replacement, today, now);
            if (linked.IsSuccess)
            {
                await revolvingRules.MoveDebtAsync(card, replacement, today, now, cancellationToken);
            }
            return linked;
        }, cancellationToken);
    }
}

/// <summary>Unblock (FR-7).</summary>
public sealed class UnblockCardHandler(CardStateChanger changer, TimeProvider timeProvider)
{
    public Task<Result<CardResponse>> HandleAsync(Guid id, CancellationToken cancellationToken) =>
        changer.ChangeAsync(id, card => card.Unblock(timeProvider.GetUtcNow()), cancellationToken);
}

/// <summary>Archive instead of delete (FR-5). A revolving card with debt cannot be archived (revolving FR-9).</summary>
public sealed class ArchiveCardHandler(CardStateChanger changer, RevolvingCardRules revolvingRules, TimeProvider timeProvider)
{
    public Task<Result<CardResponse>> HandleAsync(Guid id, CancellationToken cancellationToken) =>
        changer.ChangeAsync(id, async card => card.Archive(
            timeProvider.Today(), timeProvider.GetUtcNow(), await revolvingRules.DebtAsync(card.Id, cancellationToken)), cancellationToken);
}

/// <summary>
/// Mark as expired, optionally naming an existing replacement card (FR-8). A revolving replacement takes
/// over the debt of a revolving card (revolving FR-9).
/// </summary>
public sealed class ExpireCardHandler(
    ICardRepository cards, CardStateChanger changer, RevolvingCardRules revolvingRules, TimeProvider timeProvider)
{
    public async Task<Result<CardResponse>> HandleAsync(Guid id, ExpireCardRequest request, CancellationToken cancellationToken)
    {
        Card? replacement = null;
        if (request?.ReplacementCardId is { } replacementId)
        {
            replacement = await cards.GetAsync(replacementId, includeSensitiveData: false, cancellationToken);
            if (replacement is null)
            {
                return Result.Failure<CardResponse>(CardErrors.ReplacementUnknown);
            }
        }

        return await changer.ChangeAsync(
            id,
            async card =>
            {
                var today = timeProvider.Today();
                var now = timeProvider.GetUtcNow();
                var expired = card.MarkExpired(today, request?.Reason, replacement, now);
                if (expired.IsSuccess && replacement is not null)
                {
                    await revolvingRules.MoveDebtAsync(card, replacement, today, now, cancellationToken);
                }
                return expired;
            },
            cancellationToken);
    }
}

/// <summary>Loads a card, applies one state change, saves, and returns the updated card.</summary>
public sealed class CardStateChanger(ICardRepository cards, IUnitOfWork unitOfWork, GetCardHandler getCard)
{
    public Task<Result<CardResponse>> ChangeAsync(Guid id, Func<Card, Result> change, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(change);
        return ChangeAsync(id, card => Task.FromResult(change(card)), cancellationToken);
    }

    public async Task<Result<CardResponse>> ChangeAsync(Guid id, Func<Card, Task<Result>> change, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(change);
        var card = await cards.GetAsync(id, includeSensitiveData: false, cancellationToken);
        if (card is null)
        {
            return Result.Failure<CardResponse>(CardErrors.NotFound);
        }

        var result = await change(card);
        if (result.IsFailure)
        {
            return Result.Failure<CardResponse>(result.Errors);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await getCard.HandleAsync(id, cancellationToken);
    }
}

/// <summary>Lists and extends the issuer or bank list (requirements A2, A3).</summary>
public sealed class CardListHandler<TEntry>(ICardListRepository<TEntry> entries, IUnitOfWork unitOfWork)
    where TEntry : CardListEntry
{
    public async Task<IReadOnlyList<CardListEntryResponse>> ListAsync(CancellationToken cancellationToken)
    {
        var list = await entries.ListAsync(cancellationToken);
        return list
            .OrderBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(CardMapping.ToResponse)
            .ToList();
    }

    public async Task<Result<CardListEntryResponse>> CreateAsync(
        CardListEntryRequest request, Func<string?, Result<TEntry>> create, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(create);
        var entry = create(request?.Name);
        if (entry.IsFailure)
        {
            return Result.Failure<CardListEntryResponse>(entry.Errors);
        }
        if (await entries.NameInUseAsync(entry.Value.Name, cancellationToken))
        {
            return Result.Failure<CardListEntryResponse>(
                new DomainError("list.name.inUse", "This name is already in the list.", "name"));
        }

        entries.Add(entry.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(CardMapping.ToResponse(entry.Value));
    }
}
