using System.Reflection;

namespace Monthwise.Application.DataTransfer;

/// <summary>The running release (all projects share the version), written into exports and the history.</summary>
public static class ReleaseVersion
{
    public static string Current { get; } =
        typeof(ReleaseVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "unknown";
}
