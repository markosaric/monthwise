using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Npgsql;

namespace Monthwise.Api.Tests;

/// <summary>
/// The supported range of months and dates, January 2000 to December 2100
/// (docs/feature-requirements/month-range.md), against a real PostgreSQL (see TestDatabase).
/// Each test starts from empty tables with a recurring input, a recurring bill, a loan, an
/// installment purchase and a revolving card in the data.
/// </summary>
[Collection(SharedDatabase.Name)]
public sealed class MonthRangeEndpointTests : IAsyncLifetime
{
    private const string MonthMessage = "Enter a month from 2000-01 to 2100-12.";
    private const string DateMessage = "Enter a date from 2000-01-01 to 2100-12-31.";

    private static readonly Guid VisaIssuer = new("0199b0c0-0000-7000-8000-000000000001");
    private static readonly Guid ZaBa = new("0199b0c0-0000-7000-8000-000000000101");
    private static readonly Guid Salary = new("0199b0c0-0000-7000-8000-000000000201");
    private static readonly Guid BankLoan = new("0199c2a0-0000-7000-8000-000000000301");
    private static readonly Guid CashLoan = new("0199c2a0-0000-7000-8000-000000000312");
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.Now);
    private static readonly DateOnly MonthStart = new(Today.Year, Today.Month, 1);

    /// <summary>Every endpoint that reads a month from the query string (FR-1).</summary>
    public static readonly TheoryData<string> MonthQueries = new()
    {
        "/api/inputs?month={0}",
        "/api/cash-flow/report?month={0}",
        "/api/overall-balance?month={0}",
        "/api/bills?month={0}",
        "/api/expenses?month={0}",
        "/api/loan-payments?month={0}",
        "/api/card-payments?month={0}",
        "/api/revolving-bills?month={0}",
        "/api/shared-costs?month={0}",
    };

    private MonthwiseApiFactory? _factory;
    private HttpClient? _client;
    private string _connectionString = string.Empty;
    private Guid _account;
    private Guid _card;
    private Guid _revolving;
    private Guid _loan;
    private Guid _electricity;

    private HttpClient Client => _client!;
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _connectionString = TestDatabase.ConnectionString;

        _factory = new MonthwiseApiFactory(_connectionString, migrateOnStartup: true);
        _client = _factory.CreateClient();
        await Client.GetAsync(new Uri("/api/health", UriKind.Relative), Ct);
        await ExecuteSqlAsync(CashFlowEndpointTests.DeleteAllSql);
        var categories = (await GetAsync("/api/expense-categories")).EnumerateArray().ToList();
        _electricity = categories.Single(c => c.GetProperty("name").GetString() == "Electricity").GetProperty("id").GetGuid();

        _account = await CreateCardAsync(Card("Main account", "Debit", isPrimary: true));
        await SendAsync(HttpMethod.Post, $"/api/cards/{_account}/balance-entries",
            new { kind = "Starting", date = "2000-01-01", balanceCents = 100_000, note = (string?)null });
        _card = await CreateCardAsync(Card("Family Visa", "Credit", payingAccountId: _account));
        _revolving = await CreateCardAsync(Revolving("Revolving Visa"));

        // A monthly input from the first day of the range with no end: it runs past December 2100.
        await SendAsync(HttpMethod.Post, "/api/inputs", Input(new DateOnly(2000, 1, 1), recurrence: new { frequency = "Monthly", endDate = (string?)null }));
        await SendAsync(HttpMethod.Post, "/api/recurring-bills",
            new { name = "Electricity", categoryId = _electricity, payee = "HEP", paidFromId = _account, description = (string?)null, amountCents = 5_000L, issueDay = 1, dueDay = 15 });
        // A loan starting in the last month of the range: its payments run past it.
        var loan = await SendAsync(HttpMethod.Post, "/api/loans", Loan("2100-12", 360));
        _loan = loan.GetProperty("loan").GetProperty("id").GetGuid();
        await SendAsync(HttpMethod.Post, "/api/card-purchases", Purchase(new DateOnly(2100, 12, 20), installments: 12));
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
    }

    [Theory]
    [MemberData(nameof(MonthQueries))]
    public async Task Months_outside_the_range_are_refused_with_the_range_message(string query)
    {
        foreach (var month in new[] { "9999-12", "0001-01", "1999-12", "2101-01" })
        {
            await AssertRefusedAsync(HttpMethod.Get, string.Format(CultureInfo.InvariantCulture, query, month), null, "month", MonthMessage);
        }
    }

    [Theory]
    [MemberData(nameof(MonthQueries))]
    public async Task A_month_in_the_wrong_format_keeps_its_message(string query)
    {
        await AssertRefusedAsync(
            HttpMethod.Get, string.Format(CultureInfo.InvariantCulture, query, "2026-13"), null, "month", "Enter the month as yyyy-MM.");
    }

    [Theory]
    [MemberData(nameof(MonthQueries))]
    public async Task The_first_and_last_month_of_the_range_work(string query)
    {
        foreach (var month in new[] { "2000-01", "2100-12" })
        {
            await GetAsync(string.Format(CultureInfo.InvariantCulture, query, month));
        }
    }

    [Fact]
    public async Task Months_in_routes_are_checked_the_same_way()
    {
        await AssertRefusedAsync(HttpMethod.Get, $"/api/cards/{_card}/card-payments/2101-01", null, "month", MonthMessage);
        await AssertRefusedAsync(HttpMethod.Delete, $"/api/loans/{_loan}/payments/1999-12/Regular/paid", null, "month", MonthMessage);
        await AssertRefusedAsync(HttpMethod.Get, $"/api/revolving-cards/{_revolving}?to=2101-01", null, "to", MonthMessage);
    }

    [Fact]
    public async Task A_revolving_card_can_be_estimated_up_to_the_last_month()
    {
        var card = await GetAsync($"/api/revolving-cards/{_revolving}?to=2100-12");

        Assert.NotEqual(JsonValueKind.Undefined, card.ValueKind);
    }

    [Fact]
    public async Task The_dashboard_and_chart_still_work()
    {
        await GetAsync("/api/dashboard");
        await GetAsync("/api/cash-flow/chart?from=2000-01-01&to=2009-12-31");
        // The chart keeps its ten year limit (FR-4).
        var tooLong = await Client.GetAsync(new Uri("/api/cash-flow/chart?from=2000-01-01&to=2100-12-31", UriKind.Relative), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
    }

    [Fact]
    public async Task Dates_outside_the_range_are_refused_on_their_field()
    {
        var early = new DateOnly(1999, 12, 31);
        var late = new DateOnly(2101, 1, 1);

        await AssertRefusedAsync(HttpMethod.Post, "/api/inputs", Input(late), "issueDate", DateMessage);
        await AssertRefusedAsync(HttpMethod.Post, "/api/inputs", Input(early), "expectedDate", DateMessage);
        await AssertRefusedAsync(HttpMethod.Post, "/api/inputs",
            Input(MonthStart, recurrence: new { frequency = "Monthly", endDate = Iso(late) }), "endDate", DateMessage);
        await AssertRefusedAsync(HttpMethod.Post, $"/api/bills?month={Month(MonthStart)}", Bill(MonthStart, late), "dueDate", DateMessage);
        await AssertRefusedAsync(HttpMethod.Post, "/api/expenses", Expense(early), "date", DateMessage);
        await AssertRefusedAsync(HttpMethod.Post, "/api/card-purchases", Purchase(late, installments: null), "purchaseDate", DateMessage);
        await AssertRefusedAsync(HttpMethod.Post, $"/api/cards/{_account}/balance-entries",
            new { kind = "Correction", date = Iso(early), balanceCents = 1_000L, note = (string?)null }, "date", DateMessage);
        await AssertRefusedAsync(HttpMethod.Post, $"/api/revolving-cards/{_revolving}/extra-payments",
            new { accountId = _account, date = Iso(late), amountCents = 1_000L, description = (string?)null }, "date", DateMessage);
        await AssertRefusedAsync(HttpMethod.Get, "/api/cash-flow/chart?from=1999-12-31&to=2001-01-01", null, "from", DateMessage);

        var bill = await SendAsync(HttpMethod.Post, $"/api/bills?month={Month(MonthStart)}", Bill(MonthStart, MonthStart.AddDays(14)));
        await AssertRefusedAsync(HttpMethod.Post, $"/api/bills/{bill.GetProperty("id").GetGuid()}/paid",
            new { paidDate = Iso(late), paidFromId = _account }, "paidDate", DateMessage);
    }

    [Fact]
    public async Task Months_outside_the_range_are_refused_in_saved_data()
    {
        await AssertRefusedAsync(HttpMethod.Post, "/api/loans", Loan("1999-12", 12), "startMonth", MonthMessage);
        await AssertRefusedAsync(HttpMethod.Post, "/api/loans", Loan("2026-01", null, endMonth: "2101-01"), "endMonth", MonthMessage);
        await AssertRefusedAsync(HttpMethod.Post, $"/api/loans/{_loan}/payment-changes",
            new { fromMonth = "2101-01", monthlyPaymentCents = 9_000L, paymentDay = (int?)null, note = (string?)null, keepChangedPayments = true },
            "fromMonth", MonthMessage);
        await AssertRefusedAsync(HttpMethod.Post, "/api/cards", Card("Old card", "Debit", validThrough: "2101-01"), "validThrough", MonthMessage);
    }

    [Fact]
    public async Task Dates_at_the_edges_of_the_range_are_accepted()
    {
        await SendAsync(HttpMethod.Post, "/api/inputs", Input(new DateOnly(2000, 1, 1)));
        await SendAsync(HttpMethod.Post, "/api/inputs", Input(new DateOnly(2100, 12, 31)));
        await SendAsync(HttpMethod.Post, "/api/bills?month=2100-12", Bill(new DateOnly(2100, 12, 1), new DateOnly(2100, 12, 31)));
        await SendAsync(HttpMethod.Post, "/api/expenses", Expense(new DateOnly(2000, 1, 1)));
        await SendAsync(HttpMethod.Post, "/api/loans", Loan("2000-01", 12));
        await CreateCardAsync(Card("Long card", "Debit", validThrough: "2100-12"));

        await GetAsync("/api/inputs?month=2100-12");
        await GetAsync("/api/cash-flow/report?month=2000-01");
    }

    [Fact]
    public async Task Data_stored_outside_the_range_keeps_loading()
    {
        await SendAsync(HttpMethod.Post, "/api/inputs", Input(MonthStart));
        await ExecuteSqlAsync(
            "UPDATE cash_flow_input SET issue_date = '1990-05-01', expected_date = '1990-05-10', received_date = '1990-05-10' "
            + "WHERE recurring_input_id IS NULL; "
            + "UPDATE recurring_input SET issue_anchor = '1990-05-01', expected_anchor = '1990-05-01';");

        await GetAsync($"/api/inputs?month={Month(MonthStart)}");
        await GetAsync("/api/inputs?month=2000-01");
        await GetAsync($"/api/cash-flow/report?month={Month(MonthStart)}");
        await GetAsync("/api/cash-flow/report?month=2000-01");
        await GetAsync("/api/overall-balance?month=2000-01");
        await GetAsync("/api/dashboard");
        await GetAsync($"/api/cards/{_account}/balance");
    }

    private async Task AssertRefusedAsync(HttpMethod method, string path, object? body, string field, string message)
    {
        using var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative));
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }
        var response = await Client.SendAsync(request, Ct);
        var text = await response.Content.ReadAsStringAsync(Ct);
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, $"{method} {path}: {(int)response.StatusCode} {text}");
        var errors = JsonDocument.Parse(text).RootElement.GetProperty("errors");
        Assert.True(errors.TryGetProperty(field, out var messages), $"{method} {path}: no error on {field}: {text}");
        Assert.Contains(message, messages.EnumerateArray().Select(m => m.GetString()));
    }

    private object Input(DateOnly date, object? recurrence = null) =>
        new
        {
            payerName = "Employer",
            accountId = _account,
            categoryId = Salary,
            amountCents = 200_000L,
            issueDate = Iso(date),
            expectedDate = Iso(date),
            receivedDate = (string?)null,
            description = (string?)null,
            recurrence,
        };

    private object Bill(DateOnly issue, DateOnly due) =>
        new
        {
            name = "Electricity bill",
            categoryId = _electricity,
            payee = (string?)null,
            amountCents = 5_000L,
            issueDate = Iso(issue),
            dueDate = Iso(due),
            paidFromId = _account,
            description = (string?)null,
            paidDate = (string?)null,
        };

    private object Expense(DateOnly date) =>
        new { date = Iso(date), amountCents = 1_000L, categoryId = _electricity, paidFromId = _account, description = (string?)null };

    private object Purchase(DateOnly date, int? installments) =>
        new
        {
            cardId = _card,
            description = "Washing machine",
            purchaseDate = Iso(date),
            amountCents = 12_000L,
            inInstallments = installments is not null,
            installmentCount = installments,
            postponeFirst = false,
            categoryId = (Guid?)null,
        };

    private object Loan(string startMonth, int? count, string? endMonth = null) =>
        new
        {
            name = $"Cash loan {startMonth} {count} {endMonth}",
            categoryId = BankLoan,
            subcategoryId = CashLoan,
            lenderId = ZaBa,
            paymentMethod = "StandingOrder",
            accountId = _account,
            startMonth,
            paymentDay = 15,
            monthlyPaymentCents = 10_000L,
            paymentCount = count,
            endMonth,
            residualValueCents = (long?)null,
            description = (string?)null,
        };

    private static object Card(string name, string type, bool isPrimary = false, Guid? payingAccountId = null, string validThrough = "2099-12") =>
        new
        {
            name,
            issuerId = VisaIssuer,
            bankId = ZaBa,
            cardNumber = (string?)null,
            iban = (string?)null,
            validFrom = (string?)null,
            validThrough,
            type,
            isPrimary,
            debtLimitCents = (long?)null,
            allowsInstallments = false,
            statementDay = payingAccountId is null ? (int?)null : 5,
            paymentDay = payingAccountId is null ? (int?)null : 20,
            payingAccountId,
            paysFromAccountDirectly = false,
            maxInstallments = payingAccountId is null ? (int?)null : 36,
        };

    private object Revolving(string name) =>
        new
        {
            name,
            issuerId = VisaIssuer,
            bankId = ZaBa,
            cardNumber = (string?)null,
            iban = (string?)null,
            validFrom = (string?)null,
            validThrough = "2099-12",
            type = "Revolving",
            isPrimary = false,
            debtLimitCents = 500_000L,
            allowsInstallments = false,
            payingAccountId = _account,
            paysFromAccountDirectly = false,
            minPaymentBasisPoints = 833,
            minPaymentAmountCents = 1_327L,
            interestBasisPoints = 0,
            minPaymentInterestBasisPoints = (int?)null,
            billDay = 5,
            dueDay = 20,
            startingDebtCents = 100_000L,
            startingDebtDate = Iso(MonthStart.AddMonths(-2)),
        };

    private async Task<Guid> CreateCardAsync(object card)
    {
        var response = await Client.PostAsJsonAsync("/api/cards", card, Ct);
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
        Assert.True(response.IsSuccessStatusCode, $"{method} {path}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync(Ct)}");
        return response.StatusCode == HttpStatusCode.NoContent
            ? default
            : await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    private async Task<JsonElement> GetAsync(string path)
    {
        var response = await Client.GetAsync(new Uri(path, UriKind.Relative), Ct);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"GET {path}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync(Ct)}");
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
