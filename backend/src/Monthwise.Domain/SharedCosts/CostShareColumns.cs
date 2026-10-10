namespace Monthwise.Domain.SharedCosts;

/// <summary>
/// Reads and writes a <see cref="CostShare"/> kept as four nullable columns on an entity (recurring
/// bill, bill, loan, loan payment): all set for a shared cost, all null otherwise.
/// </summary>
public static class CostShareColumns
{
    public static CostShare? Read(Guid? participantId, int? shares, int? participantShares, Guid? contributionAccountId) =>
        participantId is { } participant && shares is { } count && participantShares is { } own
            ? new CostShare(participant, count, own, contributionAccountId)
            : null;

    public static (Guid? ParticipantId, int? Shares, int? ParticipantShares, Guid? ContributionAccountId) Write(CostShare? share) =>
        share is null
            ? (null, null, null, null)
            : (share.ParticipantId, share.Shares, share.ParticipantShares, share.ContributionAccountId);
}
