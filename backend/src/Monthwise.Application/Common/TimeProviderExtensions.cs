namespace Monthwise.Application.Common;

public static class TimeProviderExtensions
{
    /// <summary>
    /// Today in the clock's local time zone, which is the household's time zone
    /// (<see cref="HouseholdTimeProvider"/>), not the server's.
    /// </summary>
    public static DateOnly Today(this TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        return DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
    }
}
