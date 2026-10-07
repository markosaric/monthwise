using Monthwise.Domain.Common;

namespace Monthwise.Domain.CashFlow;

/// <summary>A balance checkpoint for the calculation: the balance at the end of <paramref name="Date"/>.</summary>
public sealed record BalanceCheckpoint(Guid Id, DateOnly Date, Money Balance, DateTimeOffset CreatedAt);

/// <summary>A received input for the calculation.</summary>
public readonly record struct ReceivedAmount(DateOnly ReceivedDate, Money Amount);

/// <summary>
/// The balance of one account (FR-10). The balance on a date is the latest checkpoint on or before
/// it, plus the inputs received after that checkpoint's date and on or before the date. Without a
/// checkpoint it starts from zero. Checkpoints on the same date are ordered by when they were entered.
/// </summary>
public sealed class AccountBalance
{
    private readonly List<BalanceCheckpoint> _checkpoints;
    private readonly List<ReceivedAmount> _received;

    public AccountBalance(IEnumerable<BalanceCheckpoint> checkpoints, IEnumerable<ReceivedAmount> received)
    {
        ArgumentNullException.ThrowIfNull(checkpoints);
        ArgumentNullException.ThrowIfNull(received);
        _checkpoints = checkpoints.OrderBy(c => c.Date).ThenBy(c => c.CreatedAt).ToList();
        _received = received.ToList();
    }

    public bool HasCheckpoint => _checkpoints.Count > 0;

    /// <summary>The balance at the end of <paramref name="date"/>.</summary>
    public Money BalanceOn(DateOnly date) => BalanceOn(date, _checkpoints);

    /// <summary>
    /// How much a checkpoint changed the balance: its balance minus what the app would have shown on
    /// its date without it (and without checkpoints entered after it on the same date).
    /// </summary>
    public Money DifferenceOf(Guid checkpointId)
    {
        var index = _checkpoints.FindIndex(c => c.Id == checkpointId);
        if (index < 0)
        {
            throw new ArgumentException("Unknown checkpoint.", nameof(checkpointId));
        }
        var checkpoint = _checkpoints[index];
        return checkpoint.Balance - BalanceOn(checkpoint.Date, _checkpoints.Take(index).ToList());
    }

    /// <summary>Current balance plus the expected inputs not yet received (FR-10).</summary>
    public Money ExpectedBalance(DateOnly today, IEnumerable<Money> expectedAmounts)
    {
        ArgumentNullException.ThrowIfNull(expectedAmounts);
        return BalanceOn(today) + Money.Sum(expectedAmounts);
    }

    private Money BalanceOn(DateOnly date, IReadOnlyList<BalanceCheckpoint> checkpoints)
    {
        var latest = checkpoints.LastOrDefault(c => c.Date <= date);
        var start = latest?.Balance ?? Money.Zero;
        var after = latest?.Date ?? DateOnly.MinValue;
        var received = _received.Where(r => r.ReceivedDate <= date && (latest is null || r.ReceivedDate > after));
        return start + Money.Sum(received.Select(r => r.Amount));
    }
}
