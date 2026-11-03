using Monthwise.Domain.Common;

namespace Monthwise.Domain.Revolving;

/// <summary>What a debt entry is (revolving FR-5, FR-9).</summary>
public enum DebtEntryKind
{
    /// <summary>The debt on a date, entered when the card was added (R8).</summary>
    Starting,

    /// <summary>The debt the bank shows on a date, to make the calculated debt match it.</summary>
    Correction,

    /// <summary>Debt moved here from the card this one replaces.</summary>
    TransferIn,

    /// <summary>Debt moved to the replacement card.</summary>
    TransferOut,
}

/// <summary>
/// A change of a revolving card's debt that is not a charge or a payment (revolving FR-5, FR-9). A
/// <see cref="DebtEntryKind.Starting"/> or <see cref="DebtEntryKind.Correction"/> entry sets the debt at
/// the end of its date to <see cref="Amount"/>; a transfer adds or removes <see cref="Amount"/>.
/// </summary>
public sealed class DebtEntry
{
    public const int NoteMaxLength = 200;

    private DebtEntry()
    {
    }

    public Guid Id { get; private set; }
    public Guid CardId { get; private set; }
    public DebtEntryKind Kind { get; private set; }
    public DateOnly Date { get; private set; }
    public Money Amount { get; private set; }
    public string? Note { get; private set; }

    /// <summary>For a transfer: the card the debt came from or went to.</summary>
    public Guid? LinkedCardId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public bool IsTransfer => Kind is DebtEntryKind.TransferIn or DebtEntryKind.TransferOut;

    /// <summary>A starting debt or a correction: the debt on <paramref name="date"/>.</summary>
    public static Result<DebtEntry> Create(Guid cardId, DebtEntryKind kind, DateOnly date, Money debt, string? note, DateTimeOffset now)
    {
        if (kind is not (DebtEntryKind.Starting or DebtEntryKind.Correction))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), "Transfers are created with CreateTransfer.");
        }
        var errors = Validate(debt, note, out var normalized);
        if (errors.Count > 0)
        {
            return Result.Failure<DebtEntry>(errors);
        }
        return Result.Success(new DebtEntry
        {
            Id = Guid.CreateVersion7(now),
            CardId = cardId,
            Kind = kind,
            Date = date,
            Amount = debt,
            Note = normalized,
            CreatedAt = now,
            UpdatedAt = now,
        });
    }

    /// <summary>The two entries that move <paramref name="debt"/> from one card to its replacement on <paramref name="date"/>.</summary>
    public static (DebtEntry Out, DebtEntry In) CreateTransfer(Guid fromCardId, Guid toCardId, DateOnly date, Money debt, DateTimeOffset now)
    {
        if (debt.IsNegative)
        {
            throw new ArgumentOutOfRangeException(nameof(debt), "A transferred debt cannot be negative.");
        }
        DebtEntry Entry(Guid cardId, DebtEntryKind kind, Guid linked) => new()
        {
            Id = Guid.CreateVersion7(now),
            CardId = cardId,
            Kind = kind,
            Date = date,
            Amount = debt,
            LinkedCardId = linked,
            CreatedAt = now,
            UpdatedAt = now,
        };
        return (Entry(fromCardId, DebtEntryKind.TransferOut, toCardId), Entry(toCardId, DebtEntryKind.TransferIn, fromCardId));
    }

    /// <summary>Changes a starting debt or a correction. Transfers cannot be changed.</summary>
    public Result Update(DateOnly date, Money debt, string? note, DateTimeOffset now)
    {
        if (IsTransfer)
        {
            return Result.Failure(RevolvingErrors.TransferReadOnly);
        }
        var errors = Validate(debt, note, out var normalized);
        if (errors.Count > 0)
        {
            return Result.Failure(errors);
        }
        Date = date;
        Amount = debt;
        Note = normalized;
        UpdatedAt = now;
        return Result.Success();
    }

    private static List<DomainError> Validate(Money debt, string? note, out string? normalized)
    {
        var errors = new List<DomainError>();
        if (debt.IsNegative)
        {
            errors.Add(RevolvingErrors.DebtNegative);
        }
        normalized = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (normalized is { Length: > NoteMaxLength })
        {
            errors.Add(RevolvingErrors.NoteTooLong);
        }
        return errors;
    }
}
