using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Npgsql;

namespace Monthwise.Api.Tests;

/// <summary>
/// Input cash flow endpoints against a real PostgreSQL (skipped without MONTHWISE_TEST_DB). Each test
/// starts from empty cash flow and card tables; dates are relative to today.
/// </summary>
[Collection(SharedDatabase.Name)]
public sealed class CashFlowEndpointTests : IAsyncLifetime
{
    /// <summary>Empties the cash flow and card tables (cash flow first, because of the foreign keys).</summary>
    public const string DeleteAllSql =
        "DELETE FROM skipped_occurrence; DELETE FROM cash_flow_input; DELETE FROM recurring_input; DELETE FROM balance_entry; "
        + "DELETE FROM payer; DELETE FROM input_category WHERE NOT is_predefined; UPDATE input_category SET hidden_on = NULL; "
        + "DELETE FROM card;";

    private static readonly Guid VisaIssuer = new("0199b0c0-0000-7000-8000-000000000001");
    private static readonly Guid ZaBa = new("0199b0c0-0000-7000-8000-000000000101");
    private static readonly Guid Salary = new("0199b0c0-0000-7000-8000-000000000201");
    private static readonly Guid OtherIncome = new("0199b0c0-0000-7000-8000-000000000204");
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.Now);
    private static readonly DateOnly MonthStart = new(Today.Year, Today.Month, 1);

    private MonthwiseApiFactory? _factory;
    private HttpClient? _client;
    private string _connectionString = string.Empty;
    private Guid _account;

    private HttpClient Client => _client!;
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _connectionString = MonthwiseApiFactory.TestDatabaseConnectionString ?? string.Empty;
        Assert.SkipWhen(_connectionString.Length == 0, "MONTHWISE_TEST_DB is not set; no test database available.");

        _factory = new MonthwiseApiFactory(_connectionString, migrateOnStartup: true);
        _client = _factory.CreateClient();
        await Client.GetAsync(new Uri("/api/health", UriKind.Relative), Ct);
        await ExecuteSqlAsync(DeleteAllSql);
        _account = await CreateAccountAsync("Main account", isPrimary: true);
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
    public async Task Add_tick_untick_and_delete_an_input_with_month_totals()
    {
        var created = await SendAsync(HttpMethod.Post, "/api/inputs", Input("Employer d.o.o.", 150_000));
        var id = created.GetProperty("id").GetGuid();
        Assert.Equal("Employer d.o.o.", created.GetProperty("payer").GetProperty("name").GetString());
        Assert.Equal("Main account", created.GetProperty("account").GetProperty("name").GetString());
        Assert.False(created.GetProperty("isReceived").GetBoolean());

        var month = await MonthAsync(MonthStart);
        Assert.Single(month.GetProperty("inputs").EnumerateArray());
        AssertTotals(month.GetProperty("totals"), expected: 150_000, received: 0);

        var received = await SendAsync(HttpMethod.Post, $"/api/inputs/{id}/received");
        Assert.Equal(Iso(Today), received.GetProperty("receivedDate").GetString());
        Assert.Equal(Iso(MonthStart), received.GetProperty("issueDate").GetString());
        AssertTotals((await MonthAsync(MonthStart)).GetProperty("totals"), expected: 0, received: 150_000);

        var unticked = await SendAsync(HttpMethod.Delete, $"/api/inputs/{id}/received");
        Assert.Equal(JsonValueKind.Null, unticked.GetProperty("receivedDate").ValueKind);

        var deleted = await Client.DeleteAsync(new Uri($"/api/inputs/{id}", UriKind.Relative), Ct);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Empty((await MonthAsync(MonthStart)).GetProperty("inputs").EnumerateArray());
    }

    [Fact]
    public async Task Invalid_inputs_return_field_errors()
    {
        var response = await Client.PostAsJsonAsync(
            "/api/inputs",
            Input("", 0, issue: Today, expected: Today.AddDays(-1), account: Guid.Empty),
            Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("errors");
        foreach (var field in new[] { "payerName", "amountCents", "expectedDate", "accountId" })
        {
            Assert.True(errors.TryGetProperty(field, out _), field);
        }
        // Nothing was saved, not even the payer.
        Assert.Empty((await GetAsync("/api/payers")).EnumerateArray());
    }

    [Fact]
    public async Task A_blocked_account_cannot_receive_new_inputs()
    {
        await SendAsync(HttpMethod.Post, $"/api/cards/{_account}/block", new { reason = (string?)null });

        var response = await Client.PostAsJsonAsync("/api/inputs", Input("Employer", 1_000), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("accountId", await response.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Payers_are_saved_automatically_reused_renamed_and_removed()
    {
        var first = await SendAsync(HttpMethod.Post, "/api/inputs", Input("Employer", 1_000));
        var second = await SendAsync(HttpMethod.Post, "/api/inputs", Input("  EMPLOYER ", 2_000));
        var payerId = first.GetProperty("payer").GetProperty("id").GetGuid();
        Assert.Equal(payerId, second.GetProperty("payer").GetProperty("id").GetGuid());

        Assert.Single((await GetAsync("/api/payers?search=ploy")).EnumerateArray());
        Assert.Empty((await GetAsync("/api/payers?search=xyz")).EnumerateArray());

        await SendAsync(HttpMethod.Put, $"/api/payers/{payerId}", new { name = "Employer d.o.o." });
        var removed = await Client.DeleteAsync(new Uri($"/api/payers/{payerId}", UriKind.Relative), Ct);
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Empty((await GetAsync("/api/payers")).EnumerateArray());

        // Inputs keep the name, and typing it again brings the payer back.
        var inputs = (await MonthAsync(MonthStart)).GetProperty("inputs").EnumerateArray().ToList();
        Assert.All(inputs, i => Assert.Equal("Employer d.o.o.", i.GetProperty("payer").GetProperty("name").GetString()));
        await SendAsync(HttpMethod.Post, "/api/inputs", Input("employer d.o.o.", 3_000));
        Assert.Single((await GetAsync("/api/payers")).EnumerateArray());
    }

    [Fact]
    public async Task Recurring_inputs_appear_every_month_and_occurrences_change_alone()
    {
        var first = await SendAsync(
            HttpMethod.Post, "/api/inputs",
            Input("Employer", 200_000, recurrence: new { frequency = "Monthly", endDate = (string?)null }));
        var recurringId = first.GetProperty("recurringInputId").GetGuid();
        Assert.Equal(JsonValueKind.Null, first.GetProperty("id").ValueKind);
        Assert.Equal("Monthly", first.GetProperty("frequency").GetString());

        var next = MonthStart.AddMonths(1);
        var later = MonthStart.AddMonths(2);
        var nextEntry = Assert.Single((await MonthAsync(next)).GetProperty("inputs").EnumerateArray());
        Assert.Equal(Iso(next.AddDays(9)), nextEntry.GetProperty("expectedDate").GetString());

        // Change next month's occurrence only.
        var occurrence = $"/api/recurring-inputs/{recurringId}/occurrences/{Iso(next.AddDays(9))}";
        var changed = await SendAsync(HttpMethod.Put, occurrence, Input("Employer", 210_000, issue: next, expected: next.AddDays(9)));
        Assert.NotEqual(JsonValueKind.Null, changed.GetProperty("id").ValueKind);
        AssertTotals((await MonthAsync(next)).GetProperty("totals"), expected: 210_000, received: 0);
        AssertTotals((await MonthAsync(later)).GetProperty("totals"), expected: 200_000, received: 0);

        // Delete the occurrence of the month after: it is skipped.
        var deleted = await Client.DeleteAsync(
            new Uri($"/api/recurring-inputs/{recurringId}/occurrences/{Iso(later.AddDays(9))}", UriKind.Relative), Ct);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Empty((await MonthAsync(later)).GetProperty("inputs").EnumerateArray());

        // Changing the template moves the not-received future occurrences to the new amount.
        await SendAsync(HttpMethod.Put, $"/api/recurring-inputs/{recurringId}", Recurring("Employer", 220_000));
        AssertTotals((await MonthAsync(next)).GetProperty("totals"), expected: 220_000, received: 0);
        AssertTotals((await MonthAsync(MonthStart.AddMonths(3))).GetProperty("totals"), expected: 220_000, received: 0);

        // Stopping ends it today.
        var stopped = await SendAsync(HttpMethod.Post, $"/api/recurring-inputs/{recurringId}/stop");
        Assert.True(stopped.GetProperty("isStopped").GetBoolean());
        Assert.Empty((await MonthAsync(next)).GetProperty("inputs").EnumerateArray());
    }

    [Fact]
    public async Task Receiving_a_calculated_occurrence_stores_it()
    {
        var first = await SendAsync(
            HttpMethod.Post, "/api/inputs",
            Input("Employer", 200_000, issue: Today, expected: Today, recurrence: new { frequency = "Weekly", endDate = (string?)null }));
        var recurringId = first.GetProperty("recurringInputId").GetGuid();

        var received = await SendAsync(HttpMethod.Post, $"/api/recurring-inputs/{recurringId}/occurrences/{Iso(Today)}/received");

        Assert.True(received.GetProperty("isReceived").GetBoolean());
        Assert.Equal(recurringId, received.GetProperty("recurringInputId").GetGuid());
        var notScheduled = await Client.PostAsync(
            new Uri($"/api/recurring-inputs/{recurringId}/occurrences/{Iso(Today.AddDays(1))}/received", UriKind.Relative), null, Ct);
        Assert.Equal(HttpStatusCode.NotFound, notScheduled.StatusCode);
    }

    [Fact]
    public async Task Categories_are_seeded_and_used_ones_can_only_be_hidden()
    {
        var categories = (await GetAsync("/api/input-categories")).EnumerateArray().Select(c => c.GetProperty("name").GetString()).ToList();
        Assert.Equal(["Agreed contributions", "Other income", "Salary", "Salary add-ons"], categories);

        var bonus = await SendAsync(HttpMethod.Post, "/api/input-categories", new { name = "Bonus" });
        var duplicate = await Client.PostAsJsonAsync("/api/input-categories", new { name = "bonus" }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);

        var bonusId = bonus.GetProperty("id").GetGuid();
        await SendAsync(HttpMethod.Post, "/api/inputs", Input("Employer", 1_000, category: bonusId));
        var delete = await Client.DeleteAsync(new Uri($"/api/input-categories/{bonusId}", UriKind.Relative), Ct);
        Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);

        await SendAsync(HttpMethod.Post, $"/api/input-categories/{bonusId}/hide");
        Assert.Equal(4, (await GetAsync("/api/input-categories")).GetArrayLength());
        Assert.Equal(5, (await GetAsync("/api/input-categories?includeHidden=true")).GetArrayLength());
        var hiddenUse = await Client.PostAsJsonAsync("/api/inputs", Input("Employer", 1_000, category: bonusId), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, hiddenUse.StatusCode);
    }

    [Fact]
    public async Task Balances_follow_starting_balance_corrections_and_inputs()
    {
        await SendAsync(HttpMethod.Post, $"/api/cards/{_account}/balance-entries",
            new { kind = "Starting", date = Iso(Today.AddDays(-10)), balanceCents = 10_000, note = (string?)null });
        await SendAsync(HttpMethod.Post, "/api/inputs",
            Input("Employer", 5_000, issue: Today.AddDays(-5), expected: Today.AddDays(-5), received: Today.AddDays(-5)));
        await SendAsync(HttpMethod.Post, "/api/inputs",
            Input("Employer", 2_000, issue: Today, expected: new DateOnly(Today.Year, Today.Month, DateTime.DaysInMonth(Today.Year, Today.Month))));

        var dashboard = await GetAsync("/api/cards/dashboard");
        var balance = Assert.Single(dashboard.GetProperty("balances").EnumerateArray());
        Assert.Equal(15_000, balance.GetProperty("currentBalanceCents").GetInt64());
        Assert.Equal(17_000, balance.GetProperty("expectedBalanceCents").GetInt64());
        Assert.True(balance.GetProperty("hasStartingBalance").GetBoolean());

        var correction = await SendAsync(HttpMethod.Post, $"/api/cards/{_account}/balance-entries",
            new { kind = "Correction", date = Iso(Today.AddDays(-1)), balanceCents = 14_000, note = "Bank fee" });
        Assert.Equal(-1_000, correction.GetProperty("differenceCents").GetInt64());

        var details = await GetAsync($"/api/cards/{_account}/balance");
        Assert.Equal(14_000, details.GetProperty("balance").GetProperty("currentBalanceCents").GetInt64());
        Assert.Equal(2, details.GetProperty("entries").GetArrayLength());

        var secondStart = await Client.PostAsJsonAsync($"/api/cards/{_account}/balance-entries",
            new { kind = "Starting", date = Iso(Today), balanceCents = 0, note = (string?)null }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, secondStart.StatusCode);
    }

    [Fact]
    public async Task Chart_and_report_sum_inputs_by_day_month_and_group()
    {
        var previousMonth = MonthStart.AddMonths(-1);
        await SendAsync(HttpMethod.Post, "/api/inputs", Input("Employer", 100_000, issue: previousMonth, expected: previousMonth.AddDays(9)));
        await SendAsync(HttpMethod.Post, "/api/inputs", Input("Employer", 150_000, received: MonthStart.AddDays(2)));
        await SendAsync(HttpMethod.Post, "/api/inputs", Input("Friend", 20_000, category: OtherIncome));

        var chart = await GetAsync($"/api/cash-flow/chart?from={Iso(MonthStart)}&to={Iso(MonthStart.AddMonths(1).AddDays(-1))}&granularity=Day");
        var buckets = chart.GetProperty("buckets").EnumerateArray().ToList();
        Assert.Equal(DateTime.DaysInMonth(Today.Year, Today.Month), buckets.Count);
        Assert.Equal(150_000, buckets[2].GetProperty("receivedCents").GetInt64());
        Assert.Equal(170_000 - 150_000, buckets[9].GetProperty("expectedCents").GetInt64());

        var months = await GetAsync($"/api/cash-flow/chart?from={Iso(previousMonth)}&to={Iso(MonthStart.AddMonths(1).AddDays(-1))}&granularity=Month&payerId={(await PayerIdAsync("Employer"))}");
        var monthBuckets = months.GetProperty("buckets").EnumerateArray().ToList();
        Assert.Equal(2, monthBuckets.Count);
        Assert.Equal(100_000, monthBuckets[0].GetProperty("expectedCents").GetInt64());
        Assert.Equal(150_000, monthBuckets[1].GetProperty("receivedCents").GetInt64());
        Assert.Equal(0, monthBuckets[1].GetProperty("expectedCents").GetInt64());

        var report = await GetAsync($"/api/cash-flow/report?month={MonthStart:yyyy-MM}");
        AssertTotals(report.GetProperty("totals"), expected: 20_000, received: 150_000);
        Assert.Equal(2, report.GetProperty("byCategory").GetArrayLength());
        Assert.Equal("Salary", report.GetProperty("byCategory")[0].GetProperty("name").GetString());
        Assert.Equal(2, report.GetProperty("byPayer").GetArrayLength());
        Assert.Single(report.GetProperty("byAccount").EnumerateArray());
        Assert.Equal(100_000, report.GetProperty("previousMonthTotalCents").GetInt64());
        Assert.Equal(70_000, report.GetProperty("differenceCents").GetInt64());

        var invalid = await Client.GetAsync(new Uri("/api/cash-flow/report?month=2026-13", UriKind.Relative), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task The_database_rejects_a_zero_amount_and_a_received_date_before_the_issue_date()
    {
        var id = (await SendAsync(HttpMethod.Post, "/api/inputs", Input("Employer", 1_000))).GetProperty("id").GetGuid();

        var zero = await Assert.ThrowsAsync<PostgresException>(() =>
            ExecuteSqlAsync($"UPDATE cash_flow_input SET amount_cents = 0 WHERE id = '{id}';"));
        var early = await Assert.ThrowsAsync<PostgresException>(() =>
            ExecuteSqlAsync($"UPDATE cash_flow_input SET received_date = issue_date - 1 WHERE id = '{id}';"));

        Assert.Equal(PostgresErrorCodes.CheckViolation, zero.SqlState);
        Assert.Equal(PostgresErrorCodes.CheckViolation, early.SqlState);
    }

    private object Input(
        string payerName,
        long amountCents,
        DateOnly? issue = null,
        DateOnly? expected = null,
        DateOnly? received = null,
        Guid? account = null,
        Guid? category = null,
        object? recurrence = null) =>
        new
        {
            payerName,
            accountId = account ?? _account,
            categoryId = category ?? Salary,
            amountCents,
            issueDate = Iso(issue ?? MonthStart),
            expectedDate = Iso(expected ?? MonthStart.AddDays(9)),
            receivedDate = received is { } r ? Iso(r) : null,
            description = (string?)null,
            recurrence,
        };

    private object Recurring(string payerName, long amountCents) =>
        new
        {
            payerName,
            accountId = _account,
            categoryId = Salary,
            amountCents,
            issueDate = Iso(MonthStart),
            expectedDate = Iso(MonthStart.AddDays(9)),
            description = (string?)null,
            frequency = "Monthly",
            endDate = (string?)null,
        };

    private async Task<Guid> CreateAccountAsync(string name, bool isPrimary)
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

    private async Task<Guid> PayerIdAsync(string name) =>
        (await GetAsync("/api/payers")).EnumerateArray().Single(p => p.GetProperty("name").GetString() == name).GetProperty("id").GetGuid();

    private Task<JsonElement> MonthAsync(DateOnly month) => GetAsync($"/api/inputs?month={month:yyyy-MM}");

    private static void AssertTotals(JsonElement totals, long expected, long received)
    {
        Assert.Equal(expected, totals.GetProperty("expectedCents").GetInt64());
        Assert.Equal(received, totals.GetProperty("receivedCents").GetInt64());
        Assert.Equal(expected + received, totals.GetProperty("totalCents").GetInt64());
    }

    private static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private async Task<JsonElement> SendAsync(HttpMethod method, string path, object? body = null)
    {
        using var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative));
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }
        var response = await Client.SendAsync(request, Ct);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
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
