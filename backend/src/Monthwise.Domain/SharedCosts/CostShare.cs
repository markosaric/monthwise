using Monthwise.Domain.Common;

namespace Monthwise.Domain.SharedCosts;

/// <summary>
/// How a bill or loan payment is shared (requirements: docs/feature-requirements/shared-bills.md, FR-2):
/// split into <see cref="Shares"/> equal parts, of which the participant pays
/// <see cref="ParticipantShares"/>; the household always keeps at least one.
/// <see cref="ContributionAccountId"/> is the account the user chose for the contributions (FR-5).
/// </summary>
public sealed record CostShare(Guid ParticipantId, int Shares, int ParticipantShares, Guid? ContributionAccountId = null)
{
    public const int MinShares = 2;
    public const int MaxShares = 4;
    public const int DefaultShares = 2;
    public const int DefaultParticipantShares = 1;

    public static Result<CostShare> Create(Guid participantId, int shares, int participantShares, Guid? contributionAccountId)
    {
        var errors = new List<DomainError>();
        if (participantId == Guid.Empty)
        {
            errors.Add(SharedCostErrors.ShareParticipantRequired);
        }
        if (shares is < MinShares or > MaxShares)
        {
            errors.Add(SharedCostErrors.SharesInvalid);
        }
        else if (participantShares < 1 || participantShares >= shares)
        {
            errors.Add(SharedCostErrors.ParticipantSharesInvalid);
        }
        return errors.Count > 0
            ? Result.Failure<CostShare>(errors)
            : Result.Success(new CostShare(
                participantId,
                shares,
                participantShares,
                contributionAccountId is { } id && id != Guid.Empty ? id : null));
    }

    /// <summary>
    /// The participant's part of <paramref name="amount"/> (FR-4): amount × participant shares ÷ shares,
    /// rounded to the nearest cent, half a cent rounded up.
    /// </summary>
    public Money PartOf(Money amount) =>
        Money.FromCents(checked((2 * amount.Cents * ParticipantShares + Shares) / (2L * Shares)));

    /// <summary>What the household pays itself (FR-4): the amount minus the participant's part.</summary>
    public Money OwnPartOf(Money amount) => amount - PartOf(amount);

    /// <summary>The participant's shares as a whole percentage, for display (FR-2).</summary>
    public int Percentage => (int)Math.Round(100m * ParticipantShares / Shares, MidpointRounding.AwayFromZero);

    /// <summary>The same shares with another contribution account.</summary>
    public CostShare WithContributionAccount(Guid? accountId) =>
        this with { ContributionAccountId = accountId is { } id && id != Guid.Empty ? id : null };
}
