namespace Monthwise.Api.Tests;

/// <summary>Database tests run, are skipped locally or fail in CI (database-tests-required.md FR-1 to FR-4).</summary>
public class TestDatabaseTests
{
    private const string Database = "Host=localhost;Database=monthwise_test;Username=monthwise";

    /// <summary>
    /// Shows the notice once per run on a local run without a test database (FR-1). `dotnet test`
    /// drops output written outside a test, but it always shows the reason of a skipped test.
    /// </summary>
    [Fact]
    public void Database_tests_notice()
    {
        if (TestDatabase.CurrentNotice is { } notice)
        {
            Assert.Skip(notice);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("true")]
    [InlineData("false")]
    public void Runs_when_a_test_database_is_set(string? ci) =>
        Assert.Equal(TestDatabaseDecision.Run, TestDatabase.Decide(Database, ci));

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData(null, "")]
    [InlineData(null, "false")]
    [InlineData("", "0")]
    public void Skips_on_a_local_run_without_a_test_database(string? connectionString, string? ci) =>
        Assert.Equal(TestDatabaseDecision.Skip, TestDatabase.Decide(connectionString, ci));

    [Theory]
    [InlineData(null, "true")]
    [InlineData("", "TRUE")]
    [InlineData("   ", "True")]
    [InlineData(null, " true ")]
    public void Fails_on_a_CI_run_without_a_test_database(string? connectionString, string? ci) =>
        Assert.Equal(TestDatabaseDecision.Fail, TestDatabase.Decide(connectionString, ci));
}
