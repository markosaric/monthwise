using Monthwise.Domain.Common;

namespace Monthwise.Domain.CashFlow;

/// <summary>A balance checkpoint for the calculation: the balance at the end of <paramref name="Date"/>.</summary>
public sealed record BalanceCheckpoint(Guid Id, DateOnly Date, Money Balance, DateTimeOffset CreatedAt);

/// <summary>
/// Money that came in or went out of an account on <paramref name="Date"/>: a received input is
/// positive, a paid loan payment negative.
/// </summary>
public readonly record struct BalanceMovement(DateOnly Date, Money Amount);

/// <summary>
/// The balance of one account (input FR-10, Appendix 1). The balance on a date is the latest
/// checkpoint on or before it, plus the movements (received inputs minus paid outputs) after that
/// checkpoint's date and on or before the date. Without a checkpoint it starts from zero. Checkpoints
/// on the same date are ordered by when they were entered.
/// </summary>
public sealed class AccountBalance
{
    private readonly List<BalanceCheckpoint> _checkpoints;
    private readonly List<BalanceMovement> _movements;

    public AccountBalance(IEnumerable<BalanceCheckpoint> checkpoints, IEnumerable<BalanceMovement> movements)
    {
        ArgumentNullException.ThrowIfNull(checkpoints);
        ArgumentNullException.ThrowIfNull(movements);
        _checkpoints = checkpoints.OrderBy(c => c.Date).ThenBy(c => c.CreatedAt).ToList();
        _movements = movements.ToList();
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

    /// <summary>
    /// Current balance plus the expected movements not yet made (input FR-10, Appendix 1): expected
    /// inputs are positive and expected loan payments negative.
    /// </summary>
    public Money ExpectedBalance(DateOnly today, IEnumerable<Money> expectedMovements)
    {
        ArgumentNullException.ThrowIfNull(expectedMovements);
        return BalanceOn(today) + Money.Sum(expectedMovements);
    }

    private Money BalanceOn(DateOnly date, IReadOnlyList<BalanceCheckpoint> checkpoints)
    {
        var latest = checkpoints.LastOrDefault(c => c.Date <= date);
        var start = latest?.Balance ?? Money.Zero;
        var after = latest?.Date ?? DateOnly.MinValue;
        var movements = _movements.Where(m => m.Date <= date && (latest is null || m.Date > after));
        return start + Money.Sum(movements.Select(m => m.Amount));
    }
}
