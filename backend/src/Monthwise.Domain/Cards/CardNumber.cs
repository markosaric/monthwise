namespace Monthwise.Domain.Cards;

/// <summary>
/// A full payment card number (13 to 19 digits, Luhn check). Sensitive: <see cref="ToString"/>
/// returns the masked form, so a log line or exception message never shows the full number.
/// </summary>
public sealed class CardNumber : IEquatable<CardNumber>
{
    public const int MinLength = 13;
    public const int MaxLength = 19;

    private CardNumber(string digits) => Digits = digits;

    /// <summary>The digits only, without spaces.</summary>
    public string Digits { get; }

    public string Last4 => Digits[^4..];

    public string Masked => $"•••• {Last4}";

    /// <summary>Accepts digits with optional spaces or dashes between them.</summary>
    public static bool TryParse(string? input, out CardNumber? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var digits = new string(input.Where(c => c is not (' ' or '-')).ToArray());
        if (digits.Length is < MinLength or > MaxLength || !digits.All(char.IsAsciiDigit) || !PassesLuhn(digits))
        {
            return false;
        }

        result = new CardNumber(digits);
        return true;
    }

    public static CardNumber Parse(string input) =>
        TryParse(input, out var result) ? result! : throw new FormatException("Card number is not valid.");

    private static bool PassesLuhn(string digits)
    {
        var sum = 0;
        var doubleIt = false;
        for (var i = digits.Length - 1; i >= 0; i--)
        {
            var digit = digits[i] - '0';
            if (doubleIt)
            {
                digit *= 2;
                if (digit > 9)
                {
                    digit -= 9;
                }
            }
            sum += digit;
            doubleIt = !doubleIt;
        }
        return sum % 10 == 0;
    }

    public bool Equals(CardNumber? other) => other is not null && Digits == other.Digits;
    public override bool Equals(object? obj) => Equals(obj as CardNumber);
    public override int GetHashCode() => Digits.GetHashCode(StringComparison.Ordinal);

    public override string ToString() => Masked;
}
