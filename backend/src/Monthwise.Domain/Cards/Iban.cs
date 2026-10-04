using System.Globalization;

namespace Monthwise.Domain.Cards;

/// <summary>
/// An International Bank Account Number (ISO 13616: country code, check digits, mod-97 check).
/// Sensitive: <see cref="ToString"/> returns the masked form.
/// </summary>
public sealed class Iban : IEquatable<Iban>
{
    public const int MinLength = 15;
    public const int MaxLength = 34;

    /// <summary>Lengths of countries Monthwise users are likely to meet; others only get the generic checks.</summary>
    private static readonly Dictionary<string, int> CountryLengths = new(StringComparer.Ordinal)
    {
        ["AT"] = 20,
        ["BA"] = 20,
        ["BE"] = 16,
        ["CH"] = 21,
        ["CZ"] = 24,
        ["DE"] = 22,
        ["DK"] = 18,
        ["ES"] = 24,
        ["FI"] = 18,
        ["FR"] = 27,
        ["GB"] = 22,
        ["HR"] = 21,
        ["HU"] = 28,
        ["IE"] = 22,
        ["IT"] = 27,
        ["LU"] = 20,
        ["ME"] = 22,
        ["NL"] = 18,
        ["NO"] = 15,
        ["PL"] = 28,
        ["PT"] = 25,
        ["RS"] = 22,
        ["SE"] = 24,
        ["SI"] = 19,
        ["SK"] = 24,
    };

    private Iban(string value) => Value = value;

    /// <summary>Upper case, without spaces, e.g. <c>HR1210010051863000160</c>.</summary>
    public string Value { get; }

    public string CountryCode => Value[..2];

    public string Last4 => Value[^4..];

    public string Masked => $"{CountryCode}•• … {Last4}";

    /// <summary>Accepts upper or lower case with optional spaces.</summary>
    public static bool TryParse(string? input, out Iban? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var value = new string(input.Where(c => c != ' ').ToArray()).ToUpperInvariant();
        if (value.Length is < MinLength or > MaxLength
            || !char.IsAsciiLetterUpper(value[0]) || !char.IsAsciiLetterUpper(value[1])
            || !char.IsAsciiDigit(value[2]) || !char.IsAsciiDigit(value[3])
            || !value.All(char.IsAsciiLetterOrDigit))
        {
            return false;
        }

        if (CountryLengths.TryGetValue(value[..2], out var length) && value.Length != length)
        {
            return false;
        }

        if (Mod97(value[4..] + value[..4]) != 1)
        {
            return false;
        }

        result = new Iban(value);
        return true;
    }

    public static Iban Parse(string input) =>
        TryParse(input, out var result) ? result! : throw new FormatException("IBAN is not valid.");

    /// <summary>Remainder of the number formed by replacing letters with 10..35, computed piecewise.</summary>
    private static int Mod97(string rearranged)
    {
        var remainder = 0;
        foreach (var c in rearranged)
        {
            var part = char.IsAsciiDigit(c)
                ? (c - '0').ToString(CultureInfo.InvariantCulture)
                : (c - 'A' + 10).ToString(CultureInfo.InvariantCulture);
            foreach (var digit in part)
            {
                remainder = (remainder * 10 + (digit - '0')) % 97;
            }
        }
        return remainder;
    }

    /// <summary>Groups of four for display, e.g. <c>HR12 1001 0051 8630 0016 0</c>.</summary>
    public string Formatted() =>
        string.Join(' ', Enumerable.Range(0, (Value.Length + 3) / 4)
            .Select(i => Value.Substring(i * 4, Math.Min(4, Value.Length - i * 4))));

    public bool Equals(Iban? other) => other is not null && Value == other.Value;
    public override bool Equals(object? obj) => Equals(obj as Iban);
    public override int GetHashCode() => Value.GetHashCode(StringComparison.Ordinal);

    public override string ToString() => Masked;
}
