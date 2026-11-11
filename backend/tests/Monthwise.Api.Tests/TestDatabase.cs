namespace Monthwise.Api.Tests;

/// <summary>
/// The one place that decides whether a test that needs a real PostgreSQL runs, is skipped or fails.
/// The database is given by <c>MONTHWISE_TEST_DB</c>: without it a local run skips these tests (and
/// <see cref="TestDatabaseTests.Database_tests_notice"/> says so once), and a CI run
/// (<c>CI=true</c>) fails them, so CI can never turn green without testing the database.
/// </summary>
public static class TestDatabase
{
    public const string VariableName = "MONTHWISE_TEST_DB";

    public const string SkipReason =
        "MONTHWISE_TEST_DB is not set; database tests are skipped. "
        + "Set it to a test database connection string to run them (see README).";

    public const string FailMessage =
        "MONTHWISE_TEST_DB is not set, but this is a CI run (CI=true). CI must provide a test database.";

    public const string Notice =
        "NOTICE: MONTHWISE_TEST_DB is not set, so the database tests were skipped. "
        + "Set it to a test database connection string to run them (see README, \"Run the tests\").";

    /// <summary>
    /// The connection string of the test database. Skips the calling test on a local run without it
    /// and fails it on a CI run without it.
    /// </summary>
    public static string ConnectionString
    {
        get
        {
            var connectionString = Environment.GetEnvironmentVariable(VariableName);
            var decision = Decide(connectionString, Environment.GetEnvironmentVariable("CI"));
            if (decision == TestDatabaseDecision.Skip)
            {
                Assert.Skip(SkipReason);
            }

            if (decision == TestDatabaseDecision.Fail)
            {
                Assert.Fail(FailMessage);
            }

            return connectionString!;
        }
    }

    /// <summary>The notice for this run, or <c>null</c> when nothing is skipped.</summary>
    public static string? CurrentNotice =>
        Decide(Environment.GetEnvironmentVariable(VariableName), Environment.GetEnvironmentVariable("CI")) == TestDatabaseDecision.Skip
            ? Notice
            : null;

    /// <summary>A blank connection string counts as not set; <c>CI=true</c> turns a skip into a failure.</summary>
    public static TestDatabaseDecision Decide(string? connectionString, string? ci)
    {
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            return TestDatabaseDecision.Run;
        }

        return string.Equals(ci?.Trim(), "true", StringComparison.OrdinalIgnoreCase) ? TestDatabaseDecision.Fail : TestDatabaseDecision.Skip;
    }
}

/// <summary>Whether a database test runs, is skipped or fails.</summary>
public enum TestDatabaseDecision
{
    Run,
    Skip,
    Fail,
}
