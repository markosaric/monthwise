using Monthwise.Application.CashFlow;
using Monthwise.Domain.Loans;
using Monthwise.Domain.SharedCosts;

namespace Monthwise.Application.SharedCosts;

/// <summary>Add or edit a participant (FR-1).</summary>
public sealed record ParticipantRequest(string Name, string? Description);

/// <summary>A participant (FR-1). <see cref="CanBeDeleted"/> is true while it shares no cost.</summary>
public sealed record ParticipantResponse(Guid Id, string Name, string? Description, bool IsArchived, bool CanBeDeleted);

/// <summary>
/// Shares a recurring bill, bill, loan or loan payment with one participant (FR-2): the cost is split into
/// <see cref="Shares"/> (2 to 4) and the participant pays <see cref="ParticipantShares"/> of them.
/// <see cref="ContributionAccountId"/> is the account the contributions are paid to, when the user chooses
/// one (FR-5).
/// </summary>
public sealed record ShareRequest(Guid ParticipantId, int Shares, int ParticipantShares, Guid? ContributionAccountId);

/// <summary>How a cost is shared; <see cref="Percentage"/> is the participant's shares in whole percent.</summary>
public sealed record ShareResponse(
    NamedReference Participant,
    int Shares,
    int ParticipantShares,
    int Percentage,
    NamedReference? ContributionAccount);

/// <summary>
/// The contribution of a shared bill or loan payment (FR-5, FR-6). <see cref="InputId"/> is set once it
/// is stored (received or edited); <see cref="PartCents"/> is the participant's current part, so the UI
/// can show the difference of a received contribution. <see cref="NeedsAccount"/> is true while no
/// account applies to it; <see cref="IsOverdue"/> while it is expected and its expected date has passed.
/// </summary>
public sealed record ContributionResponse(
    ContributionSourceKind Kind,
    Guid? BillId,
    Guid? LoanId,
    string? Month,
    LoanPaymentKind? PaymentKind,
    string CostName,
    NamedReference Participant,
    long PartCents,
    Guid? InputId,
    long AmountCents,
    DateOnly ExpectedDate,
    DateOnly? ReceivedDate,
    bool IsReceived,
    bool IsEdited,
    bool NeedsAccount,
    bool IsOverdue,
    NamedReference? Account);

/// <summary>
/// Edits a contribution (FR-6): the account it is paid to, its amount and dates. The payer and category
/// stay those of the participant and Agreed contributions. An edited contribution keeps its values until
/// it is reset to the part.
/// </summary>
public sealed record ContributionRequest(Guid AccountId, long AmountCents, DateOnly ExpectedDate, DateOnly? ReceivedDate, string? Description);

/// <summary>Ticks a contribution as received (FR-5): today unless a date is given, to its account unless one is given.</summary>
public sealed record ContributionReceivedRequest(DateOnly? ReceivedDate, Guid? AccountId);

/// <summary>A shared recurring bill or loan of a participant, as the Shared costs screen lists it (FR-8).</summary>
public sealed record SharedCostResponse(ContributionSourceKind Kind, Guid Id, string Name, ShareResponse Share);

/// <summary>
/// One participant on the Shared costs screen (FR-8): what they share, their parts and contributions of the
/// month, and their outstanding contributions up to the end of the month, oldest first.
/// </summary>
public sealed record ParticipantSharedCostsResponse(
    ParticipantResponse Participant,
    IReadOnlyList<SharedCostResponse> Costs,
    long MonthPartsCents,
    long MonthReceivedCents,
    long OutstandingCents,
    IReadOnlyList<ContributionResponse> MonthContributions,
    IReadOnlyList<ContributionResponse> Outstanding);

/// <summary>The Shared costs screen for a month (<c>yyyy-MM</c>, FR-8).</summary>
public sealed record SharedCostsResponse(string Month, IReadOnlyList<ParticipantSharedCostsResponse> Participants);

/// <summary>
/// Shared costs of a month in the monthly report (FR-9): the full amount of shared bills and loan
/// payments, the participants' parts, the household's own part, and the month's contributions.
/// </summary>
public sealed record ReportSharedCostsResponse(
    long TotalCents,
    long ParticipantsPartCents,
    long OwnPartCents,
    long ContributionsReceivedCents,
    long ContributionsOutstandingCents);
