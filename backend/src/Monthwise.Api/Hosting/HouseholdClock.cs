using Monthwise.Application.Common;

namespace Monthwise.Api.Hosting;

/// <summary>
/// The application's clock: the system clock in the household time zone from the setting
/// <c>Household:TimeZone</c> (production packaging, Appendix 6).
/// </summary>
internal static class HouseholdClock
{
    public static IServiceCollection AddHouseholdClock(this IServiceCollection services) =>
        services.AddSingleton<TimeProvider>(provider => new HouseholdTimeProvider(
            TimeProvider.System,
            HouseholdTimeZone.Resolve(provider.GetRequiredService<IConfiguration>()[HouseholdTimeZone.SettingName])));

    /// <summary>Stops the start, naming the setting, when the household time zone is unknown.</summary>
    public static void EnsureHouseholdTimeZone(this IServiceProvider services) =>
        services.GetRequiredService<TimeProvider>();
}
