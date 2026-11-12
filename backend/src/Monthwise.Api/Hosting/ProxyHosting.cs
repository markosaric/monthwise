using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using IPNetwork = System.Net.IPNetwork;

namespace Monthwise.Api.Hosting;

/// <summary>
/// Behind a TLS-terminating proxy (Azure Container Apps), trust its X-Forwarded-Proto and -For headers
/// so the app sees HTTPS. Turned on with <c>Hosting:BehindProxy</c>. The headers are accepted only
/// from the networks in <c>Hosting:TrustedProxyNetworks</c> (a list of CIDR ranges); without that
/// setting, from the private address ranges hosting platforms use for their internal proxy
/// (error mapping and proxy trust FR-6). A request from any other address is treated as direct.
/// </summary>
internal static partial class ProxyHosting
{
    public const string TrustedNetworksSetting = "Hosting:TrustedProxyNetworks";

    /// <summary>General private ranges: RFC 1918, shared address space, loopback and IPv6 unique local.</summary>
    public static readonly IReadOnlyList<string> DefaultTrustedNetworks =
    [
        "10.0.0.0/8",
        "172.16.0.0/12",
        "192.168.0.0/16",
        "100.64.0.0/10",
        "127.0.0.0/8",
        "::1/128",
        "fc00::/7",
    ];

    public static IServiceCollection AddProxyHosting(this IServiceCollection services, IConfiguration configuration)
    {
        if (!configuration.GetValue<bool>("Hosting:BehindProxy"))
        {
            return services;
        }

        var networks = TrustedNetworks(configuration);
        return services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();
            foreach (var network in networks)
            {
                options.KnownIPNetworks.Add(network);
            }
        });
    }

    public static WebApplication UseProxyHosting(this WebApplication app)
    {
        if (app.Configuration.GetValue<bool>("Hosting:BehindProxy"))
        {
            var configured = app.Configuration.GetSection(TrustedNetworksSetting).Get<string[]>() is { Length: > 0 };
            var count = app.Services.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value.KnownIPNetworks.Count;
            var source = configured ? TrustedNetworksSetting : "default private ranges";
            LogTrustedNetworks(app.Logger, count, source);
            app.UseForwardedHeaders();
        }

        return app;
    }

    /// <summary>The configured networks, or the default ones when none are set. An invalid entry stops startup.</summary>
    internal static IReadOnlyList<IPNetwork> TrustedNetworks(IConfiguration configuration)
    {
        var configured = configuration.GetSection(TrustedNetworksSetting).Get<string[]>()?
            .Where(entry => !string.IsNullOrWhiteSpace(entry))
            .ToArray();
        var entries = configured is { Length: > 0 } ? configured : DefaultTrustedNetworks;

        return entries.Select(entry => IPNetwork.TryParse(entry.Trim(), out var network)
                ? network
                : throw new InvalidOperationException(
                    $"{TrustedNetworksSetting} holds an entry that is not an address range in CIDR notation (for example 10.0.0.0/8)."))
            .ToArray();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Behind a proxy: forwarded headers are accepted from {Count} trusted networks ({Source})")]
    private static partial void LogTrustedNetworks(ILogger logger, int count, string source);
}
