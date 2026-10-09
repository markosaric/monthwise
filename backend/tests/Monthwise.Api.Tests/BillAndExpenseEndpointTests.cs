using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Npgsql;

namespace Monthwise.Api.Tests;

/// <summary>
/// Expense category, recurring bill, bill and expense endpoints against a real PostgreSQL (skipped
/// without MONTHWISE_TEST_DB). Each test starts from empty tables with a primary account and a credit
/// card (statement on the 5th, payment on the 20th) paid from it.
/// </summary>
[Collection(SharedDatabase.Name)]
public sealed class BillAndExpenseEndpointTests : IAsyncLifetime
{
    private static readonly Guid VisaIssuer = new("0199b0c0-0000-7000-8000-000000000001");
    private static readonly Guid ZaBa = new("0199b0c0-0000-7000-8000-000000000101");
    private static readonly Guid CashWithdrawal = new("0199c8b0-0000-7000-8000-000000000499");
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.Now);
    private static readonly DateOnly MonthStart = new(Today.Year, Today.Month, 1);

    private MonthwiseApiFactory? _factory;
    private HttpClient? _client;
    private string _connectionString = string.Empty;
    private Guid _account;
    private Guid _card;
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
        _account = await CreateCardAsync("Main account", "Debit", isPrimary: true);
        _card = await CreateCardAsync("Family Visa", "Credit", payingAccountId: _account);
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
    public async Task The_shared_list_starts_with_bill_and_expense_categories_and_cash_withdrawal_is_fixed()
    {
        var categories = (await GetAsync("/api/expense-categories")).EnumerateArray().ToList();
        var names = categories.Select(c => c.GetProperty("name").GetString()).ToList();
        foreach (var name in new[] { "Electricity", "Water and waste", "Building reserve", "Gym membership", "Groceries", "Other expenses" })
        {
            Assert.Contains(name, names);
        }
        var cash = categories.Single(c => c.GetProperty("id").GetGuid() == CashWithdrawal);
        Assert.True(cash.GetProperty("isFixed").GetBoolean());
        Assert.True(cash.GetProperty("isPredefined").GetBoolean());

        var rename = await Client.PutAsJsonAsync($"/api/expense-categories/{CashWithdrawal}", new { name = "Cash" }, Ct);
        var hide = await Client.PostAsync(new Uri($"/api/expense-categories/{CashWithdrawal}/hide", UriKind.Relative), null, Ct);
        var delete = await Client.DeleteAsync(new Uri($"/api/expense-categories/{CashWithdrawal}", UriKind.Relative), Ct);
        Assert.Equal(HttpStatusCode.Conflict, rename.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, hide.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);

        var purchase = await Client.PostAsJsonAsync("/api/card-purchases", new
        {
            cardId = _card,
            description = "ATM",
            purchaseDate = Iso(Today),
            amountCents = 1_000,
            inInstallments = false,
            installmentCount = (int?)null,
            postponeFirst = false,
            categoryId = CashWithdrawal,
        }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, purchase.StatusCode);
    }

    [Fact]
    public async Task Add_recurring_bills_adds_each_active_recurring_bill_once_as_not_issued()
    {
        var power = await SendAsync(HttpMethod.Post, "/api/recurring-bills", Recurring("Electricity, apartment", _account));
        await SendAsync(HttpMethod.Post, "/api/recurring-bills", Recurring("Gym", _card));
        var duplicate = await Client.PostAsJsonAsync("/api/recurring-bills", Recurring("ELECTRICITY, apartment", null), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);

        var next = Month(MonthStart.AddMonths(1));
        Assert.Equal(2, (await SendAsync(HttpMethod.Post, $"/api/bills/add-recurring?month={next}")).GetProperty("added").GetInt32());
        Assert.Equal(0, (await SendAsync(HttpMethod.Post, $"/api/bills/add-recurring?month={next}")).GetProperty("added").GetInt32());

        var month = await GetAsync($"/api/bills?month={next}");
        Assert.Equal(2, month.GetProperty("notIssuedCount").GetInt32());
        Assert.All(month.GetProperty("bills").EnumerateArray(), b =>
        {
            Assert.Equal("NotIssued", b.GetProperty("state").GetString());
            Assert.Equal(0, b.GetProperty("amountCents").GetInt64());
        });
        Assert.Equal(0, month.GetProperty("totals").GetProperty("totalCents").GetInt64());

        var powerId = power.GetProperty("id").GetGuid();
        await SendAsync(HttpMethod.Post, $"/api/recurring-bills/{powerId}/stop");
        var afterNext = Month(MonthStart.AddMonths(2));
        Assert.Equal(1, (await SendAsync(HttpMethod.Post, $"/api/bills/add-recurring?month={afterNext}")).GetProperty("added").GetInt32());

        var deleteUsed = await Client.DeleteAsync(new Uri($"/api/recurring-bills/{powerId}", UriKind.Relative), Ct);
        Assert.Equal(HttpStatusCode.Conflict, deleteUsed.StatusCode);
        var unused = await SendAsync(HttpMethod.Post, "/api/recurring-bills", Recurring("Water", null));
        var deleteUnused = await Client.DeleteAsync(new Uri($"/api/recurring-bills/{unused.GetProperty("id").GetGuid()}", UriKind.Relative), Ct);
        Assert.Equal(HttpStatusCode.NoContent, deleteUnused.StatusCode);
    }

    [Fact]
    public async Task A_bill_is_issued_paid_and_hints_the_due_date_of_the_next_month()
    {
        await SendAsync(HttpMethod.Post, "/api/recurring-bills", Recurring("Electricity, apartment", _account));
        var thisMonth = Month(MonthStart);
        await SendAsync(HttpMethod.Post, $"/api/bills/add-recurring?month={thisMonth}");
        var bill = Assert.Single((await GetAsync($"/api/bills?month={thisMonth}")).GetProperty("bills").EnumerateArray());
        var id = bill.GetProperty("id").GetGuid();
        Assert.Equal(JsonValueKind.Null, bill.GetProperty("dueDateHint").ValueKind);

        var notIssued = await Client.PostAsJsonAsync($"/api/bills/{id}/paid", new { paidDate = (string?)null, paidFromId = (Guid?)null }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, notIssued.StatusCode);

        var due = MonthStart.AddDays(14);
        var issued = await SendAsync(HttpMethod.Put, $"/api/bills/{id}", Bill("Electricity, apartment", 4_250, MonthStart, due, _account, paidDate: null));
        Assert.Equal("Expected", issued.GetProperty("state").GetString());

        var paid = await SendAsync(HttpMethod.Post, $"/api/bills/{id}/paid", new { paidDate = Iso(due), paidFromId = (Guid?)null });
        Assert.Equal("Paid", paid.GetProperty("state").GetString());
        Assert.Equal(_account, paid.GetProperty("paidFrom").GetProperty("id").GetGuid());
        Assert.Equal(4_250, (await GetAsync($"/api/bills?month={thisMonth}")).GetProperty("totals").GetProperty("paidCents").GetInt64());

        var next = MonthStart.AddMonths(1);
        await SendAsync(HttpMethod.Post, $"/api/bills/add-recurring?month={Month(next)}");
        var nextBill = Assert.Single((await GetAsync($"/api/bills?month={Month(next)}")).GetProperty("bills").EnumerateArray());
        Assert.Equal(Iso(next.AddDays(14)), nextBill.GetProperty("dueDateHint").GetString());

        var unticked = await SendAsync(HttpMethod.Delete, $"/api/bills/{id}/paid");
        Assert.Equal("Expected", unticked.GetProperty("state").GetString());
    }

    [Fact]
    public async Task Bills_and_expenses_paid_with_a_credit_card_are_in_its_card_payment_only()
    {
        var bill = await SendAsync(HttpMethod.Post, $"/api/bills?month={Month(MonthStart)}",
            Bill("Internet", 3_000, Today, Today, _card, paidDate: Today));
        Assert.True(bill.GetProperty("paidWithCard").GetBoolean());
        var expense = await SendAsync(HttpMethod.Post, "/api/expenses", Expense(4_000, _groceries, _card, "Konzum"));
        Assert.True(expense.GetProperty("paidWithCard").GetBoolean());

        var payments = (await GetAsync($"/api/cards/{_card}/card-payments")).EnumerateArray().ToList();
        var payment = Assert.Single(payments);
        Assert.Equal(7_000, payment.GetProperty("amountCents").GetInt64());
        var lines = (await GetAsync($"/api/cards/{_card}/card-payments/{payment.GetProperty("month").GetString()}"))
            .GetProperty("lines").EnumerateArray().ToList();
        Assert.Contains(lines, l => l.GetProperty("kind").GetString() == "Bill" && l.GetProperty("description").GetString() == "Internet");
        Assert.Contains(lines, l => l.GetProperty("kind").GetString() == "Expense"
            && l.GetProperty("sourceId").GetGuid() == expense.GetProperty("id").GetGuid()
            && l.GetProperty("category").GetProperty("name").GetString() == "Groceries");

        var bills = await GetAsync($"/api/bills?month={Month(MonthStart)}");
        Assert.Equal(0, bills.GetProperty("totals").GetProperty("totalCents").GetInt64());
        Assert.Equal(3_000, bills.GetProperty("withCardCents").GetInt64());
        var expenses = await GetAsync($"/api/expenses?month={Month(MonthStart)}");
        Assert.Equal(4_000, expenses.GetProperty("totals").GetProperty("withCardCents").GetInt64());
        Assert.Equal(0, expenses.GetProperty("totals").GetProperty("fromAccountsCents").GetInt64());

        var report = await GetAsync($"/api/cash-flow/report?month={Month(Today)}");
        Assert.DoesNotContain(report.GetProperty("outputs").GetProperty("byCategory").EnumerateArray(),
            g => g.GetProperty("name").GetString() is "Internet" or "Groceries");
    }

    [Fact]
    public async Task Expenses_cash_withdrawals_and_bills_from_an_account_count_in_its_balance_and_the_report()
    {
        await SendAsync(HttpMethod.Post, $"/api/cards/{_account}/balance-entries",
            new { kind = "Starting", date = Iso(MonthStart.AddDays(-1)), balanceCents = 100_000, note = (string?)null });
        await SendAsync(HttpMethod.Post, "/api/expenses", Expense(4_000, _groceries, _account, null));
        var withdrawal = await SendAsync(HttpMethod.Post, "/api/expenses/cash-withdrawals",
            new { date = Iso(Today), amountCents = 10_000, accountId = _account, description = "ATM" });
        Assert.True(withdrawal.GetProperty("isCashWithdrawal").GetBoolean());
        await SendAsync(HttpMethod.Post, $"/api/bills?month={Month(MonthStart)}",
            Bill("Electricity", 2_000, Today, Today, _account, paidDate: null));

        var balance = (await GetAsync($"/api/cards/{_account}/balance")).GetProperty("balance");
        Assert.Equal(86_000, balance.GetProperty("currentBalanceCents").GetInt64());
        Assert.Equal(84_000, balance.GetProperty("expectedBalanceCents").GetInt64());

        var expenses = await GetAsync($"/api/expenses?month={Month(MonthStart)}");
        Assert.Equal(14_000, expenses.GetProperty("totals").GetProperty("totalCents").GetInt64());
        Assert.Equal(10_000, expenses.GetProperty("totals").GetProperty("cashWithdrawalCents").GetInt64());
        Assert.Equal(_account, (await GetAsync("/api/expenses/last-paid-from")).GetProperty("paidFrom").GetProperty("id").GetGuid());

        var outputs = (await GetAsync($"/api/cash-flow/report?month={Month(MonthStart)}")).GetProperty("outputs");
        Assert.Equal(16_000, outputs.GetProperty("totals").GetProperty("totalCents").GetInt64());
        var byCategory = outputs.GetProperty("byCategory").EnumerateArray().ToList();
        Assert.Contains(byCategory, g => g.GetProperty("name").GetString() == "Cash withdrawal");
        Assert.Contains(byCategory, g => g.GetProperty("name").GetString() == "Groceries");

        var chart = await GetAsync(
            $"/api/cash-flow/chart?from={Iso(MonthStart)}&to={Iso(MonthStart.AddMonths(1).AddDays(-1))}&granularity=Month&expenseCategoryId={_groceries}");
        Assert.Equal(4_000, Assert.Single(chart.GetProperty("buckets").EnumerateArray()).GetProperty("outputPaidCents").GetInt64());
    }

    [Fact]
    public async Task Rules_are_reported_per_field()
    {
        var other = await CreateCardAsync("Savings", "Debit");
        var cash = await Client.PostAsJsonAsync("/api/expenses/cash-withdrawals",
            new { date = Iso(Today), amountCents = 10_000, accountId = _card, description = (string?)null }, Ct);
        var notPrimary = await Client.PostAsJsonAsync("/api/expenses/cash-withdrawals",
            new { date = Iso(Today), amountCents = 10_000, accountId = other, description = (string?)null }, Ct);
        var expense = await Client.PostAsJsonAsync("/api/expenses", Expense(0, CashWithdrawal, Guid.Empty, null), Ct);
        var bill = await Client.PostAsJsonAsync($"/api/bills?month={Month(MonthStart)}",
            Bill("", 0, null, null, null, paidDate: null), Ct);

        foreach (var response in new[] { cash, notPrimary, expense, bill })
        {
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        var expenseErrors = (await expense.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("errors");
        foreach (var field in new[] { "amountCents", "categoryId", "paidFromId" })
        {
            Assert.True(expenseErrors.TryGetProperty(field, out _), $"Expected an error for {field}.");
        }
        var billErrors = (await bill.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("errors");
        foreach (var field in new[] { "name", "amountCents", "issueDate", "dueDate" })
        {
            Assert.True(billErrors.TryGetProperty(field, out _), $"Expected an error for {field}.");
        }
    }

    [Fact]
    public async Task Database_rejects_an_issued_bill_without_amount()
    {
        var bill = await SendAsync(HttpMethod.Post, $"/api/bills?month={Month(MonthStart)}",
            Bill("Gas", 1_500, Today, Today, _account, paidDate: null));

        var error = await Assert.ThrowsAsync<PostgresException>(() =>
            ExecuteSqlAsync($"UPDATE bill SET amount_cents = 0 WHERE id = '{bill.GetProperty("id").GetGuid()}';"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
    }

    private object Recurring(string name, Guid? paidFromId) =>
        new { name, categoryId = _electricity, payee = "HEP", paidFromId, description = (string?)null };

    private object Bill(string name, long amountCents, DateOnly? issue, DateOnly? due, Guid? paidFromId, DateOnly? paidDate) =>
        new
        {
            name,
            categoryId = _electricity,
            payee = (string?)null,
            amountCents,
            issueDate = issue is { } i ? Iso(i) : null,
            dueDate = due is { } d ? Iso(d) : null,
            paidFromId,
            description = (string?)null,
            paidDate = paidDate is { } p ? Iso(p) : null,
        };

    private static object Expense(long amountCents, Guid categoryId, Guid paidFromId, string? description) =>
        new { date = Iso(Today), amountCents, categoryId, paidFromId, description };

    private static object Card(string name, string type, bool isPrimary = false, Guid? payingAccountId = null) =>
        new
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
        };

    private async Task<Guid> CreateCardAsync(string name, string type, bool isPrimary = false, Guid? payingAccountId = null)
    {
        var response = await Client.PostAsJsonAsync("/api/cards", Card(name, type, isPrimary, payingAccountId), Ct);
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
