using Monthwise.Application.Cards;
using Monthwise.Application.CashFlow;
using Monthwise.Application.Common;
using Monthwise.Application.Localization;
using Monthwise.Domain.Cards;
using Monthwise.Domain.CashFlow;
using Monthwise.Domain.Common;
using Monthwise.Domain.Lendings;

namespace Monthwise.Application.Lendings;

/// <summary>Money lent, as an output of the account it left on the lend date (money lending FR-7). Always paid.</summary>
public sealed record LendingOutput(DateOnly Date, Money Amount, Guid AccountId, Guid LendingId, Guid PayerId);

/// <summary>
/// Money coming back (FR-8): a received repayment on the account it arrived on, or the outstanding amount
/// expected on the lending's account on the expected return date.
/// </summary>
public sealed record LendingInput(DateOnly Date, Money Amount, Guid AccountId, Guid LendingId, Guid PayerId, bool IsReceived, Guid? RepaymentId);

/// <summary>All lendings on one day, with the names needed to show them.</summary>
public sealed class LendingBook
{
    /// <summary>
    /// The id of the Lending group in the report's inputs and outputs by category (FR-10). It is not an
    /// input or expense category; it only keys the group.
    /// </summary>
    public static readonly Guid GroupId = new("0199b0c0-0000-7000-8000-00000000a001");

    private readonly Dictionary<Guid, Card> _cards;
    private readonly Dictionary<Guid, Payer> _payers;

    internal LendingBook(DateOnly today, IReadOnlyList<Lending> lendings, IReadOnlyList<Card> cards, IReadOnlyList<Payer> payers)
    {
        Today = today;
        Lendings = lendings;
        _cards = cards.ToDictionary(c => c.Id);
        _payers = payers.ToDictionary(p => p.Id);
    }

    public DateOnly Today { get; }

    public IReadOnlyList<Lending> Lendings { get; }

    /// <summary>The name of the Lending group (FR-9, FR-10).</summary>
    public static string GroupName => ServerTexts.Translate("Lending");

    public NamedReference Borrower(Guid payerId) =>
        new(payerId, _payers.TryGetValue(payerId, out var payer) ? payer.Name : ServerTexts.Unknown);

    public NamedReference Account(Guid accountId) =>
        new(accountId, _cards.TryGetValue(accountId, out var card) ? card.Name : ServerTexts.Unknown);

    public IEnumerable<LendingOutput> Outputs() =>
        Lendings.Select(l => new LendingOutput(l.LendDate, l.Amount, l.FromAccountId, l.Id, l.PayerId));

    public IEnumerable<LendingOutput> OutputsBetween(DateOnly from, DateOnly to) =>
        Outputs().Where(o => o.Date >= from && o.Date <= to);

    /// <summary>Received repayments and, for every lending not returned, its outstanding amount expected back.</summary>
    public IEnumerable<LendingInput> Inputs() =>
        Lendings.SelectMany(l => l.Repayments
            .Select(r => new LendingInput(r.ReceivedDate, r.Amount, r.ToAccountId, l.Id, l.PayerId, IsReceived: true, r.Id))
            .Concat(l.Outstanding.IsPositive
                ? [new LendingInput(l.ExpectedReturnDate, l.Outstanding, l.FromAccountId, l.Id, l.PayerId, IsReceived: false, null)]
                : []));

    public IEnumerable<LendingInput> InputsBetween(DateOnly from, DateOnly to) =>
        Inputs().Where(i => i.Date >= from && i.Date <= to);

    public bool IsOverdue(Lending lending)
    {
        ArgumentNullException.ThrowIfNull(lending);
        return lending.StateOn(Today) == LendingState.Overdue;
    }

    public LendingResponse ToResponse(Lending lending)
    {
        ArgumentNullException.ThrowIfNull(lending);
        return new LendingResponse(
            lending.Id,
            Borrower(lending.PayerId),
            lending.Amount.Cents,
            lending.LendDate,
            Account(lending.FromAccountId),
            lending.ExpectedReturnDate,
            lending.Description,
            lending.Returned.Cents,
            lending.Outstanding.Cents,
            lending.StateOn(Today),
            lending.Repayments
                .Select(r => new RepaymentResponse(r.Id, r.Amount.Cents, Account(r.ToAccountId), r.ReceivedDate, r.Note))
                .ToList());
    }
}

/// <summary>Loads the <see cref="LendingBook"/> for today.</summary>
public sealed class LendingLedger(
    ILendingRepository lendings,
    ICardRepository cards,
    IPayerRepository payers,
    TimeProvider timeProvider)
{
    public async Task<LendingBook> LoadAsync(CancellationToken cancellationToken) =>
        new(
            timeProvider.Today(),
            await lendings.ListAsync(cancellationToken),
            await cards.ListAsync(cancellationToken),
            await payers.ListAsync(cancellationToken));
}
