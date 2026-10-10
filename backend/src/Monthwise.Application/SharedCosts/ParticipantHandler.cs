using Monthwise.Application.CashFlow;
using Monthwise.Application.Common;
using Monthwise.Domain.CashFlow;
using Monthwise.Domain.Common;
using Monthwise.Domain.SharedCosts;

namespace Monthwise.Application.SharedCosts;

/// <summary>
/// Participants on the Shared costs screen (FR-1): list, add, edit, archive, restore and delete. Each
/// participant is also the payer of their contributions: the payer with the same name is used, or one is
/// added, and renaming the participant renames the payer.
/// </summary>
public sealed class ParticipantHandler(
    IParticipantRepository participants,
    IPayerRepository payers,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    /// <summary>Active participants by name, then archived ones when <paramref name="includeArchived"/>.</summary>
    public async Task<IReadOnlyList<ParticipantResponse>> ListAsync(bool includeArchived, CancellationToken cancellationToken)
    {
        var list = (await participants.ListAsync(cancellationToken))
            .Where(p => includeArchived || !p.IsArchived)
            .OrderBy(p => p.IsArchived ? 1 : 0)
            .ThenBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        var responses = new List<ParticipantResponse>(list.Count);
        foreach (var participant in list)
        {
            responses.Add(await ResponseAsync(participant, cancellationToken));
        }
        return responses;
    }

    public async Task<Result<ParticipantResponse>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var participant = await participants.GetAsync(id, cancellationToken);
        return participant is null
            ? Result.Failure<ParticipantResponse>(SharedCostErrors.ParticipantNotFound)
            : Result.Success(await ResponseAsync(participant, cancellationToken));
    }

    public async Task<Result<ParticipantResponse>> CreateAsync(ParticipantRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var now = timeProvider.GetUtcNow();
        var details = new ParticipantDetails(request.Name, request.Description);
        var errors = Participant.Create(details, Guid.Empty, now).Errors.ToList();
        var name = Participant.ValidateName(request.Name);
        Payer? payer = null;
        if (name.IsSuccess)
        {
            if (await participants.NameInUseAsync(name.Value, null, cancellationToken))
            {
                errors.Insert(0, SharedCostErrors.ParticipantNameInUse);
            }
            else
            {
                payer = await payers.FindByNameAsync(name.Value, cancellationToken);
                if (payer is not null && (await participants.ListAsync(cancellationToken)).Any(p => p.PayerId == payer.Id))
                {
                    errors.Insert(0, SharedCostErrors.ParticipantPayerNameInUse);
                }
            }
        }
        if (errors.Count > 0)
        {
            return Result.Failure<ParticipantResponse>(errors);
        }
        if (payer is null)
        {
            payer = Payer.Create(name.Value, now).Value;
            payers.Add(payer);
        }
        payer.Restore();
        var participant = Participant.Create(details, payer.Id, now).Value;
        participants.Add(participant);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(await ResponseAsync(participant, cancellationToken));
    }

    public async Task<Result<ParticipantResponse>> UpdateAsync(Guid id, ParticipantRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var participant = await participants.GetAsync(id, cancellationToken);
        if (participant is null)
        {
            return Result.Failure<ParticipantResponse>(SharedCostErrors.ParticipantNotFound);
        }
        var now = timeProvider.GetUtcNow();
        var details = new ParticipantDetails(request.Name, request.Description);
        var name = Participant.ValidateName(request.Name);
        var errors = new List<DomainError>();
        var payer = await payers.GetAsync(participant.PayerId, cancellationToken);
        if (name.IsSuccess && !string.Equals(name.Value, participant.Name, StringComparison.Ordinal))
        {
            if (await participants.NameInUseAsync(name.Value, participant.Id, cancellationToken))
            {
                errors.Add(SharedCostErrors.ParticipantNameInUse);
            }
            else if (await payers.FindByNameAsync(name.Value, cancellationToken) is { } other && other.Id != participant.PayerId)
            {
                errors.Add(SharedCostErrors.ParticipantPayerNameInUse);
            }
        }
        var updated = participant.Update(details, now);
        errors.InsertRange(0, updated.Errors);
        if (errors.Count > 0)
        {
            return Result.Failure<ParticipantResponse>(errors.Distinct());
        }
        payer?.Rename(participant.Name);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(await ResponseAsync(participant, cancellationToken));
    }

    public Task<Result<ParticipantResponse>> ArchiveAsync(Guid id, CancellationToken cancellationToken) =>
        ChangeAsync(id, p => p.Archive(timeProvider.Today(), timeProvider.GetUtcNow()), cancellationToken);

    public Task<Result<ParticipantResponse>> RestoreAsync(Guid id, CancellationToken cancellationToken) =>
        ChangeAsync(id, p => p.Restore(timeProvider.GetUtcNow()), cancellationToken);

    /// <summary>Deletes a participant that shares no cost; its payer stays, so contributions keep their payer.</summary>
    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var participant = await participants.GetAsync(id, cancellationToken);
        if (participant is null)
        {
            return Result.Failure(SharedCostErrors.ParticipantNotFound);
        }
        if (await participants.IsUsedAsync(id, cancellationToken))
        {
            return Result.Failure(SharedCostErrors.ParticipantInUse);
        }
        participants.Remove(participant);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<Result<ParticipantResponse>> ChangeAsync(Guid id, Action<Participant> change, CancellationToken cancellationToken)
    {
        var participant = await participants.GetAsync(id, cancellationToken);
        if (participant is null)
        {
            return Result.Failure<ParticipantResponse>(SharedCostErrors.ParticipantNotFound);
        }
        change(participant);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(await ResponseAsync(participant, cancellationToken));
    }

    internal async Task<ParticipantResponse> ResponseAsync(Participant participant, CancellationToken cancellationToken) =>
        new(
            participant.Id,
            participant.Name,
            participant.Description,
            participant.IsArchived,
            !await participants.IsUsedAsync(participant.Id, cancellationToken));
}
