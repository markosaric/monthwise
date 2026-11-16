using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Monthwise.Api.Hosting;
using Monthwise.Application.DataTransfer;

namespace Monthwise.Api.Tests;

/// <summary>Every answer tells the browser how to protect the app (security headers FR-1 to FR-4, A1, A2, A5).</summary>
public sealed class SecurityHeadersTests : IDisposable
{
    private const string UnreachableDatabase =
        "Host=127.0.0.1;Port=1;Database=monthwise;Username=monthwise;Timeout=1";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly string _webRoot = Directory.CreateTempSubdirectory("monthwise-wwwroot-").FullName;

    public SecurityHeadersTests()
    {
        File.WriteAllText(Path.Combine(_webRoot, "index.html"), "<!doctype html><title>Monthwise</title>");
        Directory.CreateDirectory(Path.Combine(_webRoot, "assets"));
        File.WriteAllText(Path.Combine(_webRoot, "assets", "index-abc123.js"), "console.log('monthwise')");
    }

    public void Dispose() => Directory.Delete(_webRoot, recursive: true);

    [Theory]
    [InlineData("/month", HttpStatusCode.OK)]
    [InlineData("/assets/index-abc123.js", HttpStatusCode.OK)]
    [InlineData("/api/health", HttpStatusCode.ServiceUnavailable)]
    [InlineData("/api/does-not-exist", HttpStatusCode.NotFound)]
    [InlineData("/assets/missing.js", HttpStatusCode.NotFound)]
    public async Task Screens_files_and_api_answers_carry_the_protections(string path, HttpStatusCode status)
    {
        await using var factory = new MonthwiseApiFactory(UnreachableDatabase, migrateOnStartup: false, _webRoot);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri(path, UriKind.Relative), Ct);

        Assert.Equal(status, response.StatusCode);
        AssertProtected(response);
    }

    [Fact]
    public async Task An_answer_rebuilt_by_the_exception_handler_carries_the_protections()
    {
        await using var factory = new MonthwiseApiFactory(UnreachableDatabase, migrateOnStartup: false, _webRoot);
        using var client = factory.CreateClient();

        // The database cannot be reached, so reading the cards fails inside the endpoint.
        var response = await client.GetAsync(new Uri("/api/cards", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        AssertProtected(response);
    }

    [Fact]
    public async Task An_answer_refused_by_the_import_gate_carries_the_protections()
    {
        await using var factory = new MonthwiseApiFactory(UnreachableDatabase, migrateOnStartup: false, _webRoot);
        using var client = factory.CreateClient();
        var gate = factory.Services.GetRequiredService<ImportGate>();
        Assert.True(gate.TryEnter());
        try
        {
            var response = await client.GetAsync(new Uri("/api/cards", UriKind.Relative), Ct);

            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            AssertProtected(response);
        }
        finally
        {
            gate.Exit();
        }
    }

    [Fact]
    public async Task An_answer_refused_by_the_migration_gate_carries_the_protections()
    {
        await using var factory = new MonthwiseApiFactory(UnreachableDatabase, migrateOnStartup: true, _webRoot);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/api/cards", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        AssertProtected(response);
    }

    [Fact]
    public async Task Over_https_the_browser_is_told_to_keep_using_https_for_a_year()
    {
        await using var factory = new MonthwiseApiFactory(UnreachableDatabase, migrateOnStartup: false, _webRoot);
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });

        var response = await client.GetAsync(new Uri("/month", UriKind.Relative), Ct);

        Assert.True(MaxAge(response) >= TimeSpan.FromDays(365), "HSTS must last at least one year.");
    }

    [Fact]
    public async Task Over_plain_http_https_is_not_enforced()
    {
        await using var factory = new MonthwiseApiFactory(UnreachableDatabase, migrateOnStartup: false, _webRoot);
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("http://localhost") });

        var response = await client.GetAsync(new Uri("/month", UriKind.Relative), Ct);

        Assert.False(response.Headers.Contains("Strict-Transport-Security"));
    }

    [Theory]
    [InlineData("10.1.2.3", true)]
    [InlineData("203.0.113.9", false)]
    public async Task Behind_the_proxy_https_is_enforced_only_for_forwarded_https_from_a_trusted_network(
        string sender, bool enforced)
    {
        await using var api = new MonthwiseApiFactory(UnreachableDatabase, migrateOnStartup: false, _webRoot);
        await using var factory = api.WithWebHostBuilder(builder => builder.UseSetting("Hosting:BehindProxy", "true"));

        var context = await factory.Server.SendAsync(context =>
        {
            context.Request.Method = HttpMethods.Get;
            context.Request.Path = "/month";
            context.Connection.RemoteIpAddress = IPAddress.Parse(sender);
            context.Request.Headers["X-Forwarded-Proto"] = "https";
            context.Request.Headers["X-Forwarded-For"] = "198.51.100.7";
        }, Ct);

        Assert.Equal(enforced, context.Response.Headers.StrictTransportSecurity.Count > 0);
        Assert.Equal(SecurityHeaders.ContentSecurityPolicy, context.Response.Headers.ContentSecurityPolicy.ToString());
    }

    [Fact]
    public void Only_the_apps_own_script_files_may_run()
    {
        var scriptSource = SecurityHeaders.ContentSecurityPolicy.Split(';')
            .Select(directive => directive.Trim())
            .Single(directive => directive.StartsWith("script-src ", StringComparison.Ordinal));

        Assert.Equal("script-src 'self'", scriptSource);
    }

    [Theory]
    [InlineData("/scalar")]
    [InlineData("/openapi/v1.json")]
    public async Task In_development_the_api_reference_has_no_content_policy_but_keeps_the_other_protections(string path)
    {
        await using var api = new MonthwiseApiFactory(UnreachableDatabase, migrateOnStartup: false);
        await using var factory = api.WithWebHostBuilder(builder => builder.UseEnvironment("Development"));
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri(path, UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("Content-Security-Policy"));
        Assert.Equal("nosniff", Single(response, "X-Content-Type-Options"));
        Assert.Equal("DENY", Single(response, "X-Frame-Options"));
    }

    private static void AssertProtected(HttpResponseMessage response)
    {
        Assert.Equal(SecurityHeaders.ContentSecurityPolicy, Single(response, "Content-Security-Policy"));
        Assert.Equal("DENY", Single(response, "X-Frame-Options"));
        Assert.Equal("nosniff", Single(response, "X-Content-Type-Options"));
        Assert.Equal("no-referrer", Single(response, "Referrer-Policy"));
        Assert.Equal(SecurityHeaders.PermissionsPolicy, Single(response, "Permissions-Policy"));
    }

    private static string Single(HttpResponseMessage response, string name) =>
        Assert.Single(response.Headers.TryGetValues(name, out var values) ? values : []);

    private static TimeSpan? MaxAge(HttpResponseMessage response)
    {
        var value = Single(response, "Strict-Transport-Security");
        var maxAge = value.Split(';').Select(part => part.Trim())
            .Single(part => part.StartsWith("max-age=", StringComparison.Ordinal));
        return TimeSpan.FromSeconds(long.Parse(maxAge["max-age=".Length..], System.Globalization.CultureInfo.InvariantCulture));
    }
}
