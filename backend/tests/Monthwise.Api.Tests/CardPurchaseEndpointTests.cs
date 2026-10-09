using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Npgsql;

namespace Monthwise.Api.Tests;

/// <summary>
/// Card purchase and card payment endpoints against a real PostgreSQL (skipped without
/// MONTHWISE_TEST_DB). Each test starts from empty tables; purchases are dated next month, so their
/// card payments are next month's (statement on the 5th, payment on the 20th).
/// </summary>
[Collection(SharedDatabase.Name)]
public sealed class CardPurchaseEndpointTests : IAsyncLifetime
{
    private static readonly Guid VisaIssuer = new("0199b0c0-0000-7000-8000-000000000001");
    private static readonly Guid ZaBa = new("0199b0c0-0000-7000-8000-000000000101");
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.Now);
    private static readonly DateOnly NextMonth = new DateOnly(Today.Year, Today.Month, 1).AddMonths(1);

    private MonthwiseApiFactory? _factory;
    private HttpClient? _client;
    private string _connectionString = string.Empty;
    private Guid _account;
    private Guid _card;

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
    public async Task A_credit_card_has_billing_details_and_is_on_the_cards_screen()
    {
        var card = await GetAsync($"/api/cards/{_card}");
        var billing = card.GetProperty("billing");
        Assert.Equal(5, billing.GetProperty("statementDay").GetInt32());
        Assert.Equal(20, billing.GetProperty("paymentDay").GetInt32());
        Assert.Equal(_account, billing.GetProperty("payingAccount").GetProperty("id").GetGuid());
        Assert.Equal(36, billing.GetProperty("maxInstallments").GetInt32());
        Assert.False(card.GetProperty("needsBillingDetails").GetBoolean());

        var cards = await GetAsync("/api/card-purchases/cards");
        Assert.Equal(_card, Assert.Single(cards.EnumerateArray()).GetProperty("id").GetGuid());

        var missing = await Client.PostAsJsonAsync("/api/cards", Card("No billing", "Credit", payingAccountId: null), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
    }

    [Fact]
    public async Task Purchases_make_up_the_card_payments_and_their_drill_down()
    {
        await SendAsync(HttpMethod.Post, "/api/card-purchases", Purchase("Washing machine", 10_000, installments: 3));
        await SendAsync(HttpMethod.Post, "/api/card-purchases", Purchase("Groceries", 2_500, installments: null));

        var payments = (await GetAsync($"/api/cards/{_card}/card-payments")).EnumerateArray().ToList();
        Assert.Equal(3, payments.Count);
        var first = payments.Single(p => p.GetProperty("month").GetString() == Month(NextMonth));
        Assert.Equal(3_333 + 2_500, first.GetProperty("amountCents").GetInt64());
        Assert.Equal(3_334, payments.Single(p => p.GetProperty("month").GetString() == Month(NextMonth.AddMonths(2)))
            .GetProperty("amountCents").GetInt64());

        var details = await GetAsync($"/api/cards/{_card}/card-payments/{Month(NextMonth)}");
        var lines = details.GetProperty("lines").EnumerateArray().ToList();
        Assert.Equal(2, lines.Count);
        Assert.Contains(lines, l => l.GetProperty("kind").GetString() == "Installment" && l.GetProperty("installmentNumber").GetInt32() == 1);
        Assert.Contains(lines, l => l.GetProperty("kind").GetString() == "FullPurchase");

        var month = await GetAsync($"/api/card-payments?month={Month(NextMonth)}");
        Assert.Equal(_card, Assert.Single(month.GetProperty("payments").EnumerateArray()).GetProperty("cardId").GetGuid());
        Assert.Equal(5_833, month.GetProperty("totals").GetProperty("totalCents").GetInt64());
    }

    [Fact]
    public async Task Purchase_rules_are_reported_per_field()
    {
        var response = await Client.PostAsJsonAsync("/api/card-purchases", Purchase("", 0, installments: 37), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("errors");
        foreach (var field in new[] { "description", "amountCents", "installmentCount" })
        {
            Assert.True(errors.TryGetProperty(field, out _), $"Expected an error for {field}.");
        }
    }

    [Fact]
    public async Task Reversals_remove_installments_not_yet_charged_and_only_once()
    {
        var purchase = await SendAsync(HttpMethod.Post, "/api/card-purchases", Purchase("Laptop", 30_000, installments: 3));
        var id = purchase.GetProperty("purchase").GetProperty("id").GetGuid();

        var reversed = await SendAsync(HttpMethod.Post, $"/api/card-purchases/{id}/installments/2/reverse", new { reason = "cancelled" });
        var second = reversed.GetProperty("installments").EnumerateArray().Single(i => i.GetProperty("number").GetInt32() == 2);
        Assert.Equal("cancelled", second.GetProperty("reversalReason").GetString());
        Assert.Equal(2, (await GetAsync($"/api/cards/{_card}/card-payments")).GetArrayLength());

        var again = await Client.PostAsJsonAsync($"/api/card-purchases/{id}/installments/2/reverse", new { reason = (string?)null }, Ct);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);

        var whole = await SendAsync(HttpMethod.Post, $"/api/card-purchases/{id}/reverse", new { reason = "returned" });
        Assert.Equal(0, whole.GetProperty("purchase").GetProperty("remainingCents").GetInt64());
        Assert.Equal(0, (await GetAsync($"/api/cards/{_card}/card-payments")).GetArrayLength());
        var edit = await Client.PutAsJsonAsync($"/api/card-purchases/{id}", Purchase("Laptop", 30_000, installments: 3), Ct);
        Assert.Equal(HttpStatusCode.Conflict, edit.StatusCode);
    }

    [Fact]
    public async Task A_purchase_can_be_edited_and_deleted()
    {
        var purchase = await SendAsync(HttpMethod.Post, "/api/card-purchases", Purchase("TV", 12_000, installments: 12));
        var id = purchase.GetProperty("purchase").GetProperty("id").GetGuid();

        var edited = await SendAsync(HttpMethod.Put, $"/api/card-purchases/{id}", Purchase("TV", 12_000, installments: 6, postpone: true));
        Assert.Equal(6, edited.GetProperty("installments").GetArrayLength());
        Assert.Equal(Month(NextMonth.AddMonths(1)), edited.GetProperty("installments")[0].GetProperty("paymentMonth").GetString());

        var deleted = await Client.DeleteAsync(new Uri($"/api/card-purchases/{id}", UriKind.Relative), Ct);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(0, (await GetAsync($"/api/cards/{_card}/purchases")).GetArrayLength());
    }

    [Fact]
    public async Task A_card_payment_is_ticked_and_unticked_and_shows_in_the_report()
    {
        await SendAsync(HttpMethod.Post, "/api/card-purchases", Purchase("Shoes", 8_000, installments: null));
        var statement = NextMonth.AddDays(4);

        var paid = await SendAsync(
            HttpMethod.Post, $"/api/cards/{_card}/card-payments/{Month(NextMonth)}/paid", new { paidDate = Iso(statement.AddDays(1)), accountId = (Guid?)null });
        Assert.True(paid.GetProperty("isPaid").GetBoolean());
        Assert.Equal(_account, paid.GetProperty("account").GetProperty("id").GetGuid());

        var report = await GetAsync($"/api/cash-flow/report?month={Month(NextMonth)}");
        var outputs = report.GetProperty("outputs");
        Assert.Equal(8_000, outputs.GetProperty("totals").GetProperty("paidCents").GetInt64());
        Assert.Equal(_card, Assert.Single(outputs.GetProperty("byCard").EnumerateArray()).GetProperty("id").GetGuid());
        Assert.Contains(outputs.GetProperty("byCategory").EnumerateArray(), g => g.GetProperty("name").GetString() == "Card payments");

        var unpaid = await SendAsync(HttpMethod.Delete, $"/api/cards/{_card}/card-payments/{Month(NextMonth)}/paid");
        Assert.False(unpaid.GetProperty("isPaid").GetBoolean());
    }

    [Fact]
    public async Task A_debit_card_with_purchases_keeps_allowing_installments()
    {
        var debit = await CreateCardAsync("Debit with installments", "Debit", allowsInstallments: true, payingAccountId: _account);
        await SendAsync(HttpMethod.Post, "/api/card-purchases", Purchase("Bike", 50_000, installments: 6, cardId: debit));

        var response = await Client.PutAsJsonAsync($"/api/cards/{debit}", Card("Debit with installments", "Debit", payingAccountId: null), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("errors");
        Assert.True(errors.TryGetProperty("allowsInstallments", out _));
    }

    [Fact]
    public async Task Categories_can_be_added_hidden_and_only_unused_ones_deleted()
    {
        var category = await SendAsync(HttpMethod.Post, "/api/purchase-categories", new { name = "Home" });
        var id = category.GetProperty("id").GetGuid();
        var duplicate = await Client.PostAsJsonAsync("/api/purchase-categories", new { name = "HOME" }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);

        await SendAsync(HttpMethod.Post, "/api/card-purchases", Purchase("Sofa", 90_000, installments: 12, categoryId: id));
        var delete = await Client.DeleteAsync(new Uri($"/api/purchase-categories/{id}", UriKind.Relative), Ct);
        Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);

        var hidden = await SendAsync(HttpMethod.Post, $"/api/purchase-categories/{id}/hide");
        Assert.True(hidden.GetProperty("isHidden").GetBoolean());
        Assert.Equal(0, (await GetAsync("/api/purchase-categories")).GetArrayLength());
    }

    [Fact]
    public async Task Database_rejects_a_payment_day_not_after_the_statement_day()
    {
        var error = await Assert.ThrowsAsync<PostgresException>(() =>
            ExecuteSqlAsync($"UPDATE card_billing_terms SET payment_day = 5 WHERE card_id = '{_card}';"));

        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
    }

    private object Purchase(
        string description, long amountCents, int? installments, bool postpone = false, Guid? categoryId = null, Guid? cardId = null) =>
        new
        {
            cardId = cardId ?? _card,
            description,
            purchaseDate = Iso(NextMonth),
            amountCents,
            inInstallments = installments is not null,
            installmentCount = installments,
            postponeFirst = postpone,
            categoryId,
        };

    private static object Card(string name, string type, bool isPrimary = false, bool allowsInstallments = false, Guid? payingAccountId = null) =>
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
            allowsInstallments,
            statementDay = payingAccountId is null ? (int?)null : 5,
            paymentDay = payingAccountId is null ? (int?)null : 20,
            payingAccountId,
            paysFromAccountDirectly = false,
            maxInstallments = payingAccountId is null ? (int?)null : 36,
        };

    private async Task<Guid> CreateCardAsync(
        string name, string type, bool isPrimary = false, bool allowsInstallments = false, Guid? payingAccountId = null)
    {
        var response = await Client.PostAsJsonAsync("/api/cards", Card(name, type, isPrimary, allowsInstallments, payingAccountId), Ct);
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
