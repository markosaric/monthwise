using System.Net;
using Npgsql;

namespace Monthwise.Api.Tests;

/// <summary>A failed update leaves the database as it was (FR-3).</summary>
public class MigrationTests
{
    [Fact]
    public async Task A_failing_migration_reverts_the_migrations_applied_before_it()
    {
        var serverConnectionString = MonthwiseApiFactory.TestDatabaseConnectionString;
        Assert.SkipWhen(string.IsNullOrEmpty(serverConnectionString), "MONTHWISE_TEST_DB is not set; no test database available.");
        var cancellationToken = TestContext.Current.CancellationToken;

        var databaseName = $"monthwise_migration_{Guid.NewGuid():N}";
        var connectionString = new NpgsqlConnectionStringBuilder(serverConnectionString) { Database = databaseName, Pooling = false }.ConnectionString;
        await ExecuteAsync(serverConnectionString!, $"CREATE DATABASE {databaseName}", cancellationToken);
        try
        {
            // The last migration (AddSharedCosts) creates this table, so it fails after the earlier ones succeeded.
            await ExecuteAsync(connectionString, "CREATE TABLE participant (id integer)", cancellationToken);

            await using (var factory = new MonthwiseApiFactory(connectionString, migrateOnStartup: true))
            {
                using var client = factory.CreateClient();
                var health = await client.GetAsync(new Uri("/api/health", UriKind.Relative), cancellationToken);
                Assert.Equal(HttpStatusCode.ServiceUnavailable, health.StatusCode);
            }

            var cardTableExists = await ScalarAsync(
                connectionString, "SELECT to_regclass('public.card') IS NOT NULL", cancellationToken);
            Assert.False((bool)cardTableExists!);
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await ExecuteAsync(serverConnectionString!, $"DROP DATABASE IF EXISTS {databaseName} WITH (FORCE)", cancellationToken);
        }
    }

    private static async Task ExecuteAsync(string connectionString, string sql, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<object?> ScalarAsync(string connectionString, string sql, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        return await command.ExecuteScalarAsync(cancellationToken);
    }
}
