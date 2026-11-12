using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Monthwise.Api.Hosting;

namespace Monthwise.Api.Tests;

/// <summary>Forwarded headers are accepted only from trusted proxy networks (proxy trust FR-6, A6 to A8).</summary>
public sealed class ProxyHostingTests
{
    private const string UnreachableDatabase =
        "Host=127.0.0.1;Port=1;Database=monthwise;Username=monthwise;Timeout=1";

    private static readonly IPAddress Client = IPAddress.Parse("198.51.100.7");

    [Theory]
    [InlineData("10.1.2.3")]
    [InlineData("::ffff:10.1.2.3")]
    [InlineData("172.20.0.5")]
    [InlineData("100.100.0.9")]
    public async Task A_request_from_a_default_trusted_network_is_seen_as_the_forwarded_https_client(string proxy)
    {
        var context = await SendAsync(behindProxy: true, IPAddress.Parse(proxy));

        Assert.Equal("https", context.Request.Scheme);
        Assert.Equal(Client, context.Connection.RemoteIpAddress);
    }

    [Fact]
    public async Task A_request_from_any_other_address_keeps_its_own_scheme_and_sender()
    {
        var sender = IPAddress.Parse("203.0.113.10");

        var context = await SendAsync(behindProxy: true, sender);

        Assert.Equal("http", context.Request.Scheme);
        Assert.Equal(sender, context.Connection.RemoteIpAddress);
    }

    [Fact]
    public async Task Configured_networks_replace_the_default()
    {
        var configured = IPAddress.Parse("203.0.113.10");

        var trusted = await SendAsync(behindProxy: true, configured, "203.0.113.0/24");
        var formerDefault = await SendAsync(behindProxy: true, IPAddress.Parse("10.1.2.3"), "203.0.113.0/24");

        Assert.Equal("https", trusted.Request.Scheme);
        Assert.Equal("http", formerDefault.Request.Scheme);
    }

    [Fact]
    public async Task Without_proxy_mode_forwarded_headers_are_ignored()
    {
        var proxy = IPAddress.Parse("10.1.2.3");

        var context = await SendAsync(behindProxy: false, proxy);

        Assert.Equal("http", context.Request.Scheme);
        Assert.Equal(proxy, context.Connection.RemoteIpAddress);
    }

    [Fact]
    public void An_entry_that_is_not_an_address_range_stops_startup()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [$"{ProxyHosting.TrustedNetworksSetting}:0"] = "not-a-network" })
            .Build();

        var error = Assert.Throws<InvalidOperationException>(() => ProxyHosting.TrustedNetworks(configuration));

        Assert.Contains(ProxyHosting.TrustedNetworksSetting, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Without_the_setting_the_default_private_ranges_are_trusted()
    {
        var networks = ProxyHosting.TrustedNetworks(new ConfigurationBuilder().Build());

        Assert.Equal(ProxyHosting.DefaultTrustedNetworks, networks.Select(n => n.ToString()));
    }

    private static async Task<HttpContext> SendAsync(bool behindProxy, IPAddress sender, string? trustedNetwork = null)
    {
        await using var api = new MonthwiseApiFactory(UnreachableDatabase, migrateOnStartup: false);
        await using var factory = api.WithWebHostBuilder(builder =>
            {
                builder.UseSetting("Hosting:BehindProxy", behindProxy.ToString());
                if (trustedNetwork is not null)
                {
                    builder.UseSetting($"{ProxyHosting.TrustedNetworksSetting}:0", trustedNetwork);
                }
            });

        return await factory.Server.SendAsync(context =>
        {
            context.Request.Method = HttpMethods.Get;
            context.Request.Path = "/api/does-not-exist";
            context.Connection.RemoteIpAddress = sender;
            context.Request.Headers["X-Forwarded-Proto"] = "https";
            context.Request.Headers["X-Forwarded-For"] = Client.ToString();
        }, TestContext.Current.CancellationToken);
    }
}
