using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Npgsql;

namespace Monthwise.Api.Tests;

/// <summary>
/// Card endpoints against a real PostgreSQL (skipped without MONTHWISE_TEST_DB). The tests in this
/// class run one after another and each starts from an empty card table.
/// </summary>
[Collection(SharedDatabase.Name)]
public sealed class CardEndpointTests : IAsyncLifetime
{
    // Published test values only; never real card numbers or IBANs.
    private const string Visa = "4111 1111 1111 1111";
    private const string Mastercard = "5555555555554444";
    private const string CroatianIban = "HR1210010051863000160";
    private const string GermanIban = "DE89370400440532013000";
    private static readonly Guid VisaIssuer = new("0199b0c0-0000-7000-8000-000000000001");
    private static readonly Guid ZaBa = new("0199b0c0-0000-7000-8000-000000000101");

    private MonthwiseApiFactory? _factory;
    private HttpClient? _client;
    private string _connectionString = string.Empty;

    private HttpClient Client => _client!;
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _connectionString = MonthwiseApiFactory.TestDatabaseConnectionString ?? string.Empty;
        Assert.SkipWhen(_connectionString.Length == 0, "MONTHWISE_TEST_DB is not set; no test database available.");

        _factory = new MonthwiseApiFactory(_connectionString, migrateOnStartup: true);
        _client = _factory.CreateClient();
        // Starting the host applies the migrations.
        await Client.GetAsync(new Uri("/api/health", UriKind.Relative), Ct);
        await ExecuteSqlAsync(CashFlowEndpointTests.DeleteAllSql);
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
    public async Task Dashboard_reports_when_no_cards_exist()
    {
        var dashboard = await GetJsonAsync("/api/cards/dashboard");

        Assert.False(dashboard.GetProperty("hasAnyCards").GetBoolean());
        Assert.Empty(dashboard.GetProperty("cards").EnumerateArray());
    }

    [Fact]
    public async Task Create_read_update_round_trip_with_masked_numbers()
    {
        var created = await CreateCardAsync(Card("Family Visa", cardNumber: Visa, iban: CroatianIban, debtLimitCents: 150_000));

        Assert.Equal("•••• 1111", created.GetProperty("cardNumberMasked").GetString());
        Assert.Equal("HR•• … 0160", created.GetProperty("ibanMasked").GetString());
        Assert.Equal("Visa", created.GetProperty("issuer").GetProperty("name").GetString());
        Assert.Equal("Active", created.GetProperty("status").GetString());
        Assert.Equal(150_000, created.GetProperty("debtLimitCents").GetInt64());
        var id = created.GetProperty("id").GetGuid();

        var list = await GetJsonAsync("/api/cards");
        Assert.Equal(id, Assert.Single(list.EnumerateArray()).GetProperty("id").GetGuid());
        var dashboard = await GetJsonAsync("/api/cards/dashboard");
        Assert.True(dashboard.GetProperty("hasAnyCards").GetBoolean());
        Assert.Single(dashboard.GetProperty("cards").EnumerateArray());

        // Edit: rename, keep the card number (null), remove the IBAN (empty string).
        var update = await Client.PutAsJsonAsync($"/api/cards/{id}", Card("Visa Gold", cardNumber: null, iban: ""), Ct);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await update.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("Visa Gold", updated.GetProperty("name").GetString());
        Assert.Equal("•••• 1111", updated.GetProperty("cardNumberMasked").GetString());
        Assert.Equal(JsonValueKind.Null, updated.GetProperty("ibanMasked").ValueKind);
    }

    [Fact]
    public async Task A_card_can_be_saved_without_numbers_and_valid_from()
    {
        var created = await CreateCardAsync(Card("Bare card", cardNumber: null, iban: null, validFrom: null));

        Assert.Equal(JsonValueKind.Null, created.GetProperty("cardNumberMasked").ValueKind);
        Assert.Equal(JsonValueKind.Null, created.GetProperty("ibanMasked").ValueKind);
        Assert.Equal(JsonValueKind.Null, created.GetProperty("validFrom").ValueKind);
        Assert.Equal("Active", created.GetProperty("status").GetString());
        var id = created.GetProperty("id").GetGuid();

        // Several cards may have no numbers: only given numbers must be unique.
        await CreateCardAsync(Card("Second bare card", cardNumber: null, iban: null, validFrom: null));

        var reveal = await Client.GetAsync(new Uri($"/api/cards/{id}/numbers/CardNumber", UriKind.Relative), Ct);
        Assert.Equal(HttpStatusCode.NotFound, reveal.StatusCode);
    }

    [Fact]
    public async Task Debit_cards_keep_the_installments_check_box_and_other_types_derive_it()
    {
        var paying = await PayingAccountAsync();
        var debit = await CreateCardAsync(Card("Tekući", cardNumber: Visa, type: "Debit", allowsInstallments: true, payingAccountId: paying));
        var plainDebit = await CreateCardAsync(Card("Plain debit", cardNumber: Mastercard, type: "Debit"));
        var credit = await CreateCardAsync(Card("Credit", cardNumber: null, iban: GermanIban, type: "Credit", payingAccountId: paying));
        var prepaid = await CreateCardAsync(Card("Prepaid", cardNumber: null, iban: CroatianIban, type: "Prepaid"));

        Assert.True(debit.GetProperty("allowsInstallments").GetBoolean());
        Assert.False(plainDebit.GetProperty("allowsInstallments").GetBoolean());
        Assert.True(credit.GetProperty("allowsInstallments").GetBoolean());
        Assert.False(prepaid.GetProperty("allowsInstallments").GetBoolean());

        var id = plainDebit.GetProperty("id").GetGuid();
        var update = await Client.PutAsJsonAsync(
            $"/api/cards/{id}", Card("Plain debit", cardNumber: null, type: "Debit", allowsInstallments: true, payingAccountId: paying), Ct);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.True((await update.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("allowsInstallments").GetBoolean());
    }

    [Fact]
    public async Task Numbers_are_stored_encrypted_and_revealed_one_at_a_time_without_caching()
    {
        var id = (await CreateCardAsync(Card("Family Visa", cardNumber: Visa, iban: CroatianIban))).GetProperty("id").GetGuid();

        await using (var connection = new NpgsqlConnection(_connectionString))
        {
            await connection.OpenAsync(Ct);
            await using var command = new NpgsqlCommand("SELECT card_number, iban FROM card WHERE id = @id", connection);
            command.Parameters.AddWithValue("id", id);
            await using var reader = await command.ExecuteReaderAsync(Ct);
            Assert.True(await reader.ReadAsync(Ct));
            Assert.StartsWith("v1:", reader.GetString(0), StringComparison.Ordinal);
            Assert.DoesNotContain("1111", reader.GetString(0), StringComparison.Ordinal);
            Assert.DoesNotContain("HR12", reader.GetString(1), StringComparison.Ordinal);
        }

        var response = await Client.GetAsync(new Uri($"/api/cards/{id}/numbers/CardNumber", UriKind.Relative), Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var revealed = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("4111 1111 1111 1111", revealed.GetProperty("value").GetString());
        Assert.DoesNotContain("HR", await response.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);

        var iban = await GetJsonAsync($"/api/cards/{id}/numbers/Iban");
        Assert.Equal("HR12 1001 0051 8630 0016 0", iban.GetProperty("value").GetString());
    }

    [Fact]
    public async Task Invalid_input_is_reported_per_field_without_echoing_the_number()
    {
        const string wrongNumber = "4111111111111112";

        var response = await Client.PostAsJsonAsync("/api/cards", Card("", cardNumber: wrongNumber, validFrom: "2026-13"), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(Ct);
        var errors = JsonDocument.Parse(body).RootElement.GetProperty("errors");
        Assert.True(errors.TryGetProperty("cardNumber", out _));
        Assert.True(errors.TryGetProperty("validFrom", out _));
        Assert.DoesNotContain(wrongNumber, body, StringComparison.Ordinal);
        Assert.DoesNotContain(_factory!.LogMessages, message => message.Contains(wrongNumber, StringComparison.Ordinal));
        Assert.DoesNotContain(_factory.LogMessages, message => message.Contains("41111111111111", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Business_rules_are_reported_per_field()
    {
        var response = await Client.PostAsJsonAsync(
            "/api/cards",
            Card("Bad", cardNumber: null, iban: null, validFrom: "2026-05", validThrough: "2026-04", type: "Credit", isPrimary: true, debtLimitCents: -1, allowsInstallments: true),
            Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("errors");
        Assert.False(errors.TryGetProperty("cardNumber", out _), "A card needs no number.");
        foreach (var field in new[] { "validThrough", "debtLimitCents", "isPrimary", "allowsInstallments", "statementDay" })
        {
            Assert.True(errors.TryGetProperty(field, out _), $"Expected an error for {field}.");
        }
    }

    [Fact]
    public async Task Names_card_numbers_and_ibans_are_unique()
    {
        await CreateCardAsync(Card("Family Visa", cardNumber: Visa, iban: CroatianIban));

        var response = await Client.PostAsJsonAsync("/api/cards", Card("FAMILY visa", cardNumber: "4111111111111111", iban: "hr12 1001 0051 8630 0016 0"), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("errors");
        Assert.True(errors.TryGetProperty("name", out _));
        Assert.True(errors.TryGetProperty("cardNumber", out _));
        Assert.True(errors.TryGetProperty("iban", out _));
    }

    [Fact]
    public async Task Archived_cards_free_their_numbers_but_keep_their_name()
    {
        var id = (await CreateCardAsync(Card("Old Visa", cardNumber: Visa))).GetProperty("id").GetGuid();
        await PostAsync($"/api/cards/{id}/archive");

        await CreateCardAsync(Card("New Visa", cardNumber: Visa));
        var sameName = await Client.PostAsJsonAsync("/api/cards", Card("Old Visa", cardNumber: Mastercard), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, sameName.StatusCode);
    }

    [Fact]
    public async Task Block_and_unblock()
    {
        var id = (await CreateCardAsync(Card("Family Visa", cardNumber: Visa))).GetProperty("id").GetGuid();

        var blocked = await PostAsync($"/api/cards/{id}/block", new { reason = "lost" });
        Assert.Equal("Blocked", blocked.GetProperty("status").GetString());
        Assert.Equal("lost", blocked.GetProperty("blockReason").GetString());
        Assert.False(blocked.GetProperty("canBeUsed").GetBoolean());
        Assert.Empty((await GetJsonAsync("/api/cards/dashboard")).GetProperty("cards").EnumerateArray());
        Assert.Single((await GetJsonAsync("/api/cards")).EnumerateArray());

        var edit = await Client.PutAsJsonAsync($"/api/cards/{id}", Card("Renamed", cardNumber: null), Ct);
        Assert.Equal(HttpStatusCode.Conflict, edit.StatusCode);

        var unblocked = await PostAsync($"/api/cards/{id}/unblock");
        Assert.Equal("Active", unblocked.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Expire_is_final_links_the_replacement_and_hides_the_card()
    {
        var oldId = (await CreateCardAsync(Card("Old Visa", cardNumber: Visa, type: "Debit", isPrimary: true))).GetProperty("id").GetGuid();
        var newId = (await CreateCardAsync(Card("New Visa", cardNumber: Mastercard))).GetProperty("id").GetGuid();

        var expired = await PostAsync($"/api/cards/{oldId}/expire", new { reason = "replaced by bank", replacementCardId = newId });

        Assert.Equal("Expired", expired.GetProperty("status").GetString());
        Assert.False(expired.GetProperty("isPrimary").GetBoolean());
        Assert.Equal(newId, expired.GetProperty("replacedBy").GetProperty("id").GetGuid());
        var replacement = await GetJsonAsync($"/api/cards/{newId}");
        Assert.Equal(oldId, replacement.GetProperty("replaces").GetProperty("id").GetGuid());

        var again = await Client.PostAsJsonAsync($"/api/cards/{oldId}/expire", new { reason = (string?)null }, Ct);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);

        Assert.Equal(newId, Assert.Single((await GetJsonAsync("/api/cards")).EnumerateArray()).GetProperty("id").GetGuid());
        Assert.Equal(oldId, Assert.Single((await GetJsonAsync("/api/cards?show=Expired")).EnumerateArray()).GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task A_new_card_can_be_added_as_the_replacement_of_an_expired_one()
    {
        var oldId = (await CreateCardAsync(Card("Old Visa", cardNumber: Visa))).GetProperty("id").GetGuid();
        await PostAsync($"/api/cards/{oldId}/expire", new { reason = "damaged" });

        var response = await Client.PostAsJsonAsync($"/api/cards?replaces={oldId}", Card("New Visa", cardNumber: Mastercard), Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(oldId, created.GetProperty("replaces").GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Archive_is_final_and_shown_only_under_the_archived_filter()
    {
        var id = (await CreateCardAsync(Card("Family Visa", cardNumber: Visa))).GetProperty("id").GetGuid();

        var archived = await PostAsync($"/api/cards/{id}/archive");

        Assert.Equal("Archived", archived.GetProperty("status").GetString());
        Assert.Empty((await GetJsonAsync("/api/cards")).EnumerateArray());
        Assert.Single((await GetJsonAsync("/api/cards?show=Archived")).EnumerateArray());
        var dashboard = await GetJsonAsync("/api/cards/dashboard");
        Assert.True(dashboard.GetProperty("hasAnyCards").GetBoolean());
        Assert.Empty(dashboard.GetProperty("cards").EnumerateArray());
        var unblock = await Client.PostAsync(new Uri($"/api/cards/{id}/unblock", UriKind.Relative), null, Ct);
        Assert.Equal(HttpStatusCode.Conflict, unblock.StatusCode);
    }

    [Fact]
    public async Task Primary_cards_are_listed_first()
    {
        await CreateCardAsync(Card("A credit card", cardNumber: Mastercard));
        await CreateCardAsync(Card("Z current account", cardNumber: null, iban: GermanIban, type: "Debit", isPrimary: true));

        var names = (await GetJsonAsync("/api/cards")).EnumerateArray().Select(c => c.GetProperty("name").GetString()).ToArray();

        Assert.Equal(["Z current account", "A credit card"], names);
    }

    [Fact]
    public async Task Unknown_cards_return_not_found()
    {
        var response = await Client.GetAsync(new Uri($"/api/cards/{Guid.NewGuid()}", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Issuer_and_bank_lists_are_seeded_and_extendable()
    {
        var issuers = await GetJsonAsync("/api/card-issuers");
        Assert.Contains(issuers.EnumerateArray(), i => i.GetProperty("name").GetString() == "Visa");
        var banks = await GetJsonAsync("/api/banks");
        Assert.Contains(banks.EnumerateArray(), b => b.GetProperty("name").GetString() == "ZaBa");

        var name = $"Test bank {Guid.NewGuid():N}"[..30];
        var added = await Client.PostAsJsonAsync("/api/banks", new { name }, Ct);
        Assert.Equal(HttpStatusCode.OK, added.StatusCode);
        Assert.False((await added.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("isPredefined").GetBoolean());

        var duplicate = await Client.PostAsJsonAsync("/api/banks", new { name = name.ToUpperInvariant() }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
    }

    [Fact]
    public async Task Database_rejects_a_non_debit_primary_card()
    {
        var paying = await PayingAccountAsync();
        var id = (await CreateCardAsync(Card("Family Visa", cardNumber: Visa, type: "Credit", payingAccountId: paying))).GetProperty("id").GetGuid();

        var error = await Assert.ThrowsAsync<PostgresException>(() =>
            ExecuteSqlAsync($"UPDATE card SET is_primary = true WHERE id = '{id}';"));

        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
    }

    private static object Card(
        string name,
        string? cardNumber = Visa,
        string? iban = null,
        string? validFrom = "2025-01",
        string validThrough = "2029-12",
        string type = "Debit",
        bool isPrimary = false,
        long? debtLimitCents = null,
        bool allowsInstallments = false,
        Guid? payingAccountId = null) =>
        new
        {
            name,
            issuerId = VisaIssuer,
            bankId = ZaBa,
            cardNumber,
            iban,
            validFrom,
            validThrough,
            type,
            isPrimary,
            debtLimitCents,
            allowsInstallments,
            // A card that allows installments needs its billing details (installments FR-1).
            statementDay = payingAccountId is null ? (int?)null : 5,
            paymentDay = payingAccountId is null ? (int?)null : 20,
            payingAccountId,
            paysFromAccountDirectly = false,
            maxInstallments = payingAccountId is null ? (int?)null : 36,
        };

    /// <summary>A primary account that cards allowing installments can pay from.</summary>
    private async Task<Guid> PayingAccountAsync() =>
        (await CreateCardAsync(Card("Paying account", cardNumber: null, iban: null, type: "Debit", isPrimary: true))).GetProperty("id").GetGuid();

    private async Task<JsonElement> CreateCardAsync(object card)
    {
        var response = await Client.PostAsJsonAsync("/api/cards", card, Ct);
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    private async Task<JsonElement> PostAsync(string path, object? body = null)
    {
        var response = body is null
            ? await Client.PostAsync(new Uri(path, UriKind.Relative), null, Ct)
            : await Client.PostAsJsonAsync(path, body, Ct);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    private async Task<JsonElement> GetJsonAsync(string path)
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
