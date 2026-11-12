using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Npgsql;

namespace Monthwise.Api.Tests;

/// <summary>
/// Names are unique ignoring upper/lower case for every letter, also č, ć, ž, š and đ, while accents
/// still count (name comparison FR-1 to FR-4), against a real PostgreSQL (see TestDatabase).
/// </summary>
[Collection(SharedDatabase.Name)]
public sealed class NameComparisonEndpointTests : IAsyncLifetime
{
    private const string CleanUpSql =
        "DELETE FROM card_issuer WHERE NOT is_predefined; DELETE FROM bank WHERE NOT is_predefined;";

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

    private HttpClient Client => _client!;
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _connectionString = TestDatabase.ConnectionString;

        _factory = new MonthwiseApiFactory(_connectionString, migrateOnStartup: true);
        _client = _factory.CreateClient();
        await Client.GetAsync(new Uri("/api/health", UriKind.Relative), Ct);
        await ExecuteSqlAsync(CashFlowEndpointTests.DeleteAllSql + CleanUpSql);
        _account = await PostAsync("/api/cards", Card("Čevapi", isPrimary: true));
    }

    public async ValueTask DisposeAsync()
    {
        if (_factory is not null)
        {
            await ExecuteSqlAsync(CashFlowEndpointTests.DeleteAllSql + CleanUpSql);
        }
        _client?.Dispose();
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
    }

    public static TheoryData<string> SimpleLists =>
        ["/api/card-issuers", "/api/banks", "/api/input-categories", "/api/expense-categories", "/api/participants"];

    [Theory]
    [MemberData(nameof(SimpleLists))]
    public async Task A_name_differing_only_in_case_of_croatian_letters_is_refused_but_other_accents_count(string path)
    {
        await PostAsync(path, NameBody(path, "Čevapi"));

        await AssertNameRefusedAsync(path, NameBody(path, "čevapi"));
        await AssertNameRefusedAsync(path, NameBody(path, "ČEVAPI"));
        await PostAsync(path, NameBody(path, "Cevapi"));
        await PostAsync(path, NameBody(path, "Đak"));
        await AssertNameRefusedAsync(path, NameBody(path, "đAK"));
        await PostAsync(path, NameBody(path, "Dak"));
    }

    [Fact]
    public async Task Cards_loan_categories_and_subcategories_follow_the_same_rule()
    {
        await AssertNameRefusedAsync("/api/cards", Card("ČEVAPI"));
        await PostAsync("/api/cards", Card("Cevapi"));

        var category = await PostAsync("/api/loan-categories", new { name = "Šuma", allowsResidualValue = false });
        await AssertNameRefusedAsync("/api/loan-categories", new { name = "šUMA", allowsResidualValue = false });
        await PostAsync($"/api/loan-categories/{category}/subcategories", new { name = "Žito" });
        await AssertNameRefusedAsync($"/api/loan-categories/{category}/subcategories", new { name = "ŽITO" });
        await PostAsync($"/api/loan-categories/{category}/subcategories", new { name = "Zito" });
    }

    [Fact]
    public async Task Renaming_to_a_name_differing_only_in_case_is_refused()
    {
        await PostAsync("/api/expense-categories", new { name = "Ćevapčići" });
        var other = await PostAsync("/api/expense-categories", new { name = "Other" });

        var rename = await Client.PutAsJsonAsync($"/api/expense-categories/{other}", new { name = "ĆEVAPČIĆI" }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, rename.StatusCode);
        Assert.Contains("\"name\"", await rename.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_loan_name_is_unique_among_loans_that_are_not_closed()
    {
        var first = await PostLoanAsync("Đak");
        await AssertNameRefusedAsync("/api/loans", Loan("đak"));

        var close = await Client.PostAsync(new Uri($"/api/loans/{first}/close", UriKind.Relative), null, Ct);
        Assert.True(close.IsSuccessStatusCode, await close.Content.ReadAsStringAsync(Ct));
        await PostLoanAsync("đak");
    }

    [Fact]
    public async Task A_recurring_bill_name_is_unique_among_active_recurring_bills()
    {
        var categories = await GetAsync("/api/expense-categories");
        var electricity = categories.EnumerateArray()
            .Single(c => c.GetProperty("name").GetString() == "Electricity").GetProperty("id").GetGuid();

        await PostAsync("/api/recurring-bills", RecurringBill("Struja, Šibenik", electricity));
        await AssertNameRefusedAsync("/api/recurring-bills", RecurringBill("STRUJA, ŠIBENIK", electricity));
        await PostAsync("/api/recurring-bills", RecurringBill("Struja, Sibenik", electricity));
    }

    [Fact]
    public async Task A_payer_typed_with_other_case_is_the_existing_payer()
    {
        await PostAsync("/api/inputs", Input("Đuro Šimić"));
        await PostAsync("/api/inputs", Input("đuro šimić"));

        var payers = (await GetAsync("/api/payers")).EnumerateArray().Select(p => p.GetProperty("name").GetString()).ToList();
        Assert.Equal(["Đuro Šimić"], payers);
    }

    [Fact]
    public async Task The_database_refuses_names_differing_only_in_case_however_they_are_written()
    {
        await ExecuteSqlAsync(
            "INSERT INTO payer (id, name, created_at) VALUES ('0199b0c0-0000-7000-8000-00000000f001', 'Žana', now())");

        var exception = await Assert.ThrowsAsync<PostgresException>(() => ExecuteSqlAsync(
            "INSERT INTO payer (id, name, created_at) VALUES ('0199b0c0-0000-7000-8000-00000000f002', 'ŽANA', now())"));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, exception.SqlState);
        Assert.Equal("ux_payer_name", exception.ConstraintName);
    }

    public static TheoryData<string, string, bool> Pairs => new()
    {
        { "Čevapi", "čevapi", true },
        { "ČEVAPI", "čevapi", true },
        { "Ćevapčići", "ĆEVAPČIĆI", true },
        { "Đak", "đAK", true },
        { "Žana", "žana", true },
        { "Štedionica", "ŠTEDIONICA", true },
        { "Ana", "ana", true },
        { "Čaj", "Caj", false },
        { "Đak", "Dak", false },
        { "Ćup", "Čup", false },
        { "Ana", "Ana M.", false },
    };

    [Theory]
    [MemberData(nameof(Pairs))]
    public async Task The_in_memory_rule_agrees_with_the_database_collation(string a, string b, bool same)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand("SELECT @a = @b COLLATE monthwise_name", connection);
        command.Parameters.AddWithValue("a", a);
        command.Parameters.AddWithValue("b", b);
        var database = (bool)(await command.ExecuteScalarAsync(Ct))!;

        Assert.Equal(same, database);
        Assert.Equal(same, Infrastructure.Persistence.NameCollation.AreSame(a, b));
    }

    private static object NameBody(string path, string name) =>
        path == "/api/participants" ? new { name, description = (string?)null } : new { name };

    private static object Card(string name, bool isPrimary = false) =>
        new
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
        };

    private object Loan(string name) =>
        new
        {
            name,
            categoryId = BankLoan,
            subcategoryId = CashLoan,
            lenderId = ZaBa,
            paymentMethod = "StandingOrder",
            accountId = _account,
            startMonth = MonthStart.AddMonths(1).ToString("yyyy-MM", CultureInfo.InvariantCulture),
            paymentDay = 15,
            monthlyPaymentCents = 10_000L,
            paymentCount = 2,
            endMonth = (string?)null,
            residualValueCents = (long?)null,
            description = (string?)null,
        };

    private object RecurringBill(string name, Guid categoryId) =>
        new
        {
            name,
            categoryId,
            payee = (string?)null,
            paidFromId = _account,
            description = (string?)null,
            amountCents = (long?)null,
            issueDay = (int?)null,
            dueDay = (int?)null,
        };

    private object Input(string payerName) =>
        new
        {
            payerName,
            accountId = _account,
            categoryId = Salary,
            amountCents = 1_000L,
            issueDate = Iso(MonthStart),
            expectedDate = Iso(MonthStart.AddDays(9)),
            receivedDate = (string?)null,
            description = (string?)null,
            recurrence = (object?)null,
        };

    private static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private async Task<Guid> PostLoanAsync(string name)
    {
        var response = await Client.PostAsJsonAsync("/api/loans", Loan(name), Ct);
        var body = await response.Content.ReadAsStringAsync(Ct);
        Assert.True(response.IsSuccessStatusCode, body);
        return JsonDocument.Parse(body).RootElement.GetProperty("loan").GetProperty("id").GetGuid();
    }

    /// <summary>Adds an entry and returns its id.</summary>
    private async Task<Guid> PostAsync(string path, object body)
    {
        var response = await Client.PostAsJsonAsync(path, body, Ct);
        var text = await response.Content.ReadAsStringAsync(Ct);
        Assert.True(response.IsSuccessStatusCode, $"{path}: {text}");
        var json = JsonDocument.Parse(text).RootElement;
        return json.TryGetProperty("id", out var id) ? id.GetGuid() : Guid.Empty;
    }

    /// <summary>The list's usual "name already in use" field error, never a general error.</summary>
    private async Task AssertNameRefusedAsync(string path, object body)
    {
        var response = await Client.PostAsJsonAsync(path, body, Ct);
        var text = await response.Content.ReadAsStringAsync(Ct);
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, $"{path}: {(int)response.StatusCode} {text}");
        Assert.Contains("\"name\"", text, StringComparison.Ordinal);
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
