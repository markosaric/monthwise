using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Monthwise.Api.Tests;

/// <summary>The API serving the built front-end on the same origin, as in a release (FR-1).</summary>
public sealed class ReleaseHostingTests : IDisposable
{
    private const string UnreachableDatabase =
        "Host=127.0.0.1;Port=1;Database=monthwise;Username=monthwise;Timeout=1";

    private readonly string _webRoot = Directory.CreateTempSubdirectory("monthwise-wwwroot-").FullName;

    public ReleaseHostingTests()
    {
        File.WriteAllText(Path.Combine(_webRoot, "index.html"), "<!doctype html><title>Monthwise</title>");
        Directory.CreateDirectory(Path.Combine(_webRoot, "assets"));
        File.WriteAllText(Path.Combine(_webRoot, "assets", "index-abc123.js"), "console.log('monthwise')");
    }

    public void Dispose() => Directory.Delete(_webRoot, recursive: true);

    [Theory]
    [InlineData("/")]
    [InlineData("/month")]
    [InlineData("/settings/cards")]
    public async Task Screen_addresses_answer_the_front_end(string path)
    {
        await using var factory = new MonthwiseApiFactory(UnreachableDatabase, migrateOnStartup: false, _webRoot);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("<title>Monthwise</title>", body, StringComparison.Ordinal);
        Assert.True(response.Headers.CacheControl?.NoCache, "index.html must be revalidated.");
    }

    [Fact]
    public async Task Hashed_assets_are_cached_for_a_year()
    {
        await using var factory = new MonthwiseApiFactory(UnreachableDatabase, migrateOnStartup: false, _webRoot);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/assets/index-abc123.js", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(TimeSpan.FromDays(365), response.Headers.CacheControl?.MaxAge);
    }

    [Theory]
    [InlineData("/api/does-not-exist")]
    [InlineData("/api/cards/x/y/z")]
    public async Task Unknown_api_addresses_answer_404_problem_details(string path)
    {
        await using var factory = new MonthwiseApiFactory(UnreachableDatabase, migrateOnStartup: false, _webRoot);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Without_a_front_end_screen_addresses_are_not_found()
    {
        await using var factory = new MonthwiseApiFactory(UnreachableDatabase, migrateOnStartup: false);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/month", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("/scalar")]
    [InlineData("/openapi/v1.json")]
    public async Task Development_tools_are_not_served(string path)
    {
        await using var factory = new MonthwiseApiFactory(UnreachableDatabase, migrateOnStartup: false, _webRoot);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // The address is a screen address in a release: it gets the front-end, never the tool.
        Assert.DoesNotContain("openapi\":", body, StringComparison.Ordinal);
        Assert.DoesNotContain("scalar", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Failed_migrations_stop_the_api_but_not_the_health_check()
    {
        await using var factory = new MonthwiseApiFactory(UnreachableDatabase, migrateOnStartup: true, _webRoot);
        using var client = factory.CreateClient();

        var health = await client.GetAsync(new Uri("/api/health", UriKind.Relative), TestContext.Current.CancellationToken);
        var healthBody = await health.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var cards = await client.GetAsync(new Uri("/api/cards", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, health.StatusCode);
        Assert.Equal("Failed", healthBody.GetProperty("migrations").GetString());
        Assert.Equal(HttpStatusCode.ServiceUnavailable, cards.StatusCode);
        Assert.Equal("application/problem+json", cards.Content.Headers.ContentType?.MediaType);
    }
}
