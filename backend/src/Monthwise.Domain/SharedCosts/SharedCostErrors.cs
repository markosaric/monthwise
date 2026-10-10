using Monthwise.Domain.Common;

namespace Monthwise.Domain.SharedCosts;

/// <summary>Errors of the shared cost rules. Field names match the API request fields.</summary>
public static class SharedCostErrors
{
    public static readonly DomainError ParticipantNameRequired =
        new("participant.name.required", "Name is required.", "name");
    public static readonly DomainError ParticipantNameTooLong =
        new("participant.name.tooLong", $"Name can be at most {Participant.NameMaxLength} characters.", "name");
    public static readonly DomainError ParticipantNameInUse =
        new("participant.name.inUse", "Another participant already has this name.", "name");
    public static readonly DomainError ParticipantPayerNameInUse = new(
        "participant.name.payerInUse", "Another payer already has this name.", "name");
    public static readonly DomainError ParticipantDescriptionTooLong = new(
        "participant.description.tooLong",
        $"Description can be at most {Participant.DescriptionMaxLength} characters.",
        "description");
    public static readonly DomainError ParticipantNotFound =
        new("participant.notFound", "Participant not found.", Kind: ErrorKind.NotFound);
    public static readonly DomainError ParticipantInUse = new(
        "participant.inUse", "This participant shares costs; archive it instead.", Kind: ErrorKind.Conflict);

    public static readonly DomainError ShareParticipantRequired =
        new("share.participant.required", "Choose who shares the cost.", "share.participantId");
    public static readonly DomainError ShareParticipantUnknown =
        new("share.participant.unknown", "This participant does not exist.", "share.participantId");
    public static readonly DomainError ShareParticipantArchived =
        new("share.participant.archived", "This participant is archived.", "share.participantId");
    public static readonly DomainError SharesInvalid = new(
        "share.shares.invalid",
        $"Shares must be between {CostShare.MinShares} and {CostShare.MaxShares}.",
        "share.shares");
    public static readonly DomainError ParticipantSharesInvalid = new(
        "share.participantShares.invalid",
        "The participant pays at least one share and fewer than all of them.",
        "share.participantShares");
    public static readonly DomainError ContributionAccountUnusable = new(
        "share.contributionAccount.unusable",
        "Choose an account that is not blocked, expired or archived.",
        "share.contributionAccountId");

    public static readonly DomainError NotShared =
        new("contribution.notShared", "This bill or loan payment is not shared.", Kind: ErrorKind.NotFound);
    public static readonly DomainError BillNotIssued = new(
        "contribution.billNotIssued", "The bill has no contribution until it is issued.", Kind: ErrorKind.Conflict);
    public static readonly DomainError ContributionNotStored = new(
        "contribution.notStored", "The contribution follows its bill or loan payment already.", Kind: ErrorKind.Conflict);
    public static readonly DomainError ContributionNeedsAccount = new(
        "contribution.account.required", "Choose the account the contribution is paid to.", "accountId");
    public static readonly DomainError ContributionCannotBeDeleted = new(
        "contribution.cannotBeDeleted",
        "A contribution cannot be deleted; stop sharing the bill or loan payment instead.",
        Kind: ErrorKind.Conflict);
}
