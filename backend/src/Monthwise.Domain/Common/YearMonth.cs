using System.Globalization;

namespace Monthwise.Domain.Common;

/// <summary>
/// A calendar month (e.g. 2026-10). Identifies a budget period and the first period of an
/// installment plan.
/// </summary>
public readonly record struct YearMonth : IComparable<YearMonth>
{
    public YearMonth(int year, int month)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(year, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(year, 9999);
        ArgumentOutOfRangeException.ThrowIfLessThan(month, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(month, 12);
        Year = year;
        Month = month;
    }

    public int Year { get; }
    public int Month { get; }

    public static YearMonth FromDate(DateOnly date) => new(date.Year, date.Month);

    public DateOnly FirstDay => new(Year, Month, 1);
    public DateOnly LastDay => new(Year, Month, DateTime.DaysInMonth(Year, Month));

    public YearMonth AddMonths(int months)
    {
        var index = checked(Year * 12 + (Month - 1) + months);
        return new YearMonth(index / 12, index % 12 + 1);
    }

    public YearMonth Next() => AddMonths(1);
    public YearMonth Previous() => AddMonths(-1);

    /// <summary>Number of months from this month to <paramref name="other"/>; negative if other is earlier.</summary>
    public int MonthsUntil(YearMonth other) => (other.Year - Year) * 12 + (other.Month - Month);

    public int CompareTo(YearMonth other) => MonthsUntil(other) switch
    {
        > 0 => -1,
        < 0 => 1,
        _ => 0,
    };

    public static bool operator <(YearMonth left, YearMonth right) => left.CompareTo(right) < 0;
    public static bool operator >(YearMonth left, YearMonth right) => left.CompareTo(right) > 0;
    public static bool operator <=(YearMonth left, YearMonth right) => left.CompareTo(right) <= 0;
    public static bool operator >=(YearMonth left, YearMonth right) => left.CompareTo(right) >= 0;

    /// <summary>Parses the ISO form <c>yyyy-MM</c>.</summary>
    public static YearMonth Parse(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!TryParse(value, out var result))
        {
            throw new FormatException($"'{value}' is not a valid year-month (expected yyyy-MM).");
        }
        return result;
    }

    public static bool TryParse(string? value, out YearMonth result)
    {
        result = default;
        if (value is null || value.Length != 7 || value[4] != '-')
        {
            return false;
        }
        if (!int.TryParse(value.AsSpan(0, 4), NumberStyles.None, CultureInfo.InvariantCulture, out var year)
            || !int.TryParse(value.AsSpan(5, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var month)
            || year < 1 || month is < 1 or > 12)
        {
            return false;
        }
        result = new YearMonth(year, month);
        return true;
    }

    public override string ToString() => $"{Year:D4}-{Month:D2}";
}
