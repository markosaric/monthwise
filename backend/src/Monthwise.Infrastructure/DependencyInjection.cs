using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Monthwise.Application.CardPurchases;
using Monthwise.Application.Cards;
using Monthwise.Application.CashFlow;
using Monthwise.Application.Common;
using Monthwise.Application.Expenses;
using Monthwise.Application.Loans;
using Monthwise.Application.SharedCosts;
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
        services.AddScoped<ICardActivityQuery, CardActivityQuery>();
        services.AddScoped<ICashFlowInputRepository, CashFlowInputRepository>();
        services.AddScoped<IRecurringInputRepository, RecurringInputRepository>();
        services.AddScoped<IPayerRepository, PayerRepository>();
        services.AddScoped<IInputCategoryRepository, InputCategoryRepository>();
        services.AddScoped<IBalanceEntryRepository, BalanceEntryRepository>();
        services.AddScoped<ILoanRepository, LoanRepository>();
        services.AddScoped<ILoanPaymentRepository, LoanPaymentRepository>();
        services.AddScoped<ILoanCategoryRepository, LoanCategoryRepository>();
        services.AddScoped<ILoanSubcategoryRepository, LoanSubcategoryRepository>();
        services.AddScoped<ICardPurchaseRepository, CardPurchaseRepository>();
        services.AddScoped<ICardPaymentRepository, CardPaymentRepository>();
        services.AddScoped<IExpenseCategoryRepository, ExpenseCategoryRepository>();
        services.AddScoped<IRecurringBillRepository, RecurringBillRepository>();
        services.AddScoped<IBillRepository, BillRepository>();
        services.AddScoped<IExpenseRepository, ExpenseRepository>();
        services.AddScoped<IParticipantRepository, ParticipantRepository>();

        services.AddSingleton<DatabaseMigrationState>();

        services.AddHealthChecks()
            .AddDbContextCheck<MonthwiseDbContext>(DatabaseHealthCheckName);

        return services;
    }

    /// <summary>Fails fast when the card number encryption keys are missing or invalid.</summary>
    public static void EnsureSensitiveDataKeys(this IServiceProvider services) =>
        services.GetRequiredService<ISensitiveDataCipher>();

    /// <summary>
    /// Waits until the database accepts connections, at most <paramref name="timeout"/>, e.g. while
    /// PostgreSQL is still starting after the computer's login. Returns whether it answered.
    /// </summary>
    public static async Task<bool> WaitForDatabaseAsync(this IServiceProvider services, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (true)
        {
            await using (var scope = services.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<MonthwiseDbContext>();
                if (await dbContext.Database.CanConnectAsync(cancellationToken))
                {
                    return true;
                }
            }

            if (DateTimeOffset.UtcNow >= deadline)
            {
                return false;
            }

            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        }
    }

    /// <summary>
    /// Applies pending migrations and records the outcome in <see cref="DatabaseMigrationState"/>.
    /// EF Core runs each migration in its own transaction; when one fails, the migrations applied
    /// in this run are reverted, so the database is left as it was before the update.
    /// </summary>
    public static async Task MigrateDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var state = services.GetRequiredService<DatabaseMigrationState>();
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MonthwiseDbContext>();
        string? lastAppliedBefore = null;
        try
        {
            lastAppliedBefore = (await dbContext.Database.GetAppliedMigrationsAsync(cancellationToken)).LastOrDefault();
            await dbContext.Database.MigrateAsync(cancellationToken);
            state.Status = MigrationStatus.Applied;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            state.Status = MigrationStatus.Failed;
            await RevertMigrationsAsync(dbContext, lastAppliedBefore, services, cancellationToken);
            throw;
        }
    }

    private static async Task RevertMigrationsAsync(
        MonthwiseDbContext dbContext, string? lastAppliedBefore, IServiceProvider services, CancellationToken cancellationToken)
    {
        try
        {
            var applied = await dbContext.Database.GetAppliedMigrationsAsync(cancellationToken);
            if (applied.LastOrDefault() == lastAppliedBefore)
            {
                return;
            }

            await dbContext.GetService<IMigrator>().MigrateAsync(lastAppliedBefore ?? Migration.InitialDatabase, cancellationToken: cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DependencyInjection))
                .LogError(ex, "Reverting the migrations applied before the failure failed");
        }
    }

    internal static void ConfigureDbContext(DbContextOptionsBuilder options, string connectionString) =>
        options
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history"))
            .UseSnakeCaseNamingConvention();
}
