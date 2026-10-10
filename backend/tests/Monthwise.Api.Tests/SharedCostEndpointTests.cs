using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Npgsql;

namespace Monthwise.Api.Tests;

/// <summary>
/// Participants, shared bills and loans and their contributions against a real PostgreSQL (skipped
/// without MONTHWISE_TEST_DB). Each test starts from empty tables with one primary account.
/// </summary>
[Collection(SharedDatabase.Name)]
public sealed class SharedCostEndpointTests : IAsyncLifetime
{
    private static readonly Guid VisaIssuer = new("0199b0c0-0000-7000-8000-000000000001");
    private static readonly Guid ZaBa = new("0199b0c0-0000-7000-8000-000000000101");
    private static readonly Guid AgreedContributions = new("0199b0c0-0000-7000-8000-000000000203");
    private static readonly Guid BankLoan = new("0199c2a0-0000-7000-8000-000000000301");
    private static readonly Guid CashLoan = new("0199c2a0-0000-7000-8000-000000000312");
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.Now);
    private static readonly DateOnly MonthStart = new(Today.Year, Today.Month, 1);

    private MonthwiseApiFactory? _factory;
    private HttpClient? _client;
    private string _connectionString = string.Empty;
    private Guid _account;
    private Guid _electricity;

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
        _electricity = (await GetAsync("/api/expense-categories")).EnumerateArray()
            .Single(c => c.GetProperty("name").GetString() == "Electricity").GetProperty("id").GetGuid();
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
    public async Task Participants_are_unique_payers_that_can_only_be_archived_once_they_share_a_cost()
    {
        var ana = await CreateParticipantAsync("Ana");
        var duplicate = await Client.PostAsJsonAsync("/api/participants", new { name = "ANA", description = (string?)null }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        Assert.Contains("Ana", (await GetAsync("/api/payers")).EnumerateArray().Select(p => p.GetProperty("name").GetString()));

        var renamed = await SendAsync(HttpMethod.Put, $"/api/participants/{ana}", new { name = "Ana M.", description = "Sister" });
        Assert.Equal("Ana M.", renamed.GetProperty("name").GetString());
        var payers = (await GetAsync("/api/payers")).EnumerateArray().Select(p => p.GetProperty("name").GetString()).ToList();
        Assert.Contains("Ana M.", payers);
        Assert.DoesNotContain("Ana", payers);

        await SendAsync(HttpMethod.Post, "/api/recurring-bills", Recurring(Share(ana)));
        var delete = await Client.DeleteAsync(new Uri($"/api/participants/{ana}", UriKind.Relative), Ct);
        Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);

        var archived = await SendAsync(HttpMethod.Post, $"/api/participants/{ana}/archive");
        Assert.True(archived.GetProperty("isArchived").GetBoolean());
        Assert.Empty((await GetAsync("/api/participants")).EnumerateArray());
        var refused = await Client.PostAsJsonAsync("/api/recurring-bills", Recurring(Share(ana), "Water"), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);

        var ivo = await CreateParticipantAsync("Ivo");
        var removed = await Client.DeleteAsync(new Uri($"/api/participants/{ivo}", UriKind.Relative), Ct);
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
    }

    [Fact]
    public async Task Invalid_shares_return_field_errors()
    {
        var ana = await CreateParticipantAsync("Ana");

        var tooMany = await Client.PostAsJsonAsync("/api/recurring-bills", Recurring(Share(ana, shares: 5)), Ct);
        var allShares = await Client.PostAsJsonAsync("/api/recurring-bills", Recurring(Share(ana, shares: 2, participantShares: 2)), Ct);
        var unknown = await Client.PostAsJsonAsync("/api/recurring-bills", Recurring(Share(Guid.NewGuid())), Ct);

        Assert.Contains("share.shares", await ErrorFieldsAsync(tooMany));
        Assert.Contains("share.participantShares", await ErrorFieldsAsync(allShares));
        Assert.Contains("share.participantId", await ErrorFieldsAsync(unknown));
    }

    [Fact]
    public async Task An_issued_shared_bill_has_one_expected_contribution_that_follows_it_until_received()
    {
        var ana = await CreateParticipantAsync("Ana");
        await SendAsync(HttpMethod.Post, "/api/recurring-bills", Recurring(Share(ana)));
        var month = Month(MonthStart);
        await SendAsync(HttpMethod.Post, $"/api/bills/add-recurring?month={month}");
        var bill = Assert.Single((await GetAsync($"/api/bills?month={month}")).GetProperty("bills").EnumerateArray());
        var id = bill.GetProperty("id").GetGuid();
        Assert.Equal(2, bill.GetProperty("share").GetProperty("shares").GetInt32());
        Assert.Equal(JsonValueKind.Null, bill.GetProperty("contribution").ValueKind);
        Assert.Empty(Contributions(await GetAsync($"/api/inputs?month={month}")));

        // Issued: 100.01 split in two, Ana pays 50.01 (half a cent rounded up).
        await SendAsync(HttpMethod.Put, $"/api/bills/{id}", Bill(10_001, Share(ana)));
        var bills = await GetAsync($"/api/bills?month={month}");
        Assert.Equal(5_000, bills.GetProperty("ownPartCents").GetInt64());
        bill = Assert.Single(bills.GetProperty("bills").EnumerateArray());
        Assert.Equal(5_000, bill.GetProperty("ownPartCents").GetInt64());
        var contribution = Assert.Single(Contributions(await GetAsync($"/api/inputs?month={month}")));
        Assert.Equal(JsonValueKind.Null, contribution.GetProperty("id").ValueKind);
        Assert.Equal(5_001, contribution.GetProperty("amountCents").GetInt64());
        Assert.Equal("Ana", contribution.GetProperty("payer").GetProperty("name").GetString());
        Assert.Equal(AgreedContributions, contribution.GetProperty("category").GetProperty("id").GetGuid());
        Assert.Equal(_account, contribution.GetProperty("account").GetProperty("id").GetGuid());
        Assert.Equal(id, contribution.GetProperty("contribution").GetProperty("billId").GetGuid());

        // The expected contribution follows the bill.
        await SendAsync(HttpMethod.Put, $"/api/bills/{id}", Bill(12_000, Share(ana)));
        contribution = Assert.Single(Contributions(await GetAsync($"/api/inputs?month={month}")));
        Assert.Equal(6_000, contribution.GetProperty("amountCents").GetInt64());

        // Received, it is stored and no longer changes with the bill; reset gives it the new part.
        var received = await SendAsync(HttpMethod.Post, $"/api/bills/{id}/contribution/received", new { receivedDate = Iso(MonthStart.AddDays(16)), accountId = (Guid?)null });
        Assert.True(received.GetProperty("isReceived").GetBoolean());
        await SendAsync(HttpMethod.Put, $"/api/bills/{id}", Bill(13_000, Share(ana)));
        var stored = (await GetAsync($"/api/bills/{id}")).GetProperty("contribution");
        Assert.Equal(6_000, stored.GetProperty("amountCents").GetInt64());
        Assert.Equal(6_500, stored.GetProperty("partCents").GetInt64());
        var reset = await SendAsync(HttpMethod.Post, $"/api/bills/{id}/contribution/reset");
        Assert.Equal(6_500, reset.GetProperty("amountCents").GetInt64());

        // A contribution cannot be deleted; deleting the bill keeps the received one as an ordinary input.
        var inputId = reset.GetProperty("inputId").GetGuid();
        var deleteInput = await Client.DeleteAsync(new Uri($"/api/inputs/{inputId}", UriKind.Relative), Ct);
        Assert.Equal(HttpStatusCode.Conflict, deleteInput.StatusCode);
        var deleteBill = await Client.DeleteAsync(new Uri($"/api/bills/{id}", UriKind.Relative), Ct);
        Assert.Equal(HttpStatusCode.NoContent, deleteBill.StatusCode);
        var kept = Assert.Single((await GetAsync($"/api/inputs?month={month}")).GetProperty("inputs").EnumerateArray());
        Assert.Equal(inputId, kept.GetProperty("id").GetGuid());
        Assert.Equal(JsonValueKind.Null, kept.GetProperty("contribution").ValueKind);
    }

    [Fact]
    public async Task An_edited_contribution_keeps_its_values_and_stopping_the_share_removes_an_expected_one()
    {
        var ana = await CreateParticipantAsync("Ana");
        var month = Month(MonthStart);
        var bill = await SendAsync(HttpMethod.Post, $"/api/bills?month={month}", Bill(9_000, Share(ana, shares: 3)));
        var id = bill.GetProperty("id").GetGuid();
        Assert.Equal(3_000, bill.GetProperty("contribution").GetProperty("partCents").GetInt64());

        var edited = await SendAsync(HttpMethod.Put, $"/api/bills/{id}/contribution", new
        {
            accountId = _account,
            amountCents = 2_900,
            expectedDate = Iso(MonthStart.AddDays(20)),
            receivedDate = (string?)null,
            description = "Electricity, rounded",
        });
        Assert.True(edited.GetProperty("isEdited").GetBoolean());
        await SendAsync(HttpMethod.Put, $"/api/bills/{id}", Bill(9_900, Share(ana, shares: 3)));
        var contribution = Assert.Single(Contributions(await GetAsync($"/api/inputs?month={month}")));
        Assert.Equal(2_900, contribution.GetProperty("amountCents").GetInt64());
        Assert.Equal(3_300, contribution.GetProperty("contribution").GetProperty("partCents").GetInt64());

        await SendAsync(HttpMethod.Put, $"/api/bills/{id}", Bill(9_900, share: null));
        Assert.Empty(Contributions(await GetAsync($"/api/inputs?month={month}")));
    }

    [Fact]
    public async Task A_shared_loan_shares_its_payments_and_one_payment_can_be_shared_differently()
    {
        var ana = await CreateParticipantAsync("Ana");
        var loan = await SendAsync(HttpMethod.Post, "/api/loans", Loan(Share(ana)));
        var loanId = loan.GetProperty("loan").GetProperty("id").GetGuid();
        Assert.Equal("Ana", loan.GetProperty("loan").GetProperty("share").GetProperty("participant").GetProperty("name").GetString());

        var month = Month(MonthStart);
        var payments = await GetAsync($"/api/loan-payments?month={month}");
        var payment = Assert.Single(payments.GetProperty("payments").EnumerateArray());
        Assert.Equal(25_000, payment.GetProperty("ownPartCents").GetInt64());
        Assert.Equal(25_000, payments.GetProperty("ownPartCents").GetInt64());
        Assert.Equal(25_000, payment.GetProperty("contribution").GetProperty("partCents").GetInt64());
        var contribution = Assert.Single(Contributions(await GetAsync($"/api/inputs?month={month}")));
        Assert.Equal("Apartment 1/12", contribution.GetProperty("description").GetString());

        var path = $"/api/loans/{loanId}/payments/{month}/Regular";
        var notShared = await SendAsync(HttpMethod.Put, $"{path}/share", new { share = (object?)null });
        Assert.True(notShared.GetProperty("isShareChanged").GetBoolean());
        Assert.Equal(JsonValueKind.Null, notShared.GetProperty("share").ValueKind);
        Assert.Empty(Contributions(await GetAsync($"/api/inputs?month={month}")));

        var reset = await SendAsync(HttpMethod.Delete, $"{path}/share");
        Assert.False(reset.GetProperty("isShareChanged").GetBoolean());
        Assert.Single(Contributions(await GetAsync($"/api/inputs?month={month}")));

        await SendAsync(HttpMethod.Post, $"{path}/contribution/received");
        var overview = await GetAsync($"/api/shared-costs?month={month}");
        var person = Assert.Single(overview.GetProperty("participants").EnumerateArray());
        Assert.Equal("Apartment", Assert.Single(person.GetProperty("costs").EnumerateArray()).GetProperty("name").GetString());
        Assert.Equal(25_000, person.GetProperty("monthReceivedCents").GetInt64());
        Assert.Equal(0, person.GetProperty("outstandingCents").GetInt64());

        var report = await GetAsync($"/api/cash-flow/report?month={month}");
        var shared = report.GetProperty("sharedCosts");
        Assert.Equal(50_000, shared.GetProperty("totalCents").GetInt64());
        Assert.Equal(25_000, shared.GetProperty("ownPartCents").GetInt64());
        Assert.Equal(25_000, shared.GetProperty("contributionsReceivedCents").GetInt64());

        // Stopping to share the loan keeps the received contribution as an ordinary input.
        await SendAsync(HttpMethod.Put, $"/api/loans/{loanId}", Loan(share: null));
        var inputs = (await GetAsync($"/api/inputs?month={month}")).GetProperty("inputs").EnumerateArray().ToList();
        var kept = Assert.Single(inputs);
        Assert.True(kept.GetProperty("isReceived").GetBoolean());
        Assert.Equal(JsonValueKind.Null, kept.GetProperty("contribution").ValueKind);
    }

    [Fact]
    public async Task The_database_refuses_invalid_shares()
    {
        var ana = await CreateParticipantAsync("Ana");
        var bill = await SendAsync(HttpMethod.Post, "/api/recurring-bills", Recurring(Share(ana)));
        var id = bill.GetProperty("id").GetGuid();

        var tooMany = await Assert.ThrowsAsync<PostgresException>(() =>
            ExecuteSqlAsync($"UPDATE recurring_bill SET share_count = 5 WHERE id = '{id}'"));
        var allShares = await Assert.ThrowsAsync<PostgresException>(() =>
            ExecuteSqlAsync($"UPDATE recurring_bill SET participant_share_count = 2 WHERE id = '{id}'"));
        var halfSet = await Assert.ThrowsAsync<PostgresException>(() =>
            ExecuteSqlAsync($"UPDATE recurring_bill SET share_participant_id = NULL WHERE id = '{id}'"));

        Assert.All(new[] { tooMany, allShares, halfSet }, e => Assert.Equal("ck_recurring_bill_share", e.ConstraintName));
    }

    private static List<JsonElement> Contributions(JsonElement monthInputs) =>
        monthInputs.GetProperty("inputs").EnumerateArray().Where(i => i.GetProperty("contribution").ValueKind != JsonValueKind.Null).ToList();

    private static object Share(Guid participantId, int shares = 2, int participantShares = 1) =>
        new { participantId, shares, participantShares, contributionAccountId = (Guid?)null };

    private object Recurring(object? share, string name = "Electricity, apartment") =>
        new { name, categoryId = _electricity, payee = (string?)null, paidFromId = _account, description = (string?)null, share };

    private object Bill(long amountCents, object? share) =>
        new
        {
            name = "Electricity, apartment",
            categoryId = _electricity,
            payee = (string?)null,
            amountCents,
            issueDate = Iso(MonthStart),
            dueDate = Iso(MonthStart.AddDays(14)),
            paidFromId = _account,
            description = (string?)null,
            paidDate = (string?)null,
            share,
        };

    private static object Loan(object? share) =>
        new
        {
            name = "Apartment",
            categoryId = BankLoan,
            subcategoryId = CashLoan,
            lenderId = ZaBa,
            paymentMethod = "Bill",
            accountId = (Guid?)null,
            startMonth = Month(MonthStart),
            paymentDay = 15,
            monthlyPaymentCents = 50_000,
            paymentCount = 12,
            endMonth = (string?)null,
            residualValueCents = (long?)null,
            description = (string?)null,
            share,
            keepChangedPaymentShares = true,
        };

    private async Task<Guid> CreateParticipantAsync(string name)
    {
        var response = await Client.PostAsJsonAsync("/api/participants", new { name, description = (string?)null }, Ct);
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
    }

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
            statementDay = (int?)null,
            paymentDay = (int?)null,
            payingAccountId = (Guid?)null,
            paysFromAccountDirectly = false,
            maxInstallments = (int?)null,
        }, Ct);
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
    }

    private static async Task<IReadOnlyList<string>> ErrorFieldsAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        return problem.GetProperty("errors").EnumerateObject().Select(p => p.Name).ToList();
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
        Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
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
