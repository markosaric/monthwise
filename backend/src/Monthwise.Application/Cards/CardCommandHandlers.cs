using Monthwise.Application.Common;
using Monthwise.Domain.Cards;
using Monthwise.Domain.Common;

namespace Monthwise.Application.Cards;

/// <summary>Adds a card (FR-2), optionally as the replacement of an expired card (FR-8).</summary>
public sealed class CreateCardHandler(
    ICardRepository cards,
    CardInputChecker checker,
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
        }

        cards.Add(created.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await getCard.HandleAsync(created.Value.Id, cancellationToken);
    }
}

/// <summary>Edits a card (FR-4).</summary>
public sealed class UpdateCardHandler(
    ICardRepository cards,
    CardInputChecker checker,
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

        var updated = card.Update(input.Value.Details, input.Value.CardNumber, input.Value.Iban, timeProvider.GetUtcNow());
        if (updated.IsFailure)
        {
            return Result.Failure<CardResponse>(updated.Errors);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await getCard.HandleAsync(id, cancellationToken);
    }
}

/// <summary>Block (FR-7).</summary>
public sealed class BlockCardHandler(CardStateChanger changer, TimeProvider timeProvider)
{
    public Task<Result<CardResponse>> HandleAsync(Guid id, CardStateRequest request, CancellationToken cancellationToken) =>
        changer.ChangeAsync(id, card => card.Block(timeProvider.Today(), request?.Reason, timeProvider.GetUtcNow()), cancellationToken);
}

/// <summary>Unblock (FR-7).</summary>
public sealed class UnblockCardHandler(CardStateChanger changer, TimeProvider timeProvider)
{
    public Task<Result<CardResponse>> HandleAsync(Guid id, CancellationToken cancellationToken) =>
        changer.ChangeAsync(id, card => card.Unblock(timeProvider.GetUtcNow()), cancellationToken);
}

/// <summary>Archive instead of delete (FR-5).</summary>
public sealed class ArchiveCardHandler(CardStateChanger changer, TimeProvider timeProvider)
{
    public Task<Result<CardResponse>> HandleAsync(Guid id, CancellationToken cancellationToken) =>
        changer.ChangeAsync(id, card => card.Archive(timeProvider.Today(), timeProvider.GetUtcNow()), cancellationToken);
}

/// <summary>Mark as expired, optionally naming an existing replacement card (FR-8).</summary>
public sealed class ExpireCardHandler(ICardRepository cards, CardStateChanger changer, TimeProvider timeProvider)
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
            card => card.MarkExpired(timeProvider.Today(), request?.Reason, replacement, timeProvider.GetUtcNow()),
            cancellationToken);
    }
}

/// <summary>Loads a card, applies one state change, saves, and returns the updated card.</summary>
public sealed class CardStateChanger(ICardRepository cards, IUnitOfWork unitOfWork, GetCardHandler getCard)
{
    public async Task<Result<CardResponse>> ChangeAsync(Guid id, Func<Card, Result> change, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(change);
        var card = await cards.GetAsync(id, includeSensitiveData: false, cancellationToken);
        if (card is null)
        {
            return Result.Failure<CardResponse>(CardErrors.NotFound);
        }

        var result = change(card);
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
