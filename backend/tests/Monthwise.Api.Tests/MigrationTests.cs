using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Monthwise.Infrastructure.Persistence;
using Npgsql;

namespace Monthwise.Api.Tests;

/// <summary>A failed update stops and removes nothing (production packaging FR-3 and Appendix 3, name comparison FR-6).</summary>
public class MigrationTests
{
    [Fact]
    public async Task A_failing_migration_keeps_the_migrations_applied_before_it_and_reports_the_failure()
    {
        var serverConnectionString = TestDatabase.ConnectionString;
        var cancellationToken = TestContext.Current.CancellationToken;

        var databaseName = $"monthwise_migration_{Guid.NewGuid():N}";
        var connectionString = new NpgsqlConnectionStringBuilder(serverConnectionString) { Database = databaseName, Pooling = false }.ConnectionString;
        await ExecuteAsync(serverConnectionString, $"CREATE DATABASE {databaseName}", cancellationToken);
        try
        {
            // AddSharedCosts creates this table, so it fails after the earlier migrations succeeded.
            await ExecuteAsync(connectionString, "CREATE TABLE participant (id integer)", cancellationToken);

            await using (var factory = new MonthwiseApiFactory(connectionString, migrateOnStartup: true))
            {
                using var client = factory.CreateClient();
                var health = await client.GetAsync(new Uri("/api/health", UriKind.Relative), cancellationToken);
                Assert.Equal(HttpStatusCode.ServiceUnavailable, health.StatusCode);
            }

            // Nothing is undone: the earlier migrations and their tables stay, the failing one is not recorded.
            var cardTableExists = await ScalarAsync(
                connectionString, "SELECT to_regclass('public.card') IS NOT NULL", cancellationToken);
            Assert.True((bool)cardTableExists!);
            Assert.Equal(
                "20261009130440_AddBillsAndExpenses",
                await ScalarAsync(connectionString, "SELECT max(migration_id) FROM __ef_migrations_history", cancellationToken));
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await ExecuteAsync(serverConnectionString, $"DROP DATABASE IF EXISTS {databaseName} WITH (FORCE)", cancellationToken);
        }
    }

    [Fact]
    public async Task Names_differing_only_in_case_stop_the_update_until_one_is_renamed_whatever_the_database_locale()
    {
        const string beforeNameCollation = "20261104085946_AddRecurringBillOptions";
        var serverConnectionString = TestDatabase.ConnectionString;
        var cancellationToken = TestContext.Current.CancellationToken;

        // A database created with the C locale, where lower() leaves Č as it is, so the old indexes
        // let both names in.
        var databaseName = $"monthwise_migration_{Guid.NewGuid():N}";
        var connectionString = new NpgsqlConnectionStringBuilder(serverConnectionString) { Database = databaseName, Pooling = false }.ConnectionString;
        await ExecuteAsync(serverConnectionString, $"CREATE DATABASE {databaseName} LOCALE 'C' TEMPLATE template0", cancellationToken);
        try
        {
            await using var factory = new MonthwiseApiFactory(connectionString, migrateOnStartup: false);
            await using var scope = factory.Services.CreateAsyncScope();
            var migrator = scope.ServiceProvider.GetRequiredService<MonthwiseDbContext>().GetService<IMigrator>();
            await migrator.MigrateAsync(beforeNameCollation, cancellationToken: cancellationToken);
            await ExecuteAsync(
                connectionString,
                "INSERT INTO payer (id, name, created_at) VALUES "
                + "('0199b0c0-0000-7000-8000-00000000f101', 'Čevapi', now()), ('0199b0c0-0000-7000-8000-00000000f102', 'čevapi', now()); "
                + "INSERT INTO bank (id, name, is_predefined) VALUES ('0199b0c0-0000-7000-8000-00000000f201', 'Štedbanka', false)",
                cancellationToken);

            var refused = await Assert.ThrowsAsync<PostgresException>(() => migrator.MigrateAsync(cancellationToken: cancellationToken));
            Assert.Equal(PostgresErrorCodes.UniqueViolation, refused.SqlState);
            Assert.Contains("payer: 'Čevapi', 'čevapi'", refused.MessageText, StringComparison.Ordinal);
            Assert.DoesNotContain("bank", refused.MessageText, StringComparison.Ordinal);
            Assert.Equal(2L, await ScalarAsync(connectionString, "SELECT count(*) FROM payer", cancellationToken));
            Assert.Equal(
                beforeNameCollation,
                await ScalarAsync(connectionString, "SELECT max(migration_id) FROM __ef_migrations_history", cancellationToken));

            // After renaming one, the update goes through and keeps every row.
            await ExecuteAsync(connectionString, "UPDATE payer SET name = 'Čevapi 2' WHERE name = 'čevapi'", cancellationToken);
            await migrator.MigrateAsync(cancellationToken: cancellationToken);
            Assert.Equal(2L, await ScalarAsync(connectionString, "SELECT count(*) FROM payer", cancellationToken));
            Assert.Equal(1L, await ScalarAsync(connectionString, "SELECT count(*) FROM bank WHERE name = 'Štedbanka'", cancellationToken));

            var duplicate = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(
                connectionString,
                "INSERT INTO payer (id, name, created_at) VALUES ('0199b0c0-0000-7000-8000-00000000f103', 'ČEVAPI', now())",
                cancellationToken));
            Assert.Equal("ux_payer_name", duplicate.ConstraintName);

            // Reverting the update brings the earlier indexes back.
            await migrator.MigrateAsync(beforeNameCollation, cancellationToken: cancellationToken);
            Assert.Equal(
                true,
                await ScalarAsync(connectionString, "SELECT to_regclass('public.ux_payer_name_lower') IS NOT NULL", cancellationToken));
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await ExecuteAsync(serverConnectionString, $"DROP DATABASE IF EXISTS {databaseName} WITH (FORCE)", cancellationToken);
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
