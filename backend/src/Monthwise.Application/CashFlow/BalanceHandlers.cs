using Monthwise.Application.Cards;
using Monthwise.Application.Common;
using Monthwise.Domain.Cards;
using Monthwise.Domain.CashFlow;
using Monthwise.Domain.Common;

namespace Monthwise.Application.CashFlow;

/// <summary>Calculates account balances from balance entries and inputs (FR-10).</summary>
public sealed class AccountBalanceQuery(
    IBalanceEntryRepository balanceEntries,
    ICashFlowInputRepository inputs,
    InputLedger ledger,
    TimeProvider timeProvider)
{
    /// <summary>Balances of the given accounts: current (today) and expected at the end of this month.</summary>
    public async Task<IReadOnlyDictionary<Guid, AccountBalanceResponse>> ForAccountsAsync(
        IReadOnlyCollection<Guid> accountIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(accountIds);
        var today = timeProvider.Today();
        var calculators = await CalculatorsAsync(today, cancellationToken);
        var expected = await ledger.ExpectedUpToAsync(YearMonth.FromDate(today).LastDay, cancellationToken);

        return accountIds.Distinct().ToDictionary(id => id, id =>
        {
            var balance = calculators.TryGetValue(id, out var calculator) ? calculator : new AccountBalance([], []);
            var current = balance.BalanceOn(today);
            var expectedBalance = balance.ExpectedBalance(
                today, expected.Where(e => e.Details.AccountId == id).Select(e => e.Details.Amount));
            return new AccountBalanceResponse(id, current.Cents, expectedBalance.Cents, balance.HasCheckpoint);
        });
    }

    /// <summary>One calculator per account that has entries or received inputs.</summary>
    public async Task<IReadOnlyDictionary<Guid, AccountBalance>> CalculatorsAsync(DateOnly receivedUpTo, CancellationToken cancellationToken)
    {
        var entries = await balanceEntries.ListAsync(null, cancellationToken);
        var received = await inputs.ListReceivedUpToAsync(receivedUpTo, cancellationToken);
        var accountIds = entries.Select(e => e.AccountId).Concat(received.Select(r => r.AccountId)).Distinct();
        return accountIds.ToDictionary(id => id, id => new AccountBalance(
            entries.Where(e => e.AccountId == id).Select(ToCheckpoint),
            received.Where(r => r.AccountId == id).Select(r => new ReceivedAmount(r.ReceivedDate!.Value, r.Amount))));
    }

    public static BalanceCheckpoint ToCheckpoint(BalanceEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return new BalanceCheckpoint(entry.Id, entry.Date, entry.Balance, entry.CreatedAt);
    }
}

/// <summary>Starting balance and corrections of one account (FR-9), with its balance.</summary>
public sealed class BalanceEntryHandler(
    IBalanceEntryRepository balanceEntries,
    ICardRepository cards,
    AccountBalanceQuery balances,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<Result<AccountBalanceDetailsResponse>> GetAsync(Guid accountId, CancellationToken cancellationToken)
    {
        if (await cards.GetAsync(accountId, includeSensitiveData: false, cancellationToken) is null)
        {
            return Result.Failure<AccountBalanceDetailsResponse>(CardErrors.NotFound);
        }
        var balance = (await balances.ForAccountsAsync([accountId], cancellationToken))[accountId];
        var entries = await balanceEntries.ListAsync(accountId, cancellationToken);
        // Differences use every received input, also ones dated after today.
        var calculators = await balances.CalculatorsAsync(DateOnly.MaxValue, cancellationToken);
        var calculator = calculators.TryGetValue(accountId, out var found) ? found : new AccountBalance([], []);
        var responses = entries
            .OrderByDescending(e => e.Date)
            .ThenByDescending(e => e.CreatedAt)
            .Select(e => ToResponse(e, calculator))
            .ToList();
        return Result.Success(new AccountBalanceDetailsResponse(balance, responses));
    }

    public async Task<Result<BalanceEntryResponse>> CreateAsync(
        Guid accountId, BalanceEntryRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var account = await cards.GetAsync(accountId, includeSensitiveData: false, cancellationToken);
        if (account is null)
        {
            return Result.Failure<BalanceEntryResponse>(CardErrors.NotFound);
        }
        if (account.ArchivedOn is not null)
        {
            return Result.Failure<BalanceEntryResponse>(CardErrors.ReadOnly);
        }
        var existing = await balanceEntries.ListAsync(accountId, cancellationToken);
        if (request.Kind == BalanceEntryKind.Starting && existing.Any(e => e.Kind == BalanceEntryKind.Starting))
        {
            return Result.Failure<BalanceEntryResponse>(CashFlowErrors.StartingBalanceExists);
        }
        var created = BalanceEntry.Create(
            accountId, request.Kind, request.Date, Money.FromCents(request.BalanceCents), request.Note, timeProvider.GetUtcNow());
        if (created.IsFailure)
        {
            return Result.Failure<BalanceEntryResponse>(created.Errors);
        }
        balanceEntries.Add(created.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(await RespondAsync(created.Value, cancellationToken));
    }

    /// <summary>Changes date, balance and note; the kind stays.</summary>
    public async Task<Result<BalanceEntryResponse>> UpdateAsync(
        Guid entryId, BalanceEntryRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var entry = await balanceEntries.GetAsync(entryId, cancellationToken);
        if (entry is null)
        {
            return Result.Failure<BalanceEntryResponse>(CashFlowErrors.BalanceEntryNotFound);
        }
        var updated = entry.Update(request.Date, Money.FromCents(request.BalanceCents), request.Note, timeProvider.GetUtcNow());
        if (updated.IsFailure)
        {
            return Result.Failure<BalanceEntryResponse>(updated.Errors);
        }
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(await RespondAsync(entry, cancellationToken));
    }

    public async Task<Result> DeleteAsync(Guid entryId, CancellationToken cancellationToken)
    {
        var entry = await balanceEntries.GetAsync(entryId, cancellationToken);
        if (entry is null)
        {
            return Result.Failure(CashFlowErrors.BalanceEntryNotFound);
        }
        balanceEntries.Remove(entry);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<BalanceEntryResponse> RespondAsync(BalanceEntry entry, CancellationToken cancellationToken)
    {
        var calculators = await balances.CalculatorsAsync(DateOnly.MaxValue, cancellationToken);
        return ToResponse(entry, calculators[entry.AccountId]);
    }

    public static BalanceEntryResponse ToResponse(BalanceEntry entry, AccountBalance calculator)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(calculator);
        return new BalanceEntryResponse(
            entry.Id, entry.AccountId, entry.Kind, entry.Date, entry.Balance.Cents,
            calculator.DifferenceOf(entry.Id).Cents, entry.Note);
    }
}
