using Monthwise.Application.Cards;
using Monthwise.Application.CashFlow;
using Monthwise.Application.Common;
using Monthwise.Domain.Common;
using Monthwise.Domain.Expenses;
using Monthwise.Domain.SharedCosts;

namespace Monthwise.Application.SharedCosts;

/// <summary>
/// Checks a <see cref="ShareRequest"/> (shared bills FR-2): the share rules, a participant that exists
/// and is not archived, and a contribution account that can receive money. The participant and account
/// the cost already has stay allowed.
/// </summary>
public sealed class ShareResolver(IParticipantRepository participants, ICardRepository cards, TimeProvider timeProvider)
{
    public async Task<Result<CostShare?>> ResolveAsync(ShareRequest? request, CostShare? existing, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Result.Success<CostShare?>(null);
        }
        var created = CostShare.Create(request.ParticipantId, request.Shares, request.ParticipantShares, request.ContributionAccountId);
        var errors = created.IsFailure ? created.Errors.ToList() : [];
        if (request.ParticipantId != Guid.Empty)
        {
            var participant = await participants.GetAsync(request.ParticipantId, cancellationToken);
            if (participant is null)
            {
                errors.Add(SharedCostErrors.ShareParticipantUnknown);
            }
            else if (participant.IsArchived && participant.Id != existing?.ParticipantId)
            {
                errors.Add(SharedCostErrors.ShareParticipantArchived);
            }
        }
        if (request.ContributionAccountId is { } accountId && accountId != Guid.Empty && accountId != existing?.ContributionAccountId)
        {
            var account = await cards.GetAsync(accountId, includeSensitiveData: false, cancellationToken);
            if (account is null || PaidFromRules.IsCreditCard(account) || !InputDetailsResolver.CanReceive(account, timeProvider.Today()))
            {
                errors.Add(SharedCostErrors.ContributionAccountUnusable);
            }
        }
        return errors.Count > 0 ? Result.Failure<CostShare?>(errors) : Result.Success<CostShare?>(created.Value);
    }
}
