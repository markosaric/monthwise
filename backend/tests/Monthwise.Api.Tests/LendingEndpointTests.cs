using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Npgsql;

namespace Monthwise.Api.Tests;

/// <summary>
/// Lending endpoints and lending in balances, the month list, the chart, the report and the Dashboard,
/// against a real PostgreSQL (see TestDatabase; docs/feature-requirements/money-lending.md). Each test
/// starts from empty tables with a primary account, a second account and a credit card.
/// </summary>
[Collection(SharedDatabase.Name)]
public sealed class LendingEndpointTests : IAsyncLifetime
{
    private static readonly Guid VisaIssuer = new("0199b0c0-0000-7000-8000-000000000001");
    private static readonly Guid ZaBa = new("0199b0c0-0000-7000-8000-000000000101");
    private static readonly Guid LendingGroup = new("0199b0c0-0000-7000-8000-00000000a001");
    private static readonly DateOnly Today = HouseholdToday.Value;
    private static readonly DateOnly MonthStart = new(Today.Year, Today.Month, 1);
    private static readonly DateOnly MonthEnd = MonthStart.AddMonths(1).AddDays(-1);

    private MonthwiseApiFactory? _factory;
    private HttpClient? _client;
    private string _connectionString = string.Empty;
    private Guid _account;
    private Guid _savings;
    private Guid _creditCard;

    private HttpClient Client => _client!;
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _connectionString = TestDatabase.ConnectionString;
        _factory = new MonthwiseApiFactory(_connectionString, migrateOnStartup: true);
        _client = _factory.CreateClient();
        await Client.GetAsync(new Uri("/api/health", UriKind.Relative), Ct);
        await ExecuteSqlAsync(CashFlowEndpointTests.DeleteAllSql);
        _account = await CreateCardAsync("Main account", "Debit", isPrimary: true);
        _savings = await CreateCardAsync("Savings", "Debit");
        _creditCard = await CreateCardAsync("Family Visa", "Credit", payingAccountId: _account);
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
    public async Task A_lending_is_added_with_a_borrower_from_the_payers_list_and_listed_as_open()
    {
        var lending = await SendAsync(HttpMethod.Post, "/api/lendings", Lending("Ivo", 50_000, Today, MonthEnd));

        Assert.Equal("Ivo", lending.GetProperty("borrower").GetProperty("name").GetString());
        Assert.Equal("Main account", lending.GetProperty("fromAccount").GetProperty("name").GetString());
        Assert.Equal(50_000, lending.GetProperty("outstandingCents").GetInt64());
        Assert.Equal("Open", lending.GetProperty("state").GetString());

        // The borrower is a payer (FR-6), and typing it again in another case reuses it.
        var payers = (await GetAsync("/api/payers")).EnumerateArray().ToList();
        Assert.Equal("Ivo", Assert.Single(payers).GetProperty("name").GetString());
        await SendAsync(HttpMethod.Post, "/api/lendings", Lending("IVO", 10_000, Today, MonthEnd));
        Assert.Single((await GetAsync("/api/payers")).EnumerateArray());

        var list = await GetAsync("/api/lendings");
        Assert.Equal(2, list.GetProperty("lendings").GetArrayLength());
        Assert.Equal(60_000, list.GetProperty("outstandingCents").GetInt64());
        Assert.Equal(0, list.GetProperty("overdueCents").GetInt64());

        var borrower = Assert.Single((await GetAsync("/api/lendings/borrowers")).EnumerateArray());
        Assert.Equal(60_000, borrower.GetProperty("lentCents").GetInt64());
        Assert.Equal(60_000, borrower.GetProperty("outstandingCents").GetInt64());

        // Renaming the payer in Settings renames the borrower of its lendings.
        var payerId = payers[0].GetProperty("id").GetGuid();
        await SendAsync(HttpMethod.Put, $"/api/payers/{payerId}", new { name = "Ivo Ivić" });
        Assert.All((await GetAsync("/api/lendings")).GetProperty("lendings").EnumerateArray(),
            l => Assert.Equal("Ivo Ivić", l.GetProperty("borrower").GetProperty("name").GetString()));
    }

    [Fact]
    public async Task Saving_is_refused_with_field_errors_for_missing_values_wrong_dates_and_credit_or_blocked_accounts()
    {
        var empty = await Client.PostAsJsonAsync("/api/lendings", new
        {
            borrowerName = " ",
            amountCents = 0L,
            lendDate = Iso(Today),
            fromAccountId = Guid.Empty,
            expectedReturnDate = Iso(Today.AddDays(-1)),
            description = (string?)null,
        }, Ct);
        var errors = await ErrorsAsync(empty);
        Assert.Equal(["amountCents", "borrowerName", "expectedReturnDate", "fromAccountId"], errors.Keys.Order());

        var creditCard = await Client.PostAsJsonAsync("/api/lendings", Lending("Ivo", 1_000, Today, Today, _creditCard), Ct);
        Assert.Contains("fromAccountId", (await ErrorsAsync(creditCard)).Keys);

        await SendAsync(HttpMethod.Post, $"/api/cards/{_savings}/block", new { reason = (string?)null });
        var blocked = await Client.PostAsJsonAsync("/api/lendings", Lending("Ivo", 1_000, Today, Today, _savings), Ct);
        Assert.Contains("fromAccountId", (await ErrorsAsync(blocked)).Keys);
        Assert.Empty((await GetAsync("/api/payers")).EnumerateArray());
    }

    [Fact]
    public async Task Repayments_lower_the_outstanding_amount_until_the_lending_is_returned()
    {
        var id = (await SendAsync(HttpMethod.Post, "/api/lendings", Lending("Ana", 50_000, Today, MonthEnd))).GetProperty("id").GetGuid();

        var tooMuch = await Client.PostAsJsonAsync($"/api/lendings/{id}/repayments", Repayment(50_001, Today), Ct);
        Assert.Equal(["amountCents"], (await ErrorsAsync(tooMuch)).Keys);
        var tooEarly = await Client.PostAsJsonAsync($"/api/lendings/{id}/repayments", Repayment(100, Today.AddDays(-1)), Ct);
        Assert.Equal(["receivedDate"], (await ErrorsAsync(tooEarly)).Keys);

        var part = await SendAsync(HttpMethod.Post, $"/api/lendings/{id}/repayments", Repayment(20_000, Today, _savings));
        Assert.Equal(30_000, part.GetProperty("outstandingCents").GetInt64());
        Assert.Equal("Savings", part.GetProperty("repayments")[0].GetProperty("toAccount").GetProperty("name").GetString());

        // The amount cannot go below what was returned (FR-3).
        var below = await Client.PutAsJsonAsync($"/api/lendings/{id}", Lending("Ana", 19_999, Today, MonthEnd), Ct);
        Assert.Equal(["amountCents"], (await ErrorsAsync(below)).Keys);

        var rest = await SendAsync(HttpMethod.Post, $"/api/lendings/{id}/repayments", Repayment(30_000, Today));
        Assert.Equal("Returned", rest.GetProperty("state").GetString());
        Assert.Empty((await GetAsync("/api/lendings")).GetProperty("lendings").EnumerateArray());
        Assert.Single((await GetAsync("/api/lendings?state=Returned")).GetProperty("lendings").EnumerateArray());

        var repaymentId = rest.GetProperty("repayments")[1].GetProperty("id").GetGuid();
        var reopened = await SendAsync(HttpMethod.Delete, $"/api/lendings/{id}/repayments/{repaymentId}");
        Assert.Equal("Open", reopened.GetProperty("state").GetString());
        Assert.Equal(30_000, reopened.GetProperty("outstandingCents").GetInt64());

        // Deleting the lending deletes its repayments.
        var delete = await Client.DeleteAsync(new Uri($"/api/lendings/{id}", UriKind.Relative), Ct);
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync(new Uri($"/api/lendings/{id}", UriKind.Relative), Ct)).StatusCode);
    }

    [Fact]
    public async Task Lent_money_leaves_the_account_and_repayments_and_the_expected_return_count_in_the_balances()
    {
        var id = (await SendAsync(HttpMethod.Post, "/api/lendings", Lending("Ana", 50_000, Today, MonthEnd))).GetProperty("id").GetGuid();
        await SendAsync(HttpMethod.Post, $"/api/lendings/{id}/repayments", Repayment(20_000, Today, _savings));

        var main = (await GetAsync($"/api/cards/{_account}/balance")).GetProperty("balance");
        var savings = (await GetAsync($"/api/cards/{_savings}/balance")).GetProperty("balance");

        Assert.Equal(-50_000, main.GetProperty("currentBalanceCents").GetInt64());
        // The outstanding 300 EUR is expected back on the lending's account this month (FR-8).
        Assert.Equal(-20_000, main.GetProperty("expectedBalanceCents").GetInt64());
        Assert.Equal(20_000, savings.GetProperty("currentBalanceCents").GetInt64());
        Assert.Equal(20_000, savings.GetProperty("expectedBalanceCents").GetInt64());
    }

    [Fact]
    public async Task An_overdue_lending_is_highlighted_and_still_expected_in_this_months_balance()
    {
        var lendDate = Today.AddDays(-10);
        await SendAsync(HttpMethod.Post, "/api/lendings", Lending("Ana", 40_000, lendDate, Today.AddDays(-1)));

        var list = await GetAsync("/api/lendings");
        Assert.Equal("Overdue", list.GetProperty("lendings")[0].GetProperty("state").GetString());
        Assert.Equal(40_000, list.GetProperty("overdueCents").GetInt64());

        var balance = (await GetAsync($"/api/cards/{_account}/balance")).GetProperty("balance");
        Assert.Equal(-40_000, balance.GetProperty("currentBalanceCents").GetInt64());
        Assert.Equal(0, balance.GetProperty("expectedBalanceCents").GetInt64());

        var dashboard = (await GetAsync("/api/dashboard")).GetProperty("lending");
        Assert.Equal(40_000, dashboard.GetProperty("outstandingCents").GetInt64());
        Assert.Equal(40_000, dashboard.GetProperty("overdueCents").GetInt64());
        Assert.True(dashboard.GetProperty("hasOverdue").GetBoolean());
    }

    [Fact]
    public async Task The_month_list_report_chart_and_dashboard_show_lending_as_outputs_and_inputs()
    {
        var id = (await SendAsync(HttpMethod.Post, "/api/lendings", Lending("Ana", 50_000, Today, MonthEnd))).GetProperty("id").GetGuid();
        await SendAsync(HttpMethod.Post, $"/api/lendings/{id}/repayments", Repayment(20_000, Today));
        var month = Month(Today);

        var monthList = await GetAsync($"/api/lendings/month?month={month}");
        Assert.Equal(50_000, monthList.GetProperty("lentCents").GetInt64());
        Assert.Equal(20_000, monthList.GetProperty("returnedCents").GetInt64());
        Assert.Equal(30_000, monthList.GetProperty("expectedBackCents").GetInt64());

        var report = await GetAsync($"/api/cash-flow/report?month={month}");
        Assert.Equal(50_000, report.GetProperty("totals").GetProperty("totalCents").GetInt64());
        Assert.Equal(20_000, report.GetProperty("totals").GetProperty("receivedCents").GetInt64());
        Assert.Equal(50_000, report.GetProperty("outputs").GetProperty("totals").GetProperty("paidCents").GetInt64());
        Assert.Equal(0, report.GetProperty("monthResultCents").GetInt64());
        var inputGroup = Assert.Single(report.GetProperty("byCategory").EnumerateArray());
        Assert.Equal(LendingGroup, inputGroup.GetProperty("id").GetGuid());
        Assert.Equal("Lending", inputGroup.GetProperty("name").GetString());
        Assert.Equal("Ana", Assert.Single(report.GetProperty("byPayer").EnumerateArray()).GetProperty("name").GetString());
        Assert.Equal(LendingGroup, Assert.Single(report.GetProperty("outputs").GetProperty("byCategory").EnumerateArray()).GetProperty("id").GetGuid());
        var borrower = Assert.Single(report.GetProperty("lending").GetProperty("byBorrower").EnumerateArray());
        Assert.Equal(30_000, borrower.GetProperty("expectedBackCents").GetInt64());

        var payerId = Assert.Single((await GetAsync("/api/payers")).EnumerateArray()).GetProperty("id").GetGuid();
        var range = $"from={Iso(MonthStart)}&to={Iso(MonthEnd)}&granularity=Month";
        var chart = (await GetAsync($"/api/cash-flow/chart?{range}&borrowerId={payerId}")).GetProperty("buckets")[0];
        Assert.Equal(20_000, chart.GetProperty("receivedCents").GetInt64());
        Assert.Equal(30_000, chart.GetProperty("expectedCents").GetInt64());
        Assert.Equal(50_000, chart.GetProperty("outputPaidCents").GetInt64());
        var otherBorrower = (await GetAsync($"/api/cash-flow/chart?{range}&borrowerId={Guid.NewGuid()}")).GetProperty("buckets")[0];
        Assert.Equal(0, otherBorrower.GetProperty("outputPaidCents").GetInt64());

        var dashboard = await GetAsync("/api/dashboard");
        Assert.Equal(50_000, dashboard.GetProperty("outputsByKind").GetProperty("lendingCents").GetInt64());
        Assert.Equal(30_000, dashboard.GetProperty("lending").GetProperty("outstandingCents").GetInt64());
        Assert.Equal(1, dashboard.GetProperty("lending").GetProperty("openCount").GetInt32());
    }

    [Fact]
    public async Task Without_lending_the_dashboard_card_and_report_block_are_empty()
    {
        Assert.Equal(JsonValueKind.Null, (await GetAsync("/api/dashboard")).GetProperty("lending").ValueKind);
        Assert.Equal(JsonValueKind.Null, (await GetAsync($"/api/cash-flow/report?month={Month(Today)}")).GetProperty("lending").ValueKind);
    }

    [Fact]
    public async Task Errors_are_in_croatian_when_the_request_is_in_croatian()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/api/lendings", UriKind.Relative))
        {
            Content = JsonContent.Create(Lending("", 1_000, Today, Today)),
        };
        request.Headers.AcceptLanguage.ParseAdd("hr");
        var response = await Client.SendAsync(request, Ct);
        Assert.Equal("Dužnik je obavezan.", (await ErrorsAsync(response))["borrowerName"][0]);
    }

    private object Lending(string borrower, long cents, DateOnly lendDate, DateOnly expectedReturnDate, Guid? from = null) =>
        new
        {
            borrowerName = borrower,
            amountCents = cents,
            lendDate = Iso(lendDate),
            fromAccountId = from ?? _account,
            expectedReturnDate = Iso(expectedReturnDate),
            description = (string?)null,
        };

    private object Repayment(long cents, DateOnly receivedDate, Guid? to = null) =>
        new { amountCents = cents, toAccountId = to ?? _account, receivedDate = Iso(receivedDate), note = (string?)null };

    private static async Task<Dictionary<string, string[]>> ErrorsAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        return body.GetProperty("errors").EnumerateObject()
            .ToDictionary(p => p.Name, p => p.Value.EnumerateArray().Select(e => e.GetString()!).ToArray());
    }

    private async Task<Guid> CreateCardAsync(string name, string type, bool isPrimary = false, Guid? payingAccountId = null)
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
            type,
            isPrimary,
            debtLimitCents = (long?)null,
            allowsInstallments = false,
            statementDay = payingAccountId is null ? (int?)null : 5,
            paymentDay = payingAccountId is null ? (int?)null : 20,
            payingAccountId,
            paysFromAccountDirectly = false,
            maxInstallments = payingAccountId is null ? (int?)null : 36,
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
        await ExpectedStatus.AssertAsync(response, method, path, Ct);
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
