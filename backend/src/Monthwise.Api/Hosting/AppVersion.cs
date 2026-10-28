using System.Reflection;

namespace Monthwise.Api.Hosting;

/// <summary>Version of the running release: the tag version plus the commit, e.g. <c>1.2.0+3f2c1ab</c>.</summary>
internal static class AppVersion
{
    public static string Current { get; } =
        typeof(AppVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "unknown";
}
