using Microsoft.AspNetCore.HttpOverrides;

namespace Monthwise.Api.Hosting;

/// <summary>
/// Behind a TLS-terminating proxy (Azure Container Apps), trust its X-Forwarded-Proto and -For headers
/// so the app sees HTTPS. Turned on with <c>Hosting:BehindProxy</c>; the app is then reachable only
/// through that proxy.
/// </summary>
internal static class ProxyHosting
{
    public static IServiceCollection AddProxyHosting(this IServiceCollection services, IConfiguration configuration)
    {
        if (!configuration.GetValue<bool>("Hosting:BehindProxy"))
        {
            return services;
        }

        return services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            // The proxy's addresses are not fixed; the container accepts traffic only from it.
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        });
    }

    public static WebApplication UseProxyHosting(this WebApplication app)
    {
        if (app.Configuration.GetValue<bool>("Hosting:BehindProxy"))
        {
            app.UseForwardedHeaders();
        }

        return app;
    }
}
