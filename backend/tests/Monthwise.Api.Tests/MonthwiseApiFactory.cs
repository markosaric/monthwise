using System.Collections.Concurrent;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Monthwise.Application.Users;
using Monthwise.Domain.Users;

namespace Monthwise.Api.Tests;

/// <summary>
/// Runs the API in memory against the given PostgreSQL connection string, optionally serving a
/// front-end from <paramref name="webRoot"/> as a release does. <paramref name="settings"/> are extra
/// settings, e.g. a setup code. Use <see cref="CreateSignedInClientAsync"/> for calls that need a user.
/// </summary>
public sealed class MonthwiseApiFactory(
    string connectionString, bool migrateOnStartup, string? webRoot = null, IReadOnlyDictionary<string, string?>? settings = null)
    : WebApplicationFactory<Program>
{
    /// <summary>The password of every user made by <see cref="CreateUserAsync"/> (made up, tests only).</summary>
    public const string TestPassword = "test password 1234";

    /// <summary>The header every changing call of Monthwise's own screens carries (login NFR-4).</summary>
    public const string RequestHeader = "X-Monthwise-Request";

    /// <summary>Made-up keys for tests only. Every factory uses the same ones (the EF model caches the cipher).</summary>
    public static readonly string TestEncryptionKey = Convert.ToBase64String(Enumerable.Repeat((byte)7, 32).ToArray());
    public static readonly string TestHashKey = Convert.ToBase64String(Enumerable.Repeat((byte)9, 32).ToArray());

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
        // Many sign-ins per test class; the rate limit itself is tested with a low setting.
        builder.UseSetting("Login:SignInAttemptsPerMinute", "10000");
        foreach (var (key, value) in settings ?? new Dictionary<string, string?>())
        {
            builder.UseSetting(key, value);
        }
        builder.ConfigureLogging(logging =>
        {
            logging.SetMinimumLevel(LogLevel.Debug);
            logging.Services.AddSingleton<ILoggerProvider>(new CollectingLoggerProvider(LogMessages));
        });
    }

    /// <summary>A client that keeps cookies and sends the request header, like the front-end, without a session.</summary>
    public HttpClient CreateBrowserClient() => SignedInClients.CreateBrowserClient(this);

    /// <summary>A client signed in as a new user of the given role (login FR-2).</summary>
    public Task<HttpClient> CreateSignedInClientAsync(
        UserRole role = UserRole.HouseholdAdmin, string displayName = "Test user", bool staySignedIn = false) =>
        SignedInClients.CreateSignedInClientAsync(this, role, displayName, staySignedIn);

    /// <summary>A browser client signed in with the given username and password; fails when signing in fails.</summary>
    public Task<HttpClient> SignInAsync(string username, string password, bool staySignedIn = false) =>
        SignedInClients.SignInAsync(this, username, password, staySignedIn);

    /// <summary>A new active user with a unique username and <see cref="TestPassword"/>.</summary>
    public Task<UserAccount> CreateUserAsync(
        UserRole role = UserRole.HouseholdAdmin, string displayName = "Test user", bool mustChangePassword = false) =>
        SignedInClients.CreateUserAsync(this, role, displayName, mustChangePassword);

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

/// <summary>Signed-in clients for any instance of the API, also one changed with <c>WithWebHostBuilder</c>.</summary>
public static class SignedInClients
{
    public static HttpClient CreateBrowserClient(this WebApplicationFactory<Program> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(MonthwiseApiFactory.RequestHeader, "1");
        return client;
    }

    public static async Task<HttpClient> CreateSignedInClientAsync(
        this WebApplicationFactory<Program> factory,
        UserRole role = UserRole.HouseholdAdmin,
        string displayName = "Test user",
        bool staySignedIn = false)
    {
        var user = await factory.CreateUserAsync(role, displayName);
        return await factory.SignInAsync(user.Username, MonthwiseApiFactory.TestPassword, staySignedIn);
    }

    public static async Task<HttpClient> SignInAsync(
        this WebApplicationFactory<Program> factory, string username, string password, bool staySignedIn = false)
    {
        var client = factory.CreateBrowserClient();
        using var response = await client.PostAsJsonAsync(
            new Uri("/api/auth/sign-in", UriKind.Relative),
            new { username, password, staySignedIn },
            TestContext.Current.CancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            client.Dispose();
            throw new InvalidOperationException(
                $"Signing in as {username} failed: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)}");
        }
        return client;
    }

    public static async Task<UserAccount> CreateUserAsync(
        this WebApplicationFactory<Program> factory,
        UserRole role = UserRole.HouseholdAdmin,
        string displayName = "Test user",
        bool mustChangePassword = false)
    {
        ArgumentNullException.ThrowIfNull(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var accounts = scope.ServiceProvider.GetRequiredService<IUserAccounts>();
        var username = $"test-{Guid.NewGuid():N}"[..24];
        var created = await accounts.CreateAsync(
            displayName, username, role, MonthwiseApiFactory.TestPassword, mustChangePassword, TestContext.Current.CancellationToken);
        return created.Value;
    }
}
