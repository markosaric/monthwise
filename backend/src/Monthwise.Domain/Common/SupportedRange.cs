namespace Monthwise.Domain.Common;

/// <summary>
/// The months and dates the app works with: January 2000 to December 2100
/// (docs/feature-requirements/month-range.md). Months asked for and dates saved outside it are
/// refused; data stored earlier outside it keeps loading. Widen the range here only.
/// </summary>
public static class SupportedRange
{
    public static readonly YearMonth FirstMonth = new(2000, 1);
    public static readonly YearMonth LastMonth = new(2100, 12);

    public static DateOnly FirstDate => FirstMonth.FirstDay;
    public static DateOnly LastDate => LastMonth.LastDay;

    public static string MonthMessage => $"Enter a month from {FirstMonth} to {LastMonth}.";
    public static string DateMessage => $"Enter a date from {FirstDate:yyyy-MM-dd} to {LastDate:yyyy-MM-dd}.";

    public static bool Contains(YearMonth month) => month >= FirstMonth && month <= LastMonth;

    public static bool Contains(DateOnly date) => date >= FirstDate && date <= LastDate;
}
