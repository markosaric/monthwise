using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Monthwise.Application.Cards;
using Monthwise.Application.CashFlow;
using Monthwise.Application.Common;
using Monthwise.Infrastructure.Persistence;
using Monthwise.Infrastructure.Persistence.Repositories;
using Monthwise.Infrastructure.Security;

namespace Monthwise.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "Monthwise";
    public const string DatabaseHealthCheckName = "database";

    /// <summary>Registers EF Core with PostgreSQL, repositories, the sensitive data cipher and the database health check.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Resolved when the DbContext is first used, so tooling such as OpenAPI document
        // generation can start the app without a database.
        services.AddDbContext<MonthwiseDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString(ConnectionStringName)
                ?? throw new InvalidOperationException(
                    $"Connection string '{ConnectionStringName}' is not configured. See README.md, 'Initial development setup'.");
            ConfigureDbContext(options, connectionString);
        });

        // The keys are checked when the cipher is first created; the API creates it on startup.
        services.AddSingleton<ISensitiveDataCipher>(_ =>
            new SensitiveDataCipher(SensitiveDataOptions.FromConfiguration(configuration)));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<MonthwiseDbContext>());
        services.AddScoped<ICardRepository, CardRepository>();
        services.AddScoped(typeof(ICardListRepository<>), typeof(CardListRepository<>));
        services.AddSingleton<ICardActivityQuery, CardActivityQuery>();
        services.AddScoped<ICashFlowInputRepository, CashFlowInputRepository>();
        services.AddScoped<IRecurringInputRepository, RecurringInputRepository>();
        services.AddScoped<IPayerRepository, PayerRepository>();
        services.AddScoped<IInputCategoryRepository, InputCategoryRepository>();
        services.AddScoped<IBalanceEntryRepository, BalanceEntryRepository>();

        services.AddHealthChecks()
            .AddDbContextCheck<MonthwiseDbContext>(DatabaseHealthCheckName);

        return services;
    }

    /// <summary>Fails fast when the card number encryption keys are missing or invalid.</summary>
    public static void EnsureSensitiveDataKeys(this IServiceProvider services) =>
        services.GetRequiredService<ISensitiveDataCipher>();

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
