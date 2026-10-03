namespace Monthwise.Domain.Common;

/// <summary>
/// An amount of money in EUR, stored as whole cents. Floating point is never used for amounts;
/// formatting for display belongs to the UI.
/// </summary>
public readonly record struct Money(long Cents) : IComparable<Money>
{
    public static readonly Money Zero = new(0);

    public static Money FromCents(long cents) => new(cents);

    public bool IsZero => Cents == 0;
    public bool IsNegative => Cents < 0;
    public bool IsPositive => Cents > 0;

    public Money Abs() => new(Math.Abs(Cents));

    public int CompareTo(Money other) => Cents.CompareTo(other.Cents);

    public static Money operator +(Money left, Money right) => new(checked(left.Cents + right.Cents));
    public static Money operator -(Money left, Money right) => new(checked(left.Cents - right.Cents));
    public static Money operator -(Money value) => new(checked(-value.Cents));
    public static Money operator *(Money value, int factor) => new(checked(value.Cents * factor));
    public static Money operator *(int factor, Money value) => value * factor;

    public static bool operator <(Money left, Money right) => left.Cents < right.Cents;
    public static bool operator >(Money left, Money right) => left.Cents > right.Cents;
    public static bool operator <=(Money left, Money right) => left.Cents <= right.Cents;
    public static bool operator >=(Money left, Money right) => left.Cents >= right.Cents;

    public static Money Sum(IEnumerable<Money> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var total = Zero;
        foreach (var value in values)
        {
            total += value;
        }
        return total;
    }

    public override string ToString() => $"{Cents} cents";
}
