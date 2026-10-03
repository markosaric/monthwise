using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Monthwise.Infrastructure.Persistence;

namespace Monthwise.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "Monthwise";
    public const string DatabaseHealthCheckName = "database";

    /// <summary>Registers EF Core with PostgreSQL and the database health check.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Resolved when the DbContext is first used, so tooling such as OpenAPI document
        // generation can start the app without a database.
        services.AddDbContext<MonthwiseDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString(ConnectionStringName)
                ?? throw new InvalidOperationException(
                    $"Connection string '{ConnectionStringName}' is not configured. See README.md, 'Getting started'.");
            ConfigureDbContext(options, connectionString);
        });

        services.AddHealthChecks()
            .AddDbContextCheck<MonthwiseDbContext>(DatabaseHealthCheckName);

        return services;
    }

    /// <summary>Applies pending migrations. Called on startup in Development only.</summary>
    public static async Task MigrateDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MonthwiseDbContext>();
        await dbContext.Database.MigrateAsync(cancellationToken);
    }

    internal static void ConfigureDbContext(DbContextOptionsBuilder options, string connectionString) =>
        options
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history"))
            .UseSnakeCaseNamingConvention();
}
