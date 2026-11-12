using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Npgsql;

namespace Monthwise.Api.Tests;

/// <summary>
/// Revolving card endpoints against a real PostgreSQL (see TestDatabase). Each test
/// starts from empty tables with a primary account (made-up balance) and a revolving card paid from it
/// (8.33 % or 13.27 EUR minimum, no interest, bill on the 5th, due on the 20th), with a debt of
/// 1,000.00 EUR from the first day of the month before last.
/// </summary>
[Collection(SharedDatabase.Name)]
public sealed class RevolvingEndpointTests : IAsyncLifetime
{
    private const string Password = "secret-phrase-1";
    private static readonly Guid VisaIssuer = new("0199b0c0-0000-7000-8000-000000000001");
    private static readonly Guid ZaBa = new("0199b0c0-0000-7000-8000-000000000101");
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.Now);
    private static readonly DateOnly MonthStart = new(Today.Year, Today.Month, 1);
    private static readonly DateOnly DebtStart = MonthStart.AddMonths(-2);
    private static readonly DateOnly LastMonth = MonthStart.AddMonths(-1);

    private MonthwiseApiFactory? _factory;
    private HttpClient? _client;
    private string _connectionString = string.Empty;
    private Guid _account;
    private Guid _revolving;
    private Guid _groceries;

    private HttpClient Client => _client!;
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _connectionString = TestDatabase.ConnectionString;

        _factory = new MonthwiseApiFactory(_connectionString, migrateOnStartup: true);
        _client = _factory.CreateClient();
        await Client.GetAsync(new Uri("/api/health", UriKind.Relative), Ct);
        await ExecuteSqlAsync(CashFlowEndpointTests.DeleteAllSql);
        _account = (await CreateCardAsync(Card("Main account", "Debit", isPrimary: true))).GetProperty("id").GetGuid();
        await SendAsync(HttpMethod.Post, $"/api/cards/{_account}/balance-entries",
            new { kind = "Starting", date = Iso(DebtStart.AddDays(-1)), balanceCents = 200_000, note = (string?)null });
        _revolving = (await CreateCardAsync(Revolving("Revolving Visa", startingDebtCents: 100_000))).GetProperty("id").GetGuid();
        var categories = (await GetAsync("/api/expense-categories")).EnumerateArray().ToList();
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
    public async Task A_revolving_card_has_its_terms_and_starting_debt_and_is_not_an_account()
    {
        var card = await GetAsync($"/api/cards/{_revolving}");
        Assert.Equal("Revolving", card.GetProperty("type").GetString());
        var terms = card.GetProperty("revolving");
        Assert.Equal(833, terms.GetProperty("minPaymentBasisPoints").GetInt32());
        Assert.Equal(1_327, terms.GetProperty("minPaymentAmountCents").GetInt64());
        Assert.Equal(5, terms.GetProperty("billDay").GetInt32());
        Assert.Equal(20, terms.GetProperty("dueDay").GetInt32());
        Assert.Equal(_account, terms.GetProperty("payingAccount").GetProperty("id").GetGuid());
        Assert.Equal(100_000, card.GetProperty("revolvingDebtCents").GetInt64());

        var details = await GetAsync($"/api/revolving-cards/{_revolving}");
        var starting = Assert.Single(details.GetProperty("debtEntries").EnumerateArray());
        Assert.Equal("Starting", starting.GetProperty("kind").GetString());
        // No interest and nothing paid: the bills do not change the debt.
        var lastBill = details.GetProperty("bills").EnumerateArray().Single(b => b.GetProperty("month").GetString() == Month(LastMonth));
        Assert.Equal(100_000, lastBill.GetProperty("debtCents").GetInt64());
        Assert.Equal(8_330, lastBill.GetProperty("minimumCents").GetInt64());
        Assert.False(lastBill.GetProperty("isEstimated").GetBoolean());

        // Not offered where money comes from or goes to an account.
        var dashboard = await GetAsync("/api/cards/dashboard");
        Assert.DoesNotContain(dashboard.GetProperty("cards").EnumerateArray(), c => c.GetProperty("id").GetGuid() == _revolving);
    }

    [Fact]
    public async Task Card_rules_are_reported_per_field()
    {
        var noLimit = await Client.PostAsJsonAsync("/api/cards", Revolving("No limit", debtLimitCents: null), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, noLimit.StatusCode);
        Assert.True((await noLimit.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("errors").TryGetProperty("debtLimitCents", out _));

        var wrongTerms = await Client.PostAsJsonAsync("/api/cards", Revolving("Wrong terms", minPaymentBasisPoints: 0, billDay: 32), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, wrongTerms.StatusCode);
        var errors = (await wrongTerms.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("errors");
        Assert.True(errors.TryGetProperty("minPaymentBasisPoints", out _));
        Assert.True(errors.TryGetProperty("billDay", out _));

        var installments = await Client.PostAsJsonAsync("/api/card-purchases", new
        {
            cardId = _revolving,
            description = "Television",
            purchaseDate = Iso(Today),
            amountCents = 60_000,
            inInstallments = true,
            installmentCount = 6,
            postponeFirst = false,
            categoryId = (Guid?)null,
        }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, installments.StatusCode);
        Assert.True((await installments.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("errors").TryGetProperty("inInstallments", out _));
    }

    [Fact]
    public async Task Charges_raise_the_debt_and_payments_lower_it_and_leave_the_account()
    {
        await SendAsync(HttpMethod.Post, "/api/expenses",
            new { date = Iso(Today), amountCents = 4_000, categoryId = _groceries, paidFromId = _revolving, description = "Konzum" });
        Assert.Equal(104_000, await DebtAsync());
        // A charge on the card is not money out of the account.
        Assert.Equal(200_000, await CurrentBalanceAsync());

        var paid = await SendAsync(HttpMethod.Post, $"/api/revolving-cards/{_revolving}/bills/{Month(LastMonth)}/paid",
            new { paidDate = Iso(Today), accountId = (Guid?)null, amountCents = (long?)null });
        Assert.True(paid.GetProperty("isPaid").GetBoolean());
        Assert.Equal(8_330, paid.GetProperty("paidAmountCents").GetInt64());
        Assert.Equal(_account, paid.GetProperty("account").GetProperty("id").GetGuid());

        await SendAsync(HttpMethod.Post, $"/api/revolving-cards/{_revolving}/extra-payments",
            new { accountId = _account, date = Iso(Today), amountCents = 10_000, description = "Lower the debt" });
        Assert.Equal(104_000 - 8_330 - 10_000, await DebtAsync());
        Assert.Equal(200_000 - 8_330 - 10_000, await CurrentBalanceAsync());

        var tooMuch = await Client.PostAsJsonAsync($"/api/revolving-cards/{_revolving}/extra-payments",
            new { accountId = _account, date = Iso(Today), amountCents = 1_000_000, description = (string?)null }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, tooMuch.StatusCode);

        // A paid bill is in the month of its paid date.
        var thisMonth = await GetAsync($"/api/revolving-bills?month={Month(MonthStart)}");
        Assert.Contains(thisMonth.GetProperty("bills").EnumerateArray(),
            b => b.GetProperty("month").GetString() == Month(LastMonth) && b.GetProperty("isPaid").GetBoolean());
        Assert.Equal(10_000, Assert.Single(thisMonth.GetProperty("extraPayments").EnumerateArray()).GetProperty("amountCents").GetInt64());

        var outputs = (await GetAsync($"/api/cash-flow/report?month={Month(MonthStart)}")).GetProperty("outputs");
        Assert.Equal(18_330, outputs.GetProperty("totals").GetProperty("paidCents").GetInt64());
        Assert.Contains(outputs.GetProperty("byCategory").EnumerateArray(), g => g.GetProperty("name").GetString() == "Card payments");
        Assert.Contains(outputs.GetProperty("byCard").EnumerateArray(), g => g.GetProperty("id").GetGuid() == _revolving);

        var chart = await GetAsync(
            $"/api/cash-flow/chart?from={Iso(MonthStart)}&to={Iso(MonthStart.AddMonths(1).AddDays(-1))}&granularity=Month");
        Assert.Equal(18_330, Assert.Single(chart.GetProperty("buckets").EnumerateArray()).GetProperty("outputPaidCents").GetInt64());

        var unpaid = await SendAsync(HttpMethod.Delete, $"/api/revolving-cards/{_revolving}/bills/{Month(LastMonth)}/paid");
        Assert.False(unpaid.GetProperty("isPaid").GetBoolean());
        Assert.Equal(104_000 - 10_000, await DebtAsync());
    }

    [Fact]
    public async Task An_extra_payment_on_the_date_of_the_starting_debt_lowers_it()
    {
        await SendAsync(HttpMethod.Post, $"/api/revolving-cards/{_revolving}/extra-payments",
            new { accountId = _account, date = Iso(DebtStart), amountCents = 30_000, description = (string?)null });

        Assert.Equal(70_000,
            (await GetAsync($"/api/revolving-cards/{_revolving}/debt?date={Iso(DebtStart)}")).GetProperty("debtCents").GetInt64());
        Assert.Equal(70_000, await DebtAsync());
    }

    [Fact]
    public async Task A_correction_sets_the_debt_and_the_overview_adds_up_the_cards()
    {
        var correction = await SendAsync(HttpMethod.Post, $"/api/revolving-cards/{_revolving}/debt-entries",
            new { kind = "Correction", date = Iso(Today), debtCents = 80_000, note = "Bank statement" });
        Assert.Equal(80_000, correction.GetProperty("amountCents").GetInt64());
        Assert.Equal(-20_000, correction.GetProperty("differenceCents").GetInt64());
        Assert.Equal(80_000, await DebtAsync());

        await CreateCardAsync(Revolving("Second revolving", startingDebtCents: 5_000));
        var overview = await GetAsync("/api/revolving-cards");
        Assert.Equal(2, overview.GetProperty("cards").GetArrayLength());
        Assert.Equal(85_000, overview.GetProperty("totalDebtCents").GetInt64());
        var card = overview.GetProperty("cards").EnumerateArray().Single(c => c.GetProperty("id").GetGuid() == _revolving);
        Assert.Equal(150_000 - 80_000, card.GetProperty("remainingLimitCents").GetInt64());
    }

    [Fact]
    public async Task A_card_with_debt_cannot_be_archived_until_it_is_paid_off()
    {
        var blocked = await Client.PostAsJsonAsync($"/api/cards/{_revolving}/block", new { reason = "lost" }, Ct);
        Assert.Equal(HttpStatusCode.OK, blocked.StatusCode);

        var refused = await Client.PostAsync(new Uri($"/api/cards/{_revolving}/archive", UriKind.Relative), null, Ct);
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);

        await SendAsync(HttpMethod.Post, $"/api/revolving-cards/{_revolving}/debt-entries",
            new { kind = "Correction", date = Iso(Today), debtCents = 0, note = (string?)null });
        var archived = await Client.PostAsync(new Uri($"/api/cards/{_revolving}/archive", UriKind.Relative), null, Ct);
        Assert.Equal(HttpStatusCode.OK, archived.StatusCode);
    }

    [Fact]
    public async Task A_replacement_card_takes_over_the_debt()
    {
        var replacement = (await CreateCardAsync(Revolving("New revolving", startingDebtCents: null))).GetProperty("id").GetGuid();

        var blocked = await Client.PostAsJsonAsync($"/api/cards/{_revolving}/block", new { reason = "stolen", replacementCardId = replacement }, Ct);
        Assert.True(blocked.StatusCode == HttpStatusCode.OK, await blocked.Content.ReadAsStringAsync(Ct));

        Assert.Equal(0, await DebtAsync());
        Assert.Equal(100_000, (await GetAsync($"/api/revolving-cards/{replacement}/debt")).GetProperty("debtCents").GetInt64());
    }

    [Fact]
    public async Task The_database_refuses_a_revolving_card_without_a_limit()
    {
        var error = await Assert.ThrowsAsync<PostgresException>(() =>
            ExecuteSqlAsync($"UPDATE card SET debt_limit_cents = NULL WHERE id = '{_revolving}'"));

        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
    }

    [Fact]
    public async Task Export_and_import_bring_back_the_debt_and_payments()
    {
        await SendAsync(HttpMethod.Post, $"/api/revolving-cards/{_revolving}/extra-payments",
            new { accountId = _account, date = Iso(Today), amountCents = 10_000, description = (string?)null });
        await SendAsync(HttpMethod.Post, $"/api/revolving-cards/{_revolving}/bills/{Month(LastMonth)}/paid",
            new { paidDate = Iso(Today), accountId = (Guid?)null, amountCents = 9_000L });
        var before = await GetRawAsync($"/api/revolving-cards/{_revolving}");

        var export = await Client.PostAsJsonAsync("/api/data/export", new { password = Password }, Ct);
        Assert.True(export.StatusCode == HttpStatusCode.OK, await export.Content.ReadAsStringAsync(Ct));
        var file = await export.Content.ReadAsByteArrayAsync(Ct);
        await ExecuteSqlAsync(CashFlowEndpointTests.DeleteAllSql);

        using var form = new MultipartFormDataContent();
        var content = new ByteArrayContent(file);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(content, "file", "monthwise-export.monthwise");
        form.Add(new StringContent(Password), "password");
        form.Add(new StringContent("REPLACE"), "confirmation");
        var import = await Client.PostAsync(new Uri("/api/data/import", UriKind.Relative), form, Ct);
        Assert.True(import.StatusCode == HttpStatusCode.OK, await import.Content.ReadAsStringAsync(Ct));

        Assert.Equal(before, await GetRawAsync($"/api/revolving-cards/{_revolving}"));
        Assert.Equal(100_000 - 10_000 - 9_000, await DebtAsync());
    }

    private object Revolving(
        string name,
        long? startingDebtCents = null,
        long? debtLimitCents = 150_000,
        int minPaymentBasisPoints = 833,
        int billDay = 5) =>
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
            debtLimitCents,
            allowsInstallments = false,
            payingAccountId = _account,
            paysFromAccountDirectly = false,
            minPaymentBasisPoints,
            minPaymentAmountCents = 1_327L,
            interestBasisPoints = 0,
            minPaymentInterestBasisPoints = (int?)null,
            billDay,
            dueDay = 20,
            startingDebtCents,
            startingDebtDate = startingDebtCents is null ? null : Iso(DebtStart),
        };

    private static object Card(string name, string type, bool isPrimary = false) =>
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
        };

    private async Task<JsonElement> CreateCardAsync(object card)
    {
        var response = await Client.PostAsJsonAsync("/api/cards", card, Ct);
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    private async Task<long> DebtAsync() =>
        (await GetAsync($"/api/revolving-cards/{_revolving}/debt")).GetProperty("debtCents").GetInt64();

    private async Task<long> CurrentBalanceAsync() =>
        (await GetAsync($"/api/cards/{_account}/balance")).GetProperty("balance").GetProperty("currentBalanceCents").GetInt64();

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

    private async Task<JsonElement> GetAsync(string path) => JsonDocument.Parse(await GetRawAsync(path)).RootElement;

    private async Task<string> GetRawAsync(string path)
    {
        var response = await Client.GetAsync(new Uri(path, UriKind.Relative), Ct);
        var body = await response.Content.ReadAsStringAsync(Ct);
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        return body;
    }

    private async Task ExecuteSqlAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(Ct);
    }
}
