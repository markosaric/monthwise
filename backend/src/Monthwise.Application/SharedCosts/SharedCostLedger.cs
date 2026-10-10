using Monthwise.Application.Cards;
using Monthwise.Application.CashFlow;
using Monthwise.Application.Common;
using Monthwise.Application.Expenses;
using Monthwise.Application.Loans;
using Monthwise.Domain.Cards;
using Monthwise.Domain.CashFlow;
using Monthwise.Domain.Common;
using Monthwise.Domain.Expenses;
using Monthwise.Domain.Loans;
using Monthwise.Domain.SharedCosts;

namespace Monthwise.Application.SharedCosts;

/// <summary>
/// An issued shared bill or a shared loan payment (shared bills FR-2 to FR-5), with its contribution as
/// calculated from it: the participant's part, the cost's dates and account.
/// </summary>
public sealed record SharedCost(
    ContributionSource Source,
    CostShare Share,
    string CostName,
    Money Amount,
    DateOnly CostDate,
    ContributionValues Contribution);

/// <summary>What an input list shows about a contribution: its shared cost and the participant's current part.</summary>
public sealed record ContributionInfo(ContributionSource Source, string CostName, Money Part, bool IsEdited, bool NeedsAccount);

/// <summary>
/// All shared costs on one day, with their stored contributions. A contribution that is not stored is
/// calculated from its cost every time, so it always follows the cost (FR-6); a stored one (received or
/// edited) is never changed automatically.
/// </summary>
public sealed class SharedCostBook
{
    private readonly Dictionary<Guid, Participant> _participants;
    private readonly Dictionary<ContributionSource, SharedCost> _costs;
    private readonly Dictionary<ContributionSource, CashFlowInput> _stored;
    private readonly Dictionary<Guid, string> _accounts;

    internal SharedCostBook(
        DateOnly today,
        IEnumerable<Participant> participants,
        IEnumerable<SharedCost> costs,
        IEnumerable<CashFlowInput> storedContributions,
        IEnumerable<Card> accounts)
    {
        Today = today;
        _participants = participants.ToDictionary(p => p.Id);
        _costs = costs.ToDictionary(c => c.Source);
        _stored = storedContributions.Where(i => i.Contribution is not null).ToDictionary(i => i.Contribution!);
        _accounts = accounts.ToDictionary(c => c.Id, c => c.Name);
    }

    public DateOnly Today { get; }

    public IReadOnlyCollection<Participant> Participants => _participants.Values;

    public IReadOnlyCollection<SharedCost> Costs => _costs.Values;

    public SharedCost? Cost(ContributionSource source) => _costs.GetValueOrDefault(source);

    public CashFlowInput? Stored(ContributionSource source) => _stored.GetValueOrDefault(source);

    /// <summary>Stored contributions whose bill or loan payment is no longer shared (or no longer exists).</summary>
    public IEnumerable<CashFlowInput> Orphans => _stored.Where(s => !_costs.ContainsKey(s.Key)).Select(s => s.Value);

    public NamedReference Participant(Guid id) =>
        new(id, _participants.TryGetValue(id, out var participant) ? participant.Name : "Unknown");

    public ShareResponse? ToResponse(CostShare? share) =>
        share is null
            ? null
            : new ShareResponse(
                Participant(share.ParticipantId),
                share.Shares,
                share.ParticipantShares,
                share.Percentage,
                share.ContributionAccountId is { } id ? new NamedReference(id, _accounts.GetValueOrDefault(id, "Unknown")) : null);

    /// <summary>The calculated contribution of <paramref name="cost"/> as input details; without an account the account id is empty.</summary>
    public static InputDetails DetailsOf(SharedCost cost)
    {
        ArgumentNullException.ThrowIfNull(cost);
        var values = cost.Contribution;
        return new InputDetails(
            values.PayerId,
            ContributionValues.AgreedContributionsCategoryId,
            values.AccountId ?? Guid.Empty,
            values.Amount,
            values.IssueDate,
            values.ExpectedDate,
            null,
            values.Description);
    }

    /// <summary>What an input list shows about a stored contribution; null for any other input.</summary>
    public ContributionInfo? InfoOf(CashFlowInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return input.Contribution is { } source && _costs.TryGetValue(source, out var cost) ? Info(cost, input) : null;
    }

    /// <summary>Calculated contributions (not stored) expected between the dates, inclusive.</summary>
    public IEnumerable<InputEntry> CalculatedBetween(DateOnly from, DateOnly to) =>
        Calculated().Where(e => e.Details.ExpectedDate >= from && e.Details.ExpectedDate <= to);

    /// <summary>Calculated contributions expected on or before <paramref name="to"/> (for the expected balance).</summary>
    public IEnumerable<InputEntry> CalculatedExpectedUpTo(DateOnly to) => Calculated().Where(e => e.Details.ExpectedDate <= to);

    /// <summary>The contribution of a shared cost, stored or calculated; null when the cost is not shared.</summary>
    public ContributionResponse? ContributionOf(ContributionSource source) =>
        _costs.TryGetValue(source, out var cost) ? ToResponse(cost) : null;

    /// <summary>The contributions of all shared costs.</summary>
    public IEnumerable<ContributionResponse> Contributions() => _costs.Values.Select(ToResponse);

    public ContributionResponse ToResponse(SharedCost cost)
    {
        ArgumentNullException.ThrowIfNull(cost);
        var stored = Stored(cost.Source);
        var source = cost.Source;
        return new ContributionResponse(
            source.Kind,
            source.BillId,
            source.LoanId,
            source.Month?.ToString(),
            source.PaymentKind,
            cost.CostName,
            Participant(cost.Share.ParticipantId),
            cost.Contribution.Amount.Cents,
            stored?.Id,
            (stored?.Amount ?? cost.Contribution.Amount).Cents,
            stored?.ExpectedDate ?? cost.Contribution.ExpectedDate,
            stored?.ReceivedDate,
            stored?.IsReceived ?? false,
            stored?.IsEdited ?? false,
            stored is null && cost.Contribution.AccountId is null,
            !(stored?.IsReceived ?? false) && (stored?.ExpectedDate ?? cost.Contribution.ExpectedDate) < Today,
            (stored?.AccountId ?? cost.Contribution.AccountId) is { } accountId
                ? new NamedReference(accountId, _accounts.GetValueOrDefault(accountId, "Unknown"))
                : null);
    }

    private IEnumerable<InputEntry> Calculated() =>
        _costs.Values
            .Where(c => !_stored.ContainsKey(c.Source) && c.Contribution.Amount.IsPositive)
            .Select(c => new InputEntry(null, null, DetailsOf(c), null, Info(c, null)));

    private static ContributionInfo Info(SharedCost cost, CashFlowInput? stored) =>
        new(cost.Source, cost.CostName, cost.Contribution.Amount, stored?.IsEdited ?? false, stored is null && cost.Contribution.AccountId is null);
}

/// <summary>Loads the <see cref="SharedCostBook"/> for today from the bills and the loan payments of a <see cref="LoanBook"/>.</summary>
public sealed class SharedCostLedger(
    IParticipantRepository participants,
    IBillRepository bills,
    ICashFlowInputRepository inputs,
    ICardRepository cards,
    TimeProvider timeProvider)
{
    public async Task<SharedCostBook> LoadAsync(LoanBook loans, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(loans);
        var today = timeProvider.Today();
        var people = (await participants.ListAsync(cancellationToken)).ToDictionary(p => p.Id);
        var accounts = await cards.ListAsync(cancellationToken);
        var byId = accounts.ToDictionary(c => c.Id);
        var primary = accounts
            .Where(c => c.IsPrimary && !PaidFromRules.IsCreditCard(c) && InputDetailsResolver.CanReceive(c, today))
            .Select(c => c.Id)
            .ToList();
        Guid? onlyPrimary = primary.Count == 1 ? primary[0] : null;

        var costs = new List<SharedCost>();
        foreach (var bill in await bills.ListAsync(cancellationToken))
        {
            if (bill.Share is not { } share || !bill.IsIssued || !people.TryGetValue(share.ParticipantId, out var participant))
            {
                continue;
            }
            Guid? account = bill.PaidFromId is { } id && byId.TryGetValue(id, out var card) && !PaidFromRules.IsCreditCard(card) ? id : null;
            costs.Add(new SharedCost(
                ContributionSource.ForBill(bill.Id),
                share,
                bill.Name,
                bill.Amount,
                bill.CashFlowDate!.Value,
                ContributionValues.Calculate(
                    share, participant.PayerId, bill.Amount, bill.IssueDate!.Value, bill.DueDate!.Value, account, onlyPrimary, bill.Name)));
        }
        foreach (var payment in loans.Payments())
        {
            if (payment.Share is not { } share || !people.TryGetValue(share.ParticipantId, out var participant))
            {
                continue;
            }
            var name = LoanPaymentName(loans.LoanReference(payment.LoanId).Name, payment);
            costs.Add(new SharedCost(
                ContributionSource.ForLoanPayment(payment.LoanId, payment.Month, payment.Kind),
                share,
                name,
                payment.Amount,
                payment.MonthDate,
                ContributionValues.Calculate(
                    share, participant.PayerId, payment.Amount, payment.BillDate, payment.DueDate, payment.AccountId, onlyPrimary, name)));
        }
        return new SharedCostBook(today, people.Values, costs, await inputs.ListContributionsAsync(cancellationToken), accounts);
    }

    /// <summary>"Apartment mortgage 18/60", or "Car lease, residual value".</summary>
    public static string LoanPaymentName(string loanName, ScheduledPayment payment)
    {
        ArgumentNullException.ThrowIfNull(payment);
        return payment.Kind == LoanPaymentKind.Residual
            ? $"{loanName}, residual value"
            : $"{loanName} {payment.Number}/{payment.Total}";
    }
}

/// <summary>
/// After a bill or loan changes: stored contributions whose cost is no longer shared are removed when
/// expected, and kept as ordinary inputs when received (FR-6).
/// </summary>
public sealed class ContributionCleaner(LoanLedger loanLedger, ICashFlowInputRepository inputs, IUnitOfWork unitOfWork, TimeProvider timeProvider)
{
    public async Task CleanUpAsync(CancellationToken cancellationToken)
    {
        var book = (await loanLedger.LoadAsync(cancellationToken)).Shared;
        var orphans = book.Orphans.ToList();
        if (orphans.Count == 0)
        {
            return;
        }
        var now = timeProvider.GetUtcNow();
        foreach (var input in orphans)
        {
            Release(inputs, input, now);
        }
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Removes an expected contribution, or keeps a received one as an ordinary input.</summary>
    public static void Release(ICashFlowInputRepository inputs, CashFlowInput input, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(input);
        if (input.IsReceived)
        {
            input.Detach(now);
        }
        else
        {
            inputs.Remove(input);
        }
    }
}
