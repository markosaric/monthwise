using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Npgsql;

namespace Monthwise.Api.Tests;

/// <summary>
/// Loan endpoints against a real PostgreSQL (skipped without MONTHWISE_TEST_DB). Each test starts from
/// empty loan, cash flow and card tables; months are relative to today.
/// </summary>
[Collection(SharedDatabase.Name)]
public sealed class LoanEndpointTests : IAsyncLifetime
{
    private static readonly Guid VisaIssuer = new("0199b0c0-0000-7000-8000-000000000001");
    private static readonly Guid ZaBa = new("0199b0c0-0000-7000-8000-000000000101");
    private static readonly Guid BankLoan = new("0199c2a0-0000-7000-8000-000000000301");
    private static readonly Guid Lease = new("0199c2a0-0000-7000-8000-000000000302");
    private static readonly Guid CashLoan = new("0199c2a0-0000-7000-8000-000000000312");
    private static readonly Guid CarLease = new("0199c2a0-0000-7000-8000-000000000321");
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.Now);
    private static readonly DateOnly MonthStart = new(Today.Year, Today.Month, 1);
    private static readonly long[] AmountsAfterChange = [50_000, 60_000, 60_000, 60_000, 60_000, 60_000];

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
        await ExecuteSqlAsync(CashFlowEndpointTests.DeleteAllSql);
        _account = await CreateAccountAsync("Main account");
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
    public async Task Add_list_and_delete_a_standing_order_loan()
    {
        var created = await SendAsync(HttpMethod.Post, "/api/loans", Loan("Flat", 50_000, start: MonthStart.AddMonths(1), count: 12));
        var loan = created.GetProperty("loan");
        var id = loan.GetProperty("id").GetGuid();
        Assert.Equal("Active", loan.GetProperty("status").GetString());
        Assert.Equal(Month(MonthStart.AddMonths(12)), loan.GetProperty("endMonth").GetString());
        Assert.Equal(600_000, loan.GetProperty("remainingAmountCents").GetInt64());
        Assert.Equal("Main account", loan.GetProperty("account").GetProperty("name").GetString());
        Assert.Equal(12, created.GetProperty("payments").GetArrayLength());

        Assert.Single((await GetAsync("/api/loans")).EnumerateArray());
        Assert.Empty((await GetAsync("/api/loans?show=Closed")).EnumerateArray());

        var month = await GetAsync($"/api/loan-payments?month={Month(MonthStart.AddMonths(1))}");
        var payment = Assert.Single(month.GetProperty("payments").EnumerateArray());
        Assert.Equal(1, payment.GetProperty("number").GetInt32());
        Assert.Equal(12, payment.GetProperty("total").GetInt32());
        Assert.Equal(50_000, month.GetProperty("totals").GetProperty("expectedCents").GetInt64());
        Assert.Empty((await GetAsync($"/api/loan-payments?month={Month(MonthStart)}")).GetProperty("payments").EnumerateArray());

        var dashboard = await GetAsync("/api/loans/dashboard");
        Assert.True(dashboard.GetProperty("hasAnyLoans").GetBoolean());
        Assert.Equal(600_000, dashboard.GetProperty("totalRemainingCents").GetInt64());

        var deleted = await Client.DeleteAsync(new Uri($"/api/loans/{id}", UriKind.Relative), Ct);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.False((await GetAsync("/api/loans/dashboard")).GetProperty("hasAnyLoans").GetBoolean());
    }

    [Fact]
    public async Task Invalid_loans_return_field_errors()
    {
        var response = await Client.PostAsJsonAsync(
            "/api/loans",
            Loan("", 0, start: MonthStart, count: 0, paymentDay: 32, residual: 1_000),
            Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("errors");
        foreach (var field in new[] { "name", "monthlyPaymentCents", "paymentDay", "paymentCount", "residualValueCents" })
        {
            Assert.True(errors.TryGetProperty(field, out _), field);
        }
        Assert.Empty((await GetAsync("/api/loans")).EnumerateArray());

        var invalidMonth = await Client.GetAsync(new Uri("/api/loan-payments?month=2026-13", UriKind.Relative), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, invalidMonth.StatusCode);
    }

    [Fact]
    public async Task A_name_is_unique_among_loans_that_are_not_closed()
    {
        var first = await SendAsync(HttpMethod.Post, "/api/loans", Loan("Flat", 50_000, start: MonthStart.AddMonths(1), count: 12));

        var duplicate = await Client.PostAsJsonAsync("/api/loans", Loan(" flat ", 10_000, start: MonthStart.AddMonths(1), count: 2), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        Assert.Contains("\"name\"", await duplicate.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);

        await SendAsync(HttpMethod.Post, $"/api/loans/{first.GetProperty("loan").GetProperty("id").GetGuid()}/close");
        await SendAsync(HttpMethod.Post, "/api/loans", Loan("Flat", 10_000, start: MonthStart.AddMonths(1), count: 2));
    }

    [Fact]
    public async Task The_database_rejects_a_zero_payment_and_a_standing_order_without_an_account()
    {
        var created = await SendAsync(HttpMethod.Post, "/api/loans", Loan("Flat", 50_000, start: MonthStart.AddMonths(1), count: 12));
        var id = created.GetProperty("loan").GetProperty("id").GetGuid();

        await Assert.ThrowsAsync<PostgresException>(() =>
            ExecuteSqlAsync($"UPDATE loan SET monthly_payment_cents = 0 WHERE id = '{id}'"));
        await Assert.ThrowsAsync<PostgresException>(() =>
            ExecuteSqlAsync($"UPDATE loan SET account_id = NULL WHERE id = '{id}'"));
    }

    [Fact]
    public async Task A_lease_takes_a_residual_value_paid_as_a_bill_after_the_last_payment()
    {
        var created = await SendAsync(
            HttpMethod.Post, "/api/loans",
            Loan("Car", 30_000, start: MonthStart.AddMonths(1), count: 3, category: Lease, subcategory: CarLease, residual: 200_000));

        var payments = created.GetProperty("payments").EnumerateArray().ToList();
        Assert.Equal(4, payments.Count);
        var residual = payments[^1];
        Assert.Equal("Residual", residual.GetProperty("kind").GetString());
        Assert.Equal("Bill", residual.GetProperty("paymentMethod").GetString());
        Assert.Equal(Month(MonthStart.AddMonths(4)), residual.GetProperty("month").GetString());
        Assert.Equal(290_000, created.GetProperty("loan").GetProperty("remainingAmountCents").GetInt64());
    }

    [Fact]
    public async Task A_paid_bill_is_an_output_and_the_loan_can_then_only_be_closed()
    {
        var created = await SendAsync(
            HttpMethod.Post, "/api/loans",
            Loan("Cash", 20_000, start: MonthStart, count: 6, method: "Bill", paymentDay: 28));
        var id = created.GetProperty("loan").GetProperty("id").GetGuid();
        var path = $"/api/loans/{id}/payments/{Month(MonthStart)}/Regular";

        var paid = await SendAsync(HttpMethod.Post, $"{path}/paid", new { paidDate = (string?)null, accountId = _account });
        Assert.True(paid.GetProperty("isPaid").GetBoolean());
        Assert.Equal(Iso(Today), paid.GetProperty("paidDate").GetString());
        Assert.Equal("Main account", paid.GetProperty("account").GetProperty("name").GetString());

        var unpaid = await SendAsync(HttpMethod.Delete, $"{path}/paid");
        Assert.False(unpaid.GetProperty("isPaid").GetBoolean());
        await SendAsync(HttpMethod.Post, $"{path}/paid", new { paidDate = (string?)null, accountId = _account });

        var report = await GetAsync($"/api/cash-flow/report?month={Month(MonthStart)}");
        var outputs = report.GetProperty("outputs");
        Assert.Equal(20_000, outputs.GetProperty("totals").GetProperty("paidCents").GetInt64());
        Assert.Equal("Cash", Assert.Single(outputs.GetProperty("byLoan").EnumerateArray()).GetProperty("name").GetString());

        var deleted = await Client.DeleteAsync(new Uri($"/api/loans/{id}", UriKind.Relative), Ct);
        Assert.Equal(HttpStatusCode.Conflict, deleted.StatusCode);

        var closed = await SendAsync(HttpMethod.Post, $"/api/loans/{id}/close");
        Assert.Equal("Closed", closed.GetProperty("loan").GetProperty("status").GetString());
        Assert.Single(closed.GetProperty("payments").EnumerateArray());
        Assert.Single((await GetAsync("/api/loans?show=Closed")).EnumerateArray());
        Assert.Empty((await GetAsync("/api/loans")).EnumerateArray());

        var closedPayment = await Client.DeleteAsync(new Uri($"{path}/paid", UriKind.Relative), Ct);
        Assert.Equal(HttpStatusCode.Conflict, closedPayment.StatusCode);
    }

    [Fact]
    public async Task Payment_changes_and_changed_payments_follow_the_month()
    {
        var created = await SendAsync(HttpMethod.Post, "/api/loans", Loan("Flat", 50_000, start: MonthStart.AddMonths(1), count: 6));
        var id = created.GetProperty("loan").GetProperty("id").GetGuid();
        var second = MonthStart.AddMonths(2);

        var changed = await SendAsync(HttpMethod.Put, $"/api/loans/{id}/payments/{Month(second)}/Regular", new
        {
            amountCents = 45_000,
            dueDate = Iso(second.AddDays(19)),
        });
        Assert.True(changed.GetProperty("isChanged").GetBoolean());
        Assert.Equal(Iso(second.AddDays(19)), changed.GetProperty("dueDate").GetString());

        var withChange = await SendAsync(HttpMethod.Post, $"/api/loans/{id}/payment-changes", new
        {
            fromMonth = Month(second),
            monthlyPaymentCents = 60_000,
            paymentDay = (int?)null,
            note = "New rate",
            keepChangedPayments = false,
        });
        var change = Assert.Single(withChange.GetProperty("changes").EnumerateArray());
        Assert.True(change.GetProperty("canChange").GetBoolean());
        var amounts = withChange.GetProperty("payments").EnumerateArray().Select(p => p.GetProperty("amountCents").GetInt64()).ToList();
        Assert.Equal(AmountsAfterChange, amounts);

        var changeId = change.GetProperty("id").GetGuid();
        var removed = await SendAsync(HttpMethod.Delete, $"/api/loans/{id}/payment-changes/{changeId}");
        Assert.Empty(removed.GetProperty("changes").EnumerateArray());
        Assert.All(removed.GetProperty("payments").EnumerateArray(), p => Assert.Equal(50_000, p.GetProperty("amountCents").GetInt64()));
    }

    [Fact]
    public async Task Loan_categories_and_subcategories_are_added_hidden_and_deleted()
    {
        var category = await SendAsync(HttpMethod.Post, "/api/loan-categories", new { name = "Leasing club", allowsResidualValue = true });
        var categoryId = category.GetProperty("id").GetGuid();
        Assert.False(category.GetProperty("isPredefined").GetBoolean());

        var subcategory = await SendAsync(HttpMethod.Post, $"/api/loan-categories/{categoryId}/subcategories", new { name = "Bike" });
        var subcategoryId = subcategory.GetProperty("id").GetGuid();
        await SendAsync(HttpMethod.Post, $"/api/loan-subcategories/{subcategoryId}/hide");
        var visible = await GetAsync("/api/loan-categories");
        Assert.Empty(visible.EnumerateArray().Single(c => c.GetProperty("id").GetGuid() == categoryId).GetProperty("subcategories").EnumerateArray());

        await SendAsync(HttpMethod.Post, $"/api/loan-subcategories/{subcategoryId}/show");
        await SendAsync(HttpMethod.Post, "/api/loans", Loan("Bike", 5_000, start: MonthStart.AddMonths(1), count: 2, category: categoryId, subcategory: subcategoryId));

        var used = await Client.DeleteAsync(new Uri($"/api/loan-categories/{categoryId}", UriKind.Relative), Ct);
        Assert.Equal(HttpStatusCode.Conflict, used.StatusCode);
        var usedSubcategory = await Client.DeleteAsync(new Uri($"/api/loan-subcategories/{subcategoryId}", UriKind.Relative), Ct);
        Assert.Equal(HttpStatusCode.Conflict, usedSubcategory.StatusCode);

        var unused = await SendAsync(HttpMethod.Post, "/api/loan-categories", new { name = "Unused", allowsResidualValue = false });
        var deleted = await Client.DeleteAsync(new Uri($"/api/loan-categories/{unused.GetProperty("id").GetGuid()}", UriKind.Relative), Ct);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
    }

    private object Loan(
        string name,
        long monthlyPaymentCents,
        DateOnly start,
        int count,
        string method = "StandingOrder",
        int paymentDay = 15,
        Guid? category = null,
        Guid? subcategory = null,
        long? residual = null) =>
        new
        {
            name,
            categoryId = category ?? BankLoan,
            subcategoryId = subcategory ?? CashLoan,
            lenderId = ZaBa,
            paymentMethod = method,
            accountId = method == "StandingOrder" ? _account : (Guid?)null,
            startMonth = Month(start),
            paymentDay,
            monthlyPaymentCents,
            paymentCount = count,
            endMonth = (string?)null,
            residualValueCents = residual,
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
