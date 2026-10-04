using Monthwise.Domain.Common;

namespace Monthwise.Domain.Cards;

/// <summary>
/// An entry of a list the user can extend: card issuers (Visa, Mastercard, …) and banks
/// (ZaBa, PBZ, …). Predefined entries are seeded; the user adds more from the card form.
/// </summary>
public abstract class CardListEntry
{
    public const int NameMaxLength = 50;

    protected CardListEntry()
    {
        Name = string.Empty;
    }

    protected CardListEntry(Guid id, string name, bool isPredefined)
    {
        Id = id;
        Name = name;
        IsPredefined = isPredefined;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public bool IsPredefined { get; private set; }

    protected static Result<string> ValidateName(string? name)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return Result.Failure<string>(new DomainError("name.required", "Name is required.", "name"));
        }
        if (trimmed.Length > NameMaxLength)
        {
            return Result.Failure<string>(new DomainError(
                "name.tooLong", $"Name can be at most {NameMaxLength} characters.", "name"));
        }
        return Result.Success(trimmed);
    }
}

/// <summary>A card network or scheme, e.g. Visa or Mastercard.</summary>
public sealed class CardIssuer : CardListEntry
{
    private CardIssuer()
    {
    }

    private CardIssuer(Guid id, string name, bool isPredefined) : base(id, name, isPredefined)
    {
    }

    public static Result<CardIssuer> Create(string? name)
    {
        var validName = ValidateName(name);
        return validName.IsSuccess
            ? Result.Success(new CardIssuer(Guid.CreateVersion7(), validName.Value, isPredefined: false))
            : Result.Failure<CardIssuer>(validName.Errors);
    }

    /// <summary>For seed data only.</summary>
    public static CardIssuer Predefined(Guid id, string name) => new(id, name, isPredefined: true);
}

/// <summary>The bank or institution that holds the account, e.g. ZaBa or PBZ.</summary>
public sealed class Bank : CardListEntry
{
    private Bank()
    {
    }

    private Bank(Guid id, string name, bool isPredefined) : base(id, name, isPredefined)
    {
    }

    public static Result<Bank> Create(string? name)
    {
        var validName = ValidateName(name);
        return validName.IsSuccess
            ? Result.Success(new Bank(Guid.CreateVersion7(), validName.Value, isPredefined: false))
            : Result.Failure<Bank>(validName.Errors);
    }

    /// <summary>For seed data only.</summary>
    public static Bank Predefined(Guid id, string name) => new(id, name, isPredefined: true);
}
