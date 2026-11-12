using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Monthwise.Application.Common;
using Monthwise.Domain.Cards;
using Monthwise.Domain.Loans;
using Monthwise.Infrastructure.Persistence;
using Npgsql;

namespace Monthwise.Api.Tests;

/// <summary>
/// A change the database refuses becomes a <see cref="DatabaseRuleException"/> of the right kind,
/// with nothing saved and no record values in its message (error mapping FR-1 to FR-5, A1 to A5),
/// against a real PostgreSQL (see TestDatabase). The changes skip the handlers' own checks on
/// purpose, as a race would.
/// </summary>
[Collection(SharedDatabase.Name)]
public sealed class DatabaseRuleTests : IAsyncLifetime
{
    private const string Prefix = "Rule test";
    private const string CleanUpSql =
        $"DELETE FROM loan_subcategory WHERE name COLLATE \"C\" LIKE '{Prefix}%'; DELETE FROM loan_category WHERE name COLLATE \"C\" LIKE '{Prefix}%';";

    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private MonthwiseApiFactory? _factory;
    private string _connectionString = string.Empty;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _connectionString = TestDatabase.ConnectionString;
        _factory = new MonthwiseApiFactory(_connectionString, migrateOnStartup: true);
        using var client = _factory.CreateClient();
        await client.GetAsync(new Uri("/api/health", UriKind.Relative), Ct);
        await ExecuteSqlAsync(CleanUpSql);
    }

    public async ValueTask DisposeAsync()
    {
        if (_factory is not null)
        {
            await ExecuteSqlAsync(CleanUpSql);
            await _factory.DisposeAsync();
        }
    }

    [Fact]
    public async Task A_duplicate_value_is_a_unique_rule()
    {
        var error = await SaveAsync(db => db.Banks.Add(Bank.Create("ZaBa").Value));

        Assert.Equal(DatabaseRule.Unique, error.Rule);
        Assert.Equal("A value that must be unique is already in use.", error.Message);
        Assert.Equal(1, await CountAsync("SELECT count(*) FROM bank WHERE name = 'ZaBa'"));
    }

    [Fact]
    public async Task A_reference_to_a_missing_record_is_a_missing_reference()
    {
        var name = $"{Prefix} orphan";

        var error = await SaveAsync(db => db.LoanSubcategories.Add(LoanSubcategory.Create(Guid.NewGuid(), name, Now).Value));

        Assert.Equal(DatabaseRule.MissingReference, error.Rule);
        Assert.Equal("A record this change refers to no longer exists. Reload and try again.", error.Message);
        Assert.DoesNotContain(name, error.Message, StringComparison.Ordinal);
        Assert.Equal(0, await CountAsync($"SELECT count(*) FROM loan_subcategory WHERE name = '{name}'"));
    }

    [Fact]
    public async Task Deleting_a_record_that_is_still_used_is_still_referenced()
    {
        var category = LoanCategory.Create($"{Prefix} used", allowsResidualValue: false, Now).Value;
        await SeedAsync(db =>
        {
            db.LoanCategories.Add(category);
            db.LoanSubcategories.Add(LoanSubcategory.Create(category.Id, $"{Prefix} child", Now).Value);
        });

        var error = await SaveAsync(db => db.LoanCategories.Remove(db.LoanCategories.Single(c => c.Id == category.Id)));

        Assert.Equal(DatabaseRule.StillReferenced, error.Rule);
        Assert.Equal("This record is still used by other data and cannot be deleted.", error.Message);
        Assert.Equal(1, await CountAsync($"SELECT count(*) FROM loan_category WHERE id = '{category.Id}'"));
    }

    [Fact]
    public async Task A_value_the_database_does_not_allow_is_a_value_rule()
    {
        var category = LoanCategory.Create($"{Prefix} no name", allowsResidualValue: false, Now).Value;

        var error = await SaveAsync(db =>
        {
            db.LoanCategories.Add(category);
            db.Entry(category).Property(c => c.Name).CurrentValue = null!;
        });

        Assert.Equal(DatabaseRule.ValueNotAllowed, error.Rule);
        Assert.Equal("Some of the values are not allowed. Check the entries and try again.", error.Message);
        Assert.Equal(0, await CountAsync($"SELECT count(*) FROM loan_category WHERE id = '{category.Id}'"));
    }

    [Fact]
    public async Task Other_database_errors_are_not_translated()
    {
        await using var scope = _factory!.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MonthwiseDbContext>();
        db.LoanCategories.Add(LoanCategory.Create($"{Prefix} too long", allowsResidualValue: false, Now).Value);
        db.Entry(db.LoanCategories.Local.Single()).Property(c => c.Name).CurrentValue = new string('x', 80);

        var error = await Assert.ThrowsAsync<DbUpdateException>(() => ((IUnitOfWork)db).SaveChangesAsync(Ct));

        Assert.Equal(PostgresErrorCodes.StringDataRightTruncation, Assert.IsType<PostgresException>(error.InnerException).SqlState);
    }

    /// <summary>Makes the change in a fresh unit of work and returns the rule error it raises.</summary>
    private async Task<DatabaseRuleException> SaveAsync(Action<MonthwiseDbContext> change)
    {
        await using var scope = _factory!.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MonthwiseDbContext>();
        change(db);
        return await Assert.ThrowsAsync<DatabaseRuleException>(() => ((IUnitOfWork)db).SaveChangesAsync(Ct));
    }

    private async Task SeedAsync(Action<MonthwiseDbContext> change)
    {
        await using var scope = _factory!.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MonthwiseDbContext>();
        change(db);
        await ((IUnitOfWork)db).SaveChangesAsync(Ct);
    }

    private async Task<long> CountAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(sql, connection);
        return (long)(await command.ExecuteScalarAsync(Ct))!;
    }

    private async Task ExecuteSqlAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(Ct);
    }
}
