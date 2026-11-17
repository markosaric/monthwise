namespace Monthwise.Application.Common;

/// <summary>
/// A clock whose local time zone is the household's (production packaging, Appendix 6), so
/// <see cref="TimeProvider.GetLocalNow"/> and <see cref="TimeProviderExtensions.Today"/> give the
/// household's date and time whatever time zone the server runs in. The current moment, timestamps
/// and timers come unchanged from the wrapped clock.
/// </summary>
public sealed class HouseholdTimeProvider(TimeProvider clock, TimeZoneInfo householdTimeZone) : TimeProvider
{
    private readonly TimeProvider _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    private readonly TimeZoneInfo _householdTimeZone = householdTimeZone ?? throw new ArgumentNullException(nameof(householdTimeZone));

    public override TimeZoneInfo LocalTimeZone => _householdTimeZone;

    public override DateTimeOffset GetUtcNow() => _clock.GetUtcNow();

    public override long GetTimestamp() => _clock.GetTimestamp();

    public override long TimestampFrequency => _clock.TimestampFrequency;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
        _clock.CreateTimer(callback, state, dueTime, period);
}

/// <summary>The household time zone setting (production packaging, Appendix 6).</summary>
public static class HouseholdTimeZone
{
    /// <summary>The setting's name, as given in settings.json or as <c>Household__TimeZone</c>.</summary>
    public const string SettingName = "Household:TimeZone";

    /// <summary>The time zone used when the setting is not given.</summary>
    public const string DefaultId = "Europe/Zagreb";

    /// <summary>
    /// The time zone with the given IANA id (for example <c>Europe/Zagreb</c>), or the default when
    /// none is given. Throws, naming the setting, when the id is not a known time zone.
    /// </summary>
    public static TimeZoneInfo Resolve(string? id)
    {
        var name = string.IsNullOrWhiteSpace(id) ? DefaultId : id.Trim();
        if (TimeZoneInfo.TryFindSystemTimeZoneById(name, out var zone))
        {
            return zone;
        }

        throw new InvalidOperationException(
            $"The setting {SettingName} has the unknown time zone '{name}'. Use an IANA time zone such as {DefaultId}.");
    }
}
