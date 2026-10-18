using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Npgsql;

namespace Monthwise.Api.Tests;

/// <summary>
/// The Dashboard endpoint against a real PostgreSQL (skipped without MONTHWISE_TEST_DB). Each test
/// starts from empty tables; dates are relative to today.
/// </summary>
[Collection(SharedDatabase.Name)]
public sealed class DashboardEndpointTests : IAsyncLifetime
{
    private static readonly Guid VisaIssuer = new("0199b0c0-0000-7000-8000-000000000001");
    private static readonly Guid ZaBa = new("0199b0c0-0000-7000-8000-000000000101");
    private static readonly Guid Salary = new("0199b0c0-0000-7000-8000-000000000201");
    private static readonly Guid BankLoan = new("0199c2a0-0000-7000-8000-000000000301");
    private static readonly Guid CashLoan = new("0199c2a0-0000-7000-8000-000000000312");
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.Now);
    private static readonly DateOnly MonthStart = new(Today.Year, Today.Month, 1);

    private MonthwiseApiFactory? _factory;
    private HttpClient? _client;
    private string _connectionString = string.Empty;
    private Guid _account;
    private Guid _electricity;
    private Guid _groceries;

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
        _account = await CreateAccountAsync("Main account");
        var categories = (await GetAsync("/api/expense-categories")).EnumerateArray().ToList();
        _electricity = categories.Single(c => c.GetProperty("name").GetString() == "Electricity").GetProperty("id").GetGuid();
        _groceries = categories.Single(c => c.GetProperty("name").GetString() == "Groceries").GetProperty("id").GetGuid();
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
    public async Task An_empty_month_has_zero_figures_and_no_section_cards()
    {
        var dashboard = await GetAsync("/api/dashboard");

        Assert.Equal(Month(MonthStart), dashboard.GetProperty("month").GetString());
        Assert.Equal(0, dashboard.GetProperty("inputs").GetProperty("totalCents").GetInt64());
        Assert.Equal(0, dashboard.GetProperty("outputs").GetProperty("totalCents").GetInt64());
        Assert.Equal(0, dashboard.GetProperty("monthResultCents").GetInt64());
        Assert.Equal(0, dashboard.GetProperty("stillToPay").GetProperty("count").GetInt32());
        Assert.Equal(JsonValueKind.Null, dashboard.GetProperty("nextDue").ValueKind);
        Assert.Empty(dashboard.GetProperty("comingUp").EnumerateArray());
        Assert.Equal(6, dashboard.GetProperty("lastSixMonths").GetArrayLength());
        foreach (var section in new[] { "loans", "cardPayments", "sharedCosts", "recurringBills" })
        {
            Assert.Equal(JsonValueKind.Null, dashboard.GetProperty(section).ValueKind);
        }
    }

    [Fact]
    public async Task The_figures_match_the_monthly_report_and_the_month_lists()
    {
        await SendAsync(HttpMethod.Post, "/api/inputs", Input(100_000, expected: MonthStart, received: MonthStart));
        await SendAsync(HttpMethod.Post, "/api/inputs", Input(20_000, expected: Today, received: null));
        await SendAsync(HttpMethod.Post, $"/api/bills?month={Month(MonthStart)}", Bill("Electricity bill", 5_000, due: Today, paidDate: null));
        await SendAsync(HttpMethod.Post, $"/api/bills?month={Month(MonthStart)}", Bill("Water bill", 3_000, due: Today, paidDate: MonthStart));
        await SendAsync(HttpMethod.Post, "/api/expenses", new { date = Iso(Today), amountCents = 4_000L, categoryId = _groceries, paidFromId = _account, description = (string?)null });
        await SendAsync(HttpMethod.Post, "/api/loans", Loan("Car", 30_000, start: MonthStart.AddMonths(3), count: 10));

        var dashboard = await GetAsync("/api/dashboard");
        var report = await GetAsync($"/api/cash-flow/report?month={Month(MonthStart)}");

        var inputs = dashboard.GetProperty("inputs");
        Assert.Equal(120_000, inputs.GetProperty("totalCents").GetInt64());
        Assert.Equal(100_000, inputs.GetProperty("doneCents").GetInt64());
        Assert.Equal(report.GetProperty("previousMonthTotalCents").GetInt64(), inputs.GetProperty("previousMonthTotalCents").GetInt64());

        var outputs = dashboard.GetProperty("outputs");
        Assert.Equal(12_000, outputs.GetProperty("totalCents").GetInt64());
        Assert.Equal(7_000, outputs.GetProperty("doneCents").GetInt64());
        Assert.Equal(report.GetProperty("outputs").GetProperty("totals").GetProperty("totalCents").GetInt64(), outputs.GetProperty("totalCents").GetInt64());
        Assert.Equal(report.GetProperty("monthResultCents").GetInt64(), dashboard.GetProperty("monthResultCents").GetInt64());
        Assert.Equal(108_000, dashboard.GetProperty("monthResultCents").GetInt64());

        var byKind = dashboard.GetProperty("outputsByKind");
        Assert.Equal(8_000, byKind.GetProperty("billsCents").GetInt64());
        Assert.Equal(4_000, byKind.GetProperty("expensesCents").GetInt64());
        Assert.Equal(0, byKind.GetProperty("loanPaymentsCents").GetInt64());
        Assert.Equal(0, byKind.GetProperty("cardPaymentsCents").GetInt64());

        var stillToPay = dashboard.GetProperty("stillToPay");
        Assert.Equal(5_000, stillToPay.GetProperty("totalCents").GetInt64());
        Assert.Equal(1, stillToPay.GetProperty("count").GetInt32());
        Assert.Equal(0, stillToPay.GetProperty("overdueCount").GetInt32());

        var nextDue = dashboard.GetProperty("nextDue");
        Assert.Equal("Electricity bill", nextDue.GetProperty("name").GetString());
        Assert.Equal(Iso(Today), nextDue.GetProperty("date").GetString());

        var comingUp = dashboard.GetProperty("comingUp").EnumerateArray().ToList();
        Assert.Equal(["Bill", "Input"], comingUp.Select(i => i.GetProperty("kind").GetString()));
        Assert.Equal(20_000, comingUp[1].GetProperty("amountCents").GetInt64());

        var thisMonth = dashboard.GetProperty("lastSixMonths").EnumerateArray().Last();
        Assert.Equal(Month(MonthStart), thisMonth.GetProperty("month").GetString());
        Assert.Equal(120_000, thisMonth.GetProperty("inputsCents").GetInt64());
        Assert.Equal(12_000, thisMonth.GetProperty("outputsCents").GetInt64());

        var loans = dashboard.GetProperty("loans");
        Assert.Equal(1, loans.GetProperty("activeCount").GetInt32());
        Assert.Equal(300_000, loans.GetProperty("remainingCents").GetInt64());
        Assert.False(loans.GetProperty("hasOverdue").GetBoolean());
        Assert.Equal(JsonValueKind.Null, dashboard.GetProperty("sharedCosts").ValueKind);
    }

    [Fact]
    public async Task Recurring_bills_show_how_many_are_not_issued_this_month()
    {
        await SendAsync(HttpMethod.Post, "/api/recurring-bills", new { name = "Electricity", categoryId = _electricity, payee = (string?)null, paidFromId = _account, description = (string?)null });
        await SendAsync(HttpMethod.Post, $"/api/bills/add-recurring?month={Month(MonthStart)}");

        var recurring = (await GetAsync("/api/dashboard")).GetProperty("recurringBills");

        Assert.Equal(1, recurring.GetProperty("activeCount").GetInt32());
        Assert.Equal(1, recurring.GetProperty("notIssuedThisMonth").GetInt32());
    }

    private object Input(long amountCents, DateOnly expected, DateOnly? received) =>
        new
        {
            payerName = "Employer",
            accountId = _account,
            categoryId = Salary,
            amountCents,
            issueDate = Iso(MonthStart),
            expectedDate = Iso(expected),
            receivedDate = received is { } r ? Iso(r) : null,
            description = (string?)null,
            recurrence = (object?)null,
        };

    private object Bill(string name, long amountCents, DateOnly due, DateOnly? paidDate) =>
        new
        {
            name,
            categoryId = _electricity,
            payee = (string?)null,
            amountCents,
            issueDate = Iso(MonthStart),
            dueDate = Iso(due),
            paidFromId = _account,
            description = (string?)null,
            paidDate = paidDate is { } p ? Iso(p) : null,
        };

    private object Loan(string name, long monthlyPaymentCents, DateOnly start, int count) =>
        new
        {
            name,
            categoryId = BankLoan,
            subcategoryId = CashLoan,
            lenderId = ZaBa,
            paymentMethod = "StandingOrder",
            accountId = _account,
            startMonth = Month(start),
            paymentDay = 15,
            monthlyPaymentCents,
            paymentCount = count,
            endMonth = (string?)null,
            residualValueCents = (long?)null,
            description = (string?)null,
        };

    private async Task<Guid> CreateAccountAsync(string name)
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
            isPrimary = true,
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
