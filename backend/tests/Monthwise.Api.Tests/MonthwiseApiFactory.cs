using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Monthwise.Api.Tests;

/// <summary>
/// Runs the API in memory against the given PostgreSQL connection string, optionally serving a
/// front-end from <paramref name="webRoot"/> as a release does.
/// </summary>
public sealed class MonthwiseApiFactory(string connectionString, bool migrateOnStartup, string? webRoot = null)
    : WebApplicationFactory<Program>
{
    /// <summary>Made-up keys for tests only. Every factory uses the same ones (the EF model caches the cipher).</summary>
    public static readonly string TestEncryptionKey = Convert.ToBase64String(Enumerable.Repeat((byte)7, 32).ToArray());
    public static readonly string TestHashKey = Convert.ToBase64String(Enumerable.Repeat((byte)9, 32).ToArray());

    /// <summary>
    /// Connection string of a test database, set in CI by the PostgreSQL service container.
    /// Tests that need a real database are skipped when it is not set.
    /// </summary>
    public static string? TestDatabaseConnectionString =>
        Environment.GetEnvironmentVariable("MONTHWISE_TEST_DB");

    /// <summary>Every log message the API wrote, to check that no sensitive value is logged.</summary>
    public ConcurrentQueue<string> LogMessages { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        if (webRoot is not null)
        {
            builder.UseWebRoot(webRoot);
        }

        builder.UseSetting("ConnectionStrings:Monthwise", connectionString);
        builder.UseSetting("Database:MigrateOnStartup", migrateOnStartup.ToString());
        builder.UseSetting("SensitiveData:ActiveKeyId", "test");
        builder.UseSetting("SensitiveData:EncryptionKeys:test", TestEncryptionKey);
        builder.UseSetting("SensitiveData:HashKey", TestHashKey);
        builder.ConfigureLogging(logging =>
        {
            logging.SetMinimumLevel(LogLevel.Debug);
            logging.Services.AddSingleton<ILoggerProvider>(new CollectingLoggerProvider(LogMessages));
        });
    }

    private sealed class CollectingLoggerProvider(ConcurrentQueue<string> messages) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new CollectingLogger(messages);
        public void Dispose()
        {
        }
    }

    private sealed class CollectingLogger(ConcurrentQueue<string> messages) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);
            messages.Enqueue($"{formatter(state, exception)} {exception}");
        }
    }
}
