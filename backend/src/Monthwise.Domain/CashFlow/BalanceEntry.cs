using Monthwise.Domain.Common;

namespace Monthwise.Domain.CashFlow;

/// <summary>Starting balance or correction (FR-9).</summary>
public enum BalanceEntryKind
{
    Starting,
    Correction,
}

/// <summary>
/// A balance checkpoint of an account: "at the end of <see cref="Date"/> the account had
/// <see cref="Balance"/>". A starting balance is the first one; a correction is one entered later
/// because the app's balance did not match the bank. Neither is a payment (FR-9).
/// </summary>
public sealed class BalanceEntry
{
    public const int NoteMaxLength = 200;

    private BalanceEntry()
    {
    }

    public Guid Id { get; private set; }
    public Guid AccountId { get; private set; }
    public BalanceEntryKind Kind { get; private set; }
    public DateOnly Date { get; private set; }
    public Money Balance { get; private set; }
    public string? Note { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static Result<BalanceEntry> Create(
        Guid accountId, BalanceEntryKind kind, DateOnly date, Money balance, string? note, DateTimeOffset now)
    {
        var entry = new BalanceEntry { Id = Guid.CreateVersion7(now), AccountId = accountId, Kind = kind, CreatedAt = now };
        var applied = entry.Apply(date, balance, note, now);
        return applied.IsSuccess ? Result.Success(entry) : Result.Failure<BalanceEntry>(applied.Errors);
    }

    public Result Update(DateOnly date, Money balance, string? note, DateTimeOffset now) => Apply(date, balance, note, now);

    private Result Apply(DateOnly date, Money balance, string? note, DateTimeOffset now)
    {
        var normalized = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (normalized is { Length: > NoteMaxLength })
        {
            return Result.Failure(CashFlowErrors.BalanceNoteTooLong);
        }
        if (!Enum.IsDefined(Kind))
        {
            return Result.Failure(CashFlowErrors.KindInvalid);
        }
        Date = date;
        Balance = balance;
        Note = normalized;
        UpdatedAt = now;
        return Result.Success();
    }
}
