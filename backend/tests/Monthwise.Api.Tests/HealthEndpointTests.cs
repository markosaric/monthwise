using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Monthwise.Api.Tests;

[Collection(SharedDatabase.Name)]
public class HealthEndpointTests
{
    private const string UnreachableDatabase =
        "Host=127.0.0.1;Port=1;Database=monthwise;Username=monthwise;Timeout=1";

    [Fact]
    public async Task Reports_healthy_when_database_is_reachable()
    {
        var connectionString = MonthwiseApiFactory.TestDatabaseConnectionString;
        Assert.SkipWhen(string.IsNullOrEmpty(connectionString), "MONTHWISE_TEST_DB is not set; no test database available.");

        await using var factory = new MonthwiseApiFactory(connectionString!, migrateOnStartup: true);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/api/health", UriKind.Relative), TestContext.Current.CancellationToken);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", body.GetProperty("status").GetString());
        Assert.Equal("Healthy", body.GetProperty("database").GetString());
        Assert.Equal("Applied", body.GetProperty("migrations").GetString());
        Assert.StartsWith("0.0.0-dev", body.GetProperty("version").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reports_unhealthy_when_database_is_unreachable()
    {
        await using var factory = new MonthwiseApiFactory(UnreachableDatabase, migrateOnStartup: false);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/api/health", UriKind.Relative), TestContext.Current.CancellationToken);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("Unhealthy", body.GetProperty("status").GetString());
        Assert.Equal("Unhealthy", body.GetProperty("database").GetString());
        Assert.Equal("Pending", body.GetProperty("migrations").GetString());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("version").GetString()));
    }
}
