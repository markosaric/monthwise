using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Monthwise.Api.Tests;

/// <summary>Runs the API in memory against the given PostgreSQL connection string.</summary>
public sealed class MonthwiseApiFactory(string connectionString, bool migrateOnStartup) : WebApplicationFactory<Program>
{
    /// <summary>
    /// Connection string of a test database, set in CI by the PostgreSQL service container.
    /// Tests that need a real database are skipped when it is not set.
    /// </summary>
    public static string? TestDatabaseConnectionString =>
        Environment.GetEnvironmentVariable("MONTHWISE_TEST_DB");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Monthwise", connectionString);
        builder.UseSetting("Database:MigrateOnStartup", migrateOnStartup.ToString());
    }
}
