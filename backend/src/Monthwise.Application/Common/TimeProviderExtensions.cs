namespace Monthwise.Application.Common;

public static class TimeProviderExtensions
{
    /// <summary>Today in the server's local time zone (the household's time zone).</summary>
    public static DateOnly Today(this TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        return DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
    }
}
