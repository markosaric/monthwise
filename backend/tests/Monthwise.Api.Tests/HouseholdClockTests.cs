using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Monthwise.Application.Common;
using Npgsql;

namespace Monthwise.Api.Tests;

/// <summary>A clock stopped at one moment, for tests around the household's midnight.</summary>
public sealed class FixedClock(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;

    /// <summary>The server's own time zone, as on Docker and Azure.</summary>
    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
}

/// <summary>"Today" in the default household time zone, for tests that compare with the API's dates.</summary>
public static class HouseholdToday
{
    public static DateOnly Value =>
        new HouseholdTimeProvider(TimeProvider.System, HouseholdTimeZone.Resolve(null)).Today();
}

/// <summary>"Today" is the household's date, not the server's (production packaging, Appendix 6).</summary>
public class HouseholdClockTests
{
    private static DateOnly TodayAt(string utc, string? timeZone = null) =>
        new HouseholdTimeProvider(new FixedClock(DateTimeOffset.Parse(utc, System.Globalization.CultureInfo.InvariantCulture)),
            HouseholdTimeZone.Resolve(timeZone)).Today();

    [Theory]
    // Winter time (UTC+1): 00:30 in Zagreb on the first of the month.
    [InlineData("2026-11-30T23:30:00Z", "2026-12-01")]
    [InlineData("2026-11-30T22:59:59Z", "2026-11-30")]
    [InlineData("2026-11-30T23:00:00Z", "2026-12-01")]
    // Summer time (UTC+2).
    [InlineData("2026-06-30T22:30:00Z", "2026-07-01")]
    [InlineData("2026-06-30T21:59:59Z", "2026-06-30")]
    // The clocks go forward (29 March 2026) and back (25 October 2026).
    [InlineData("2026-03-28T22:59:59Z", "2026-03-28")]
    [InlineData("2026-03-28T23:00:00Z", "2026-03-29")]
    [InlineData("2026-03-29T21:59:59Z", "2026-03-29")]
    [InlineData("2026-03-29T22:00:00Z", "2026-03-30")]
    [InlineData("2026-10-24T21:59:59Z", "2026-10-24")]
    [InlineData("2026-10-24T22:00:00Z", "2026-10-25")]
    [InlineData("2026-10-25T22:59:59Z", "2026-10-25")]
    [InlineData("2026-10-25T23:00:00Z", "2026-10-26")]
    public void Today_changes_at_midnight_in_the_default_household_time_zone(string utc, string expected)
    {
        Assert.Equal(DateOnly.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), TodayAt(utc));
    }

    [Fact]
    public void Today_follows_another_household_time_zone()
    {
        Assert.Equal(new DateOnly(2026, 11, 30), TodayAt("2026-11-30T23:30:00Z", "America/New_York"));
        Assert.Equal(new DateOnly(2026, 12, 1), TodayAt("2026-12-01T05:00:00Z", "America/New_York"));
    }

    [Fact]
    public void The_current_moment_and_timestamps_are_unchanged()
    {
        var clock = new FixedClock(new DateTimeOffset(2026, 11, 30, 23, 30, 0, TimeSpan.Zero));
        var household = new HouseholdTimeProvider(clock, HouseholdTimeZone.Resolve(null));

        Assert.Equal(clock.GetUtcNow(), household.GetUtcNow());
        Assert.Equal(TimeSpan.FromHours(1), household.GetLocalNow().Offset);
        Assert.Equal(clock.GetUtcNow(), household.GetLocalNow());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" Europe/Zagreb ")]
    public void The_default_and_a_given_zone_resolve(string? id)
    {
        Assert.Equal(HouseholdTimeZone.DefaultId, HouseholdTimeZone.Resolve(id).Id);
    }

    [Fact]
    public void An_unknown_zone_names_the_setting()
    {
        var error = Assert.Throws<InvalidOperationException>(() => HouseholdTimeZone.Resolve("Europe/Zagrb"));

        Assert.Contains("Household:TimeZone", error.Message, StringComparison.Ordinal);
        Assert.Contains("Europe/Zagrb", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_api_does_not_start_with_an_unknown_zone()
    {
        await using var defaults = new MonthwiseApiFactory(TestDatabaseless, migrateOnStartup: false);
        await using var factory = defaults.WithWebHostBuilder(builder => builder.UseSetting(HouseholdTimeZone.SettingName, "Europe/Zagrb"));

        var error = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());

        Assert.Contains("Household:TimeZone", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_health_response_shows_the_household_time_zone()
    {
        await using var factory = new MonthwiseApiFactory(TestDatabaseless, migrateOnStartup: false);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/api/health", UriKind.Relative), TestContext.Current.CancellationToken);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        Assert.Equal("Europe/Zagreb", body.GetProperty("timeZone").GetString());
    }

    private const string TestDatabaseless = "Host=127.0.0.1;Port=1;Database=monthwise;Username=monthwise;Timeout=1";
}

/// <summary>
/// The API at 00:30 Croatian time on the first of a month while the server is on UTC (acceptance
/// criteria 19 and 20 of Appendix 6), against a real PostgreSQL (see TestDatabase).
/// </summary>
[Collection(SharedDatabase.Name)]
public sealed class HouseholdClockEndpointTests : IAsyncLifetime
{
    // 00:30 on 1 December 2026 in Zagreb (winter time), still 30 November in UTC.
    private static readonly DateTimeOffset JustAfterMidnight = new(2026, 11, 30, 23, 30, 0, TimeSpan.Zero);
    private static readonly Guid VisaIssuer = new("0199b0c0-0000-7000-8000-000000000001");
    private static readonly Guid ZaBa = new("0199b0c0-0000-7000-8000-000000000101");
    private static readonly Guid Salary = new("0199b0c0-0000-7000-8000-000000000201");
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private MonthwiseApiFactory? _factory;

    public async ValueTask InitializeAsync()
    {
        // Bring the database up to date and empty it, as the other endpoint tests do.
        await using var setup = new MonthwiseApiFactory(TestDatabase.ConnectionString, migrateOnStartup: true);
        using var client = setup.CreateClient();
        await client.GetAsync(new Uri("/api/health", UriKind.Relative), Ct);
        await using var connection = new NpgsqlConnection(TestDatabase.ConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(CashFlowEndpointTests.DeleteAllSql, connection);
        await command.ExecuteNonQueryAsync(Ct);
    }

    public async ValueTask DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
    }

    [Fact]
    public async Task The_dashboard_and_a_received_input_use_the_household_date()
    {
        using var client = await StartAsync(timeZone: null);

        var dashboard = await GetAsync(client, "/api/dashboard");
        Assert.Equal("2026-12", dashboard.GetProperty("month").GetString());

        var id = await CreateInputAsync(client, new DateOnly(2026, 12, 1));
        var received = await SendAsync(client, HttpMethod.Post, $"/api/inputs/{id}/received");
        Assert.Equal("2026-12-01", received.GetProperty("receivedDate").GetString());
    }

    [Fact]
    public async Task Another_household_time_zone_is_followed()
    {
        using var client = await StartAsync(timeZone: "America/New_York");

        var dashboard = await GetAsync(client, "/api/dashboard");
        Assert.Equal("2026-11", dashboard.GetProperty("month").GetString());

        var id = await CreateInputAsync(client, new DateOnly(2026, 11, 30));
        var received = await SendAsync(client, HttpMethod.Post, $"/api/inputs/{id}/received");
        Assert.Equal("2026-11-30", received.GetProperty("receivedDate").GetString());
    }

    private async Task<HttpClient> StartAsync(string? timeZone)
    {
        _factory = new MonthwiseApiFactory(TestDatabase.ConnectionString, migrateOnStartup: false);
        var configured = _factory.WithWebHostBuilder(builder =>
        {
            if (timeZone is not null)
            {
                builder.UseSetting(HouseholdTimeZone.SettingName, timeZone);
            }

            builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(provider =>
                new HouseholdTimeProvider(
                    new FixedClock(JustAfterMidnight),
                    HouseholdTimeZone.Resolve(provider.GetRequiredService<IConfiguration>()[HouseholdTimeZone.SettingName]))));
        });
        return await configured.CreateSignedInClientAsync();
    }

    private static async Task<Guid> CreateInputAsync(HttpClient client, DateOnly date)
    {
        var account = await SendAsync(client, HttpMethod.Post, "/api/cards", new
        {
            name = "Main account",
            issuerId = VisaIssuer,
            bankId = ZaBa,
            cardNumber = (string?)null,
            iban = (string?)null,
            validFrom = (string?)null,
            validThrough = "2099-12",
            type = "Debit",
            isPrimary = true,
            debtLimitCents = (long?)null,
            allowsInstallments = false,
        });
        var iso = date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        var created = await SendAsync(client, HttpMethod.Post, "/api/inputs", new
        {
            payerName = "Employer",
            accountId = account.GetProperty("id").GetGuid(),
            categoryId = Salary,
            amountCents = 150_000,
            issueDate = iso,
            expectedDate = iso,
            receivedDate = (string?)null,
            description = (string?)null,
            recurrence = (object?)null,
        });
        return created.GetProperty("id").GetGuid();
    }

    private static async Task<JsonElement> GetAsync(HttpClient client, string path)
    {
        var response = await client.GetAsync(new Uri(path, UriKind.Relative), Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    private static async Task<JsonElement> SendAsync(HttpClient client, HttpMethod method, string path, object? body = null)
    {
        using var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative));
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        var response = await client.SendAsync(request, Ct);
        var text = await response.Content.ReadAsStringAsync(Ct);
        Assert.True(response.IsSuccessStatusCode, $"{method} {path}: {(int)response.StatusCode} {text}");
        return JsonDocument.Parse(text).RootElement.Clone();
    }
}
