using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Npgsql;

namespace Monthwise.Api.Tests;

/// <summary>
/// The overall balance of the primary accounts (overall balance tile FR-2 to FR-5) against a real
/// PostgreSQL (skipped without MONTHWISE_TEST_DB). Each test starts from empty tables; dates are relative
/// to today.
/// </summary>
[Collection(SharedDatabase.Name)]
public sealed class OverallBalanceEndpointTests : IAsyncLifetime
{
    private static readonly Guid VisaIssuer = new("0199b0c0-0000-7000-8000-000000000001");
    private static readonly Guid ZaBa = new("0199b0c0-0000-7000-8000-000000000101");
    private static readonly Guid Salary = new("0199b0c0-0000-7000-8000-000000000201");
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.Now);
    private static readonly DateOnly MonthStart = new(Today.Year, Today.Month, 1);

    private MonthwiseApiFactory? _factory;
    private HttpClient? _client;
    private string _connectionString = string.Empty;
    private Guid _electricity;

    private HttpClient Client => _client!;
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _connectionString = MonthwiseApiFactory.TestDatabaseConnectionString ?? string.Empty;
        Assert.SkipWhen(_connectionString.Length == 0, "MONTHWISE_TEST_DB is not set; no test database available.");

        _factory = new MonthwiseApiFactory(_connectionString, migrateOnStartup: true);
        _client = _factory.CreateClient();
        await Client.GetAsync(new Uri("/api/health", UriKind.Relative), Ct);
        await ExecuteSqlAsync(CashFlowEndpointTests.DeleteAllSql);
        var categories = (await GetAsync("/api/expense-categories")).EnumerateArray().ToList();
        _electricity = categories.Single(c => c.GetProperty("name").GetString() == "Electricity").GetProperty("id").GetGuid();
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
    }

    [Fact]
    public async Task Without_a_primary_account_there_is_no_value()
    {
        await CreateAccountAsync("Other account", isPrimary: false);

        var overall = await GetAsync("/api/overall-balance");

        Assert.Equal(Month(MonthStart), overall.GetProperty("month").GetString());
        Assert.True(overall.GetProperty("isExpected").GetBoolean());
        Assert.Equal(JsonValueKind.Null, overall.GetProperty("balanceCents").ValueKind);
        Assert.Equal(0, overall.GetProperty("primaryAccountCount").GetInt32());
    }

    [Fact]
    public async Task One_primary_account_equals_its_expected_balance()
    {
        var account = await CreateAccountAsync("Main account");
        await StartingBalanceAsync(account, 100_000, MonthStart);
        await SendAsync(HttpMethod.Post, "/api/inputs", Input(account, 20_000, expected: Today, received: null));
        await SendAsync(HttpMethod.Post, $"/api/bills?month={Month(MonthStart)}", Bill(account, 5_000, due: Today));

        var overall = await GetAsync("/api/overall-balance");
        var balance = (await GetAsync($"/api/cards/{account}/balance")).GetProperty("balance");

        Assert.Equal(115_000, overall.GetProperty("balanceCents").GetInt64());
        Assert.Equal(balance.GetProperty("expectedBalanceCents").GetInt64(), overall.GetProperty("balanceCents").GetInt64());
        Assert.Equal(1, overall.GetProperty("primaryAccountCount").GetInt32());
        Assert.False(overall.GetProperty("missingStartingBalance").GetBoolean());
    }

    [Fact]
    public async Task Several_primary_accounts_are_summed_and_counted()
    {
        var first = await CreateAccountAsync("First account");
        var second = await CreateAccountAsync("Second account");
        await StartingBalanceAsync(first, 100_000, MonthStart);
        await StartingBalanceAsync(second, 50_000, MonthStart);

        var overall = await GetAsync("/api/overall-balance");

        Assert.Equal(150_000, overall.GetProperty("balanceCents").GetInt64());
        Assert.Equal(2, overall.GetProperty("primaryAccountCount").GetInt32());
    }

    [Fact]
    public async Task Paying_or_receiving_does_not_change_it_but_new_items_do()
    {
        var account = await CreateAccountAsync("Main account");
        await StartingBalanceAsync(account, 100_000, MonthStart);
        var before = await BalanceAsync();

        var bill = await SendAsync(HttpMethod.Post, $"/api/bills?month={Month(MonthStart)}", Bill(account, 5_000, due: Today));
        Assert.Equal(before - 5_000, await BalanceAsync());
        await SendAsync(HttpMethod.Post, $"/api/bills/{bill.GetProperty("id").GetGuid()}/paid", new { paidDate = Iso(Today), paidFromId = account });
        Assert.Equal(before - 5_000, await BalanceAsync());

        var input = await SendAsync(HttpMethod.Post, "/api/inputs", Input(account, 10_000, expected: Today, received: null));
        Assert.Equal(before + 5_000, await BalanceAsync());
        await SendAsync(HttpMethod.Post, $"/api/inputs/{input.GetProperty("id").GetGuid()}/received");
        Assert.Equal(before + 5_000, await BalanceAsync());
    }

    [Fact]
    public async Task An_unpaid_bill_from_last_month_lowers_this_month()
    {
        var account = await CreateAccountAsync("Main account");
        var lastMonth = MonthStart.AddMonths(-1);
        await StartingBalanceAsync(account, 100_000, lastMonth);
        await SendAsync(HttpMethod.Post, $"/api/bills?month={Month(lastMonth)}", Bill(account, 7_000, due: lastMonth.AddDays(10)));

        Assert.Equal(93_000, await BalanceAsync());
    }

    [Fact]
    public async Task Outputs_without_account_count_only_with_one_primary_account()
    {
        var first = await CreateAccountAsync("First account");
        await StartingBalanceAsync(first, 100_000, MonthStart);
        await SendAsync(HttpMethod.Post, $"/api/bills?month={Month(MonthStart)}", Bill(null, 4_000, due: Today));

        var one = await GetAsync("/api/overall-balance");
        Assert.Equal(96_000, one.GetProperty("balanceCents").GetInt64());
        Assert.Equal(0, one.GetProperty("outputsWithoutAccountCount").GetInt32());

        var second = await CreateAccountAsync("Second account");
        await StartingBalanceAsync(second, 0, MonthStart);
        var two = await GetAsync("/api/overall-balance");
        Assert.Equal(100_000, two.GetProperty("balanceCents").GetInt64());
        Assert.Equal(1, two.GetProperty("outputsWithoutAccountCount").GetInt32());
    }

    [Fact]
    public async Task A_past_month_is_actual_and_a_future_month_is_expected()
    {
        var account = await CreateAccountAsync("Main account");
        var lastMonth = MonthStart.AddMonths(-1);
        var nextMonth = MonthStart.AddMonths(1);
        await StartingBalanceAsync(account, 100_000, lastMonth);
        await SendAsync(HttpMethod.Post, "/api/inputs", Input(account, 30_000, expected: lastMonth.AddDays(5), received: lastMonth.AddDays(5)));
        // Received this month: not part of last month's actual balance.
        await SendAsync(HttpMethod.Post, "/api/inputs", Input(account, 10_000, expected: MonthStart, received: MonthStart));
        await SendAsync(HttpMethod.Post, $"/api/bills?month={Month(nextMonth)}", Bill(account, 2_000, due: nextMonth.AddDays(5)));

        var past = await GetAsync($"/api/overall-balance?month={Month(lastMonth)}");
        Assert.False(past.GetProperty("isExpected").GetBoolean());
        Assert.Equal(130_000, past.GetProperty("balanceCents").GetInt64());

        var current = await GetAsync($"/api/overall-balance?month={Month(MonthStart)}");
        Assert.True(current.GetProperty("isExpected").GetBoolean());
        Assert.Equal(140_000, current.GetProperty("balanceCents").GetInt64());

        var future = await GetAsync($"/api/overall-balance?month={Month(nextMonth)}");
        Assert.True(future.GetProperty("isExpected").GetBoolean());
        Assert.Equal(138_000, future.GetProperty("balanceCents").GetInt64());
    }

    [Fact]
    public async Task A_primary_account_without_starting_balance_is_flagged()
    {
        await CreateAccountAsync("Main account");

        var overall = await GetAsync("/api/overall-balance");

        Assert.True(overall.GetProperty("missingStartingBalance").GetBoolean());
        Assert.Equal(0, overall.GetProperty("balanceCents").GetInt64());
    }

    [Fact]
    public async Task An_invalid_month_is_rejected()
    {
        var response = await Client.GetAsync(new Uri("/api/overall-balance?month=2026-13", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<long> BalanceAsync() => (await GetAsync("/api/overall-balance")).GetProperty("balanceCents").GetInt64();

    private Task<JsonElement> StartingBalanceAsync(Guid account, long balanceCents, DateOnly date) =>
        SendAsync(HttpMethod.Post, $"/api/cards/{account}/balance-entries",
            new { kind = "Starting", date = Iso(date), balanceCents, note = (string?)null });

    private static object Input(Guid account, long amountCents, DateOnly expected, DateOnly? received) =>
        new
        {
            payerName = "Employer",
            accountId = account,
            categoryId = Salary,
            amountCents,
            issueDate = Iso(expected),
            expectedDate = Iso(expected),
            receivedDate = received is { } r ? Iso(r) : null,
            description = (string?)null,
            recurrence = (object?)null,
        };

    private object Bill(Guid? account, long amountCents, DateOnly due) =>
        new
        {
            name = "Electricity bill",
            categoryId = _electricity,
            payee = (string?)null,
            amountCents,
            issueDate = Iso(new DateOnly(due.Year, due.Month, 1)),
            dueDate = Iso(due),
            paidFromId = account,
            description = (string?)null,
            paidDate = (string?)null,
        };

    private async Task<Guid> CreateAccountAsync(string name, bool isPrimary = true)
    {
        var response = await Client.PostAsJsonAsync("/api/cards", new
        {
            name,
            issuerId = VisaIssuer,
            bankId = ZaBa,
            cardNumber = (string?)null,
            iban = (string?)null,
            validFrom = (string?)null,
            validThrough = "2099-12",
            type = "Debit",
            isPrimary,
            debtLimitCents = (long?)null,
            allowsInstallments = false,
        }, Ct);
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
    }

    private static string Month(DateOnly date) => date.ToString("yyyy-MM", CultureInfo.InvariantCulture);

    private static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private async Task<JsonElement> SendAsync(HttpMethod method, string path, object? body = null)
    {
        using var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative));
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }
        var response = await Client.SendAsync(request, Ct);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync(Ct));
        return response.StatusCode == HttpStatusCode.NoContent
            ? default
            : await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    private async Task<JsonElement> GetAsync(string path)
    {
        var response = await Client.GetAsync(new Uri(path, UriKind.Relative), Ct);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    private async Task ExecuteSqlAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(Ct);
    }
}
