using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Npgsql;

namespace Monthwise.Api.Tests;

/// <summary>
/// Each table is read once per request (repeated loading FR-2 to FR-6), against a real PostgreSQL.
/// The heavy requests are measured on three years of made-up history; set MONTHWISE_MEASURE_OUT to a
/// file name to write the measurements there.
/// </summary>
[Collection(SharedDatabase.Name)]
public sealed partial class RequestLoadingTests : IAsyncLifetime
{
    private const int HistoryMonths = 36;
    private const string ExportPassword = "secret-phrase-1";
    private static readonly Guid VisaIssuer = new("0199b0c0-0000-7000-8000-000000000001");
    private static readonly Guid ZaBa = new("0199b0c0-0000-7000-8000-000000000101");
    private static readonly Guid Salary = new("0199b0c0-0000-7000-8000-000000000201");
    private static readonly Guid BankLoan = new("0199c2a0-0000-7000-8000-000000000301");
    private static readonly Guid CashLoan = new("0199c2a0-0000-7000-8000-000000000312");
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.Now);
    private static readonly DateOnly MonthStart = new(Today.Year, Today.Month, 1);

    private readonly StatementCounter _counter = new();
    private MonthwiseApiFactory? _factory;
    private HttpClient? _client;
    private string _connectionString = string.Empty;

    private HttpClient Client => _client!;
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _connectionString = TestDatabase.ConnectionString;
        _factory = new MonthwiseApiFactory(_connectionString, migrateOnStartup: true);
        _client = _factory.CreateClient();
        await Client.GetAsync(new Uri("/api/health", UriKind.Relative), Ct);
        await ExecuteSqlAsync(CashFlowEndpointTests.DeleteAllSql + " DELETE FROM data_transfer_log; DELETE FROM data_safety_copy;");
    }

    public async ValueTask DisposeAsync()
    {
        _counter.Dispose();
        _client?.Dispose();
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
    }

    /// <summary>The statement limits of repeated loading FR-5, by request.</summary>
    public static TheoryData<string, int> HeavyRequests => new()
    {
        { "/api/dashboard", 40 },
        { $"/api/cash-flow/report?month={Month(MonthStart)}", 30 },
        { $"/api/cash-flow/chart?from={Iso(MonthStart.AddMonths(-11))}&to={Iso(MonthStart.AddMonths(1).AddDays(-1))}&granularity=Month", 30 },
    };

    [Theory]
    [MemberData(nameof(HeavyRequests))]
    public async Task A_heavy_request_reads_each_table_once_on_three_years_of_history(string path, int statementLimit)
    {
        var empty = await MeasureAsync(path);
        await SeedHistoryAsync();
        await MeasureAsync(path);
        var seeded = await MeasureAsync(path);

        if (Environment.GetEnvironmentVariable("MONTHWISE_MEASURE_OUT") is { Length: > 0 } file)
        {
            await File.AppendAllLinesAsync(file, [$"empty  {empty}", $"seeded {seeded}"], Ct);
        }
        foreach (var measurement in new[] { empty, seeded })
        {
            Assert.True(measurement.Statements <= statementLimit, measurement.ToString());
            Assert.True(measurement.WholeTableReads.All(t => t.Value == 1), measurement.ToString());
        }
    }

    [Fact]
    public async Task A_change_is_in_its_own_answer_and_in_the_next_request()
    {
        var categories = (await GetAsync("/api/expense-categories")).EnumerateArray().ToList();
        var electricity = categories.Single(c => c.GetProperty("name").GetString() == "Electricity").GetProperty("id").GetGuid();
        var groceries = categories.Single(c => c.GetProperty("name").GetString() == "Groceries").GetProperty("id").GetGuid();
        var account = await CreateCardAsync(Card("Main account", "Debit", isPrimary: true));
        var credit = await CreateCardAsync(Card("Credit card", "Credit", payingAccountId: account));
        var ana = (await SendAsync(HttpMethod.Post, "/api/participants", new { name = "Ana", description = (string?)null })).GetProperty("id").GetGuid();
        var month = Month(MonthStart);

        // A bill paid: the answer and the Dashboard show it paid.
        var bill = await SendAsync(HttpMethod.Post, $"/api/bills?month={month}", new
        {
            name = "Electricity",
            categoryId = electricity,
            payee = (string?)null,
            amountCents = 8_000L,
            issueDate = Iso(MonthStart),
            dueDate = Iso(Today),
            paidFromId = account,
            description = (string?)null,
            paidDate = (string?)null,
            share = Share(ana),
        });
        var billId = bill.GetProperty("id").GetGuid();
        Assert.Equal(8_000, (await GetAsync("/api/dashboard")).GetProperty("stillToPay").GetProperty("totalCents").GetInt64());
        var paid = await SendAsync(HttpMethod.Post, $"/api/bills/{billId}/paid", new { paidDate = Iso(Today), paidFromId = (Guid?)null });
        Assert.Equal("Paid", paid.GetProperty("state").GetString());
        Assert.Equal(0, (await GetAsync("/api/dashboard")).GetProperty("stillToPay").GetProperty("totalCents").GetInt64());

        // Its shared part received: the answer and the Shared costs overview show it received.
        Assert.Equal(4_000, (await GetAsync("/api/dashboard")).GetProperty("sharedCosts").GetProperty("outstandingCents").GetInt64());
        var received = await SendAsync(HttpMethod.Post, $"/api/bills/{billId}/contribution/received", new { receivedDate = Iso(Today), accountId = (Guid?)null });
        Assert.True(received.GetProperty("isReceived").GetBoolean());
        var person = Assert.Single((await GetAsync($"/api/shared-costs?month={month}")).GetProperty("participants").EnumerateArray());
        Assert.Equal(0, person.GetProperty("outstandingCents").GetInt64());

        // A loan payment paid: the answer and the loan show one payment paid.
        var loan = await SendAsync(HttpMethod.Post, "/api/loans", new
        {
            name = "Car",
            categoryId = BankLoan,
            subcategoryId = CashLoan,
            lenderId = ZaBa,
            paymentMethod = "Bill",
            accountId = (Guid?)null,
            startMonth = month,
            paymentDay = 28,
            monthlyPaymentCents = 20_000L,
            paymentCount = 6,
            residualValueCents = (long?)null,
            description = (string?)null,
            share = (object?)null,
        });
        var loanId = loan.GetProperty("loan").GetProperty("id").GetGuid();
        Assert.Equal(120_000, loan.GetProperty("loan").GetProperty("remainingAmountCents").GetInt64());
        var payment = await SendAsync(HttpMethod.Post, $"/api/loans/{loanId}/payments/{month}/Regular/paid", new { paidDate = (string?)null, accountId = account });
        Assert.True(payment.GetProperty("isPaid").GetBoolean());
        var loanAfter = (await GetAsync($"/api/loans/{loanId}")).GetProperty("loan");
        Assert.Equal(1, loanAfter.GetProperty("paymentsPaid").GetInt32());
        Assert.Equal(100_000, loanAfter.GetProperty("remainingAmountCents").GetInt64());

        // A card purchase added: the next month's card payment has it.
        var nextMonth = Month(MonthStart.AddMonths(1));
        await SendAsync(HttpMethod.Post, "/api/card-purchases", new
        {
            cardId = credit,
            description = "Washing machine",
            purchaseDate = Iso(MonthStart.AddMonths(1)),
            amountCents = 60_000L,
            inInstallments = true,
            installmentCount = 6,
            postponeFirst = false,
            categoryId = groceries,
        });
        var cardPayments = await GetAsync($"/api/card-payments?month={nextMonth}");
        Assert.Equal(credit, Assert.Single(cardPayments.GetProperty("payments").EnumerateArray()).GetProperty("cardId").GetGuid());
        Assert.Equal(10_000, cardPayments.GetProperty("totals").GetProperty("totalCents").GetInt64());
    }

    [Fact]
    public async Task A_restored_export_file_is_what_the_next_request_shows()
    {
        await CreateCardAsync(Card("Main account", "Debit", isPrimary: true));
        var export = await Client.PostAsJsonAsync("/api/data/export", new { password = ExportPassword }, Ct);
        Assert.True(export.StatusCode == HttpStatusCode.OK, await export.Content.ReadAsStringAsync(Ct));
        var file = await export.Content.ReadAsByteArrayAsync(Ct);

        await CreateCardAsync(Card("Second account", "Debit"));
        Assert.Equal(2, (await GetAsync("/api/cards")).GetArrayLength());

        using var form = new MultipartFormDataContent();
        var content = new ByteArrayContent(file);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(content, "file", "monthwise-export.monthwise");
        form.Add(new StringContent(ExportPassword), "password");
        form.Add(new StringContent("REPLACE"), "confirmation");
        var import = await Client.PostAsync(new Uri("/api/data/import", UriKind.Relative), form, Ct);
        Assert.True(import.StatusCode == HttpStatusCode.OK, await import.Content.ReadAsStringAsync(Ct));

        var cards = (await GetAsync("/api/cards")).EnumerateArray().ToList();
        Assert.Equal("Main account", Assert.Single(cards).GetProperty("name").GetString());
    }

    private async Task<Measurement> MeasureAsync(string path)
    {
        _counter.Statements.Clear();
        var watch = Stopwatch.StartNew();
        var response = await Client.GetAsync(new Uri(path, UriKind.Relative), Ct);
        watch.Stop();
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        var statements = _counter.Statements.ToList();
        // A check (SELECT EXISTS ...) is not a read of the table's rows (FR-2).
        var reads = statements
            .Where(s => !s.StartsWith("SELECT EXISTS", StringComparison.Ordinal))
            .Select(s => FromTable().Match(s))
            .Where(m => m.Success)
            .GroupBy(m => m.Groups[1].Value)
            .ToDictionary(g => g.Key, g => g.Count());
        return new Measurement(path, statements.Count, watch.ElapsedMilliseconds, reads);
    }

    private sealed record Measurement(string Path, int Statements, long Milliseconds, IReadOnlyDictionary<string, int> WholeTableReads)
    {
        public override string ToString() =>
            $"{Path}: {Statements} statements, {Milliseconds} ms; "
            + string.Join(", ", WholeTableReads.OrderByDescending(t => t.Value).ThenBy(t => t.Key).Select(t => $"{t.Key}={t.Value}"));
    }

    private async Task SeedHistoryAsync()
    {
        var categories = (await GetAsync("/api/expense-categories")).EnumerateArray().ToList();
        Guid Category(string name) => categories.Single(c => c.GetProperty("name").GetString() == name).GetProperty("id").GetGuid();
        var electricity = Category("Electricity");
        var groceries = Category("Groceries");

        var first = MonthStart.AddMonths(-HistoryMonths + 1);
        var account = await CreateCardAsync(Card("Main account", "Debit", isPrimary: true));
        var savings = await CreateCardAsync(Card("Second account", "Debit"));
        var credit = await CreateCardAsync(Card("Credit card", "Credit", payingAccountId: account));
        var revolving = await CreateCardAsync(Revolving("Revolving card", account, first));
        var ana = (await SendAsync(HttpMethod.Post, "/api/participants", new { name = "Ana", description = (string?)null })).GetProperty("id").GetGuid();

        foreach (var (name, payment) in new[] { ("Car", 30_000L), ("Apartment", 65_000L), ("Kitchen", 12_000L) })
        {
            await SendAsync(HttpMethod.Post, "/api/loans", new
            {
                name,
                categoryId = BankLoan,
                subcategoryId = CashLoan,
                lenderId = ZaBa,
                paymentMethod = "StandingOrder",
                accountId = account,
                startMonth = Month(first),
                paymentDay = 15,
                monthlyPaymentCents = payment,
                paymentCount = 60,
                residualValueCents = (long?)null,
                description = (string?)null,
                share = name == "Apartment" ? Share(ana) : null,
            });
        }

        for (var m = 0; m < HistoryMonths; m++)
        {
            var start = first.AddMonths(m);
            var month = Month(start);
            var received = start <= Today ? start : (DateOnly?)null;
            foreach (var amount in new[] { 250_000L, 40_000L })
            {
                await SendAsync(HttpMethod.Post, "/api/inputs", new
                {
                    payerName = "Employer",
                    accountId = amount > 100_000 ? account : savings,
                    categoryId = Salary,
                    amountCents = amount,
                    issueDate = Iso(start),
                    expectedDate = Iso(start),
                    receivedDate = received is { } r ? Iso(r) : null,
                    description = (string?)null,
                    recurrence = (object?)null,
                });
            }

            var paidOn = start.AddDays(9) <= Today ? start.AddDays(9) : (DateOnly?)null;
            foreach (var (name, amount, shared) in new[] { ("Electricity", 8_000L, true), ("Water", 3_000L, false), ("Internet", 3_500L, false), ("Phone", 2_000L, false) })
            {
                await SendAsync(HttpMethod.Post, $"/api/bills?month={month}", new
                {
                    name,
                    categoryId = electricity,
                    payee = (string?)null,
                    amountCents = amount + m,
                    issueDate = Iso(start),
                    dueDate = Iso(start.AddDays(14)),
                    paidFromId = account,
                    description = (string?)null,
                    paidDate = paidOn is { } p ? Iso(p) : null,
                    share = shared ? Share(ana) : null,
                });
            }

            for (var e = 0; e < 13; e++)
            {
                var date = start.AddDays(e * 2);
                if (date > Today)
                {
                    break;
                }
                await SendAsync(HttpMethod.Post, "/api/expenses", new
                {
                    date = Iso(date),
                    amountCents = 1_000L + (e * 137),
                    categoryId = groceries,
                    paidFromId = e < 10 ? account : revolving,
                    description = (string?)null,
                });
            }

            if (start.AddDays(3) <= Today)
            {
                await SendAsync(HttpMethod.Post, "/api/card-purchases", new
                {
                    cardId = credit,
                    description = $"Purchase {month}",
                    purchaseDate = Iso(start.AddDays(3)),
                    amountCents = 60_000L,
                    inInstallments = true,
                    installmentCount = 6,
                    postponeFirst = false,
                    categoryId = groceries,
                });
            }
        }
    }

    private static object Share(Guid participantId) =>
        new { participantId, shares = 2, participantShares = 1, contributionAccountId = (Guid?)null };

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

    private static object Revolving(string name, Guid payingAccountId, DateOnly debtStart) =>
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
            payingAccountId,
            paysFromAccountDirectly = false,
            minPaymentBasisPoints = 833,
            minPaymentAmountCents = 1_327L,
            interestBasisPoints = 1_200,
            minPaymentInterestBasisPoints = (int?)null,
            billDay = 5,
            dueDay = 20,
            startingDebtCents = 100_000L,
            startingDebtDate = Iso(debtStart),
        };

    private async Task<Guid> CreateCardAsync(object card) =>
        (await SendAsync(HttpMethod.Post, "/api/cards", card)).GetProperty("id").GetGuid();

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
        Assert.True(response.IsSuccessStatusCode, $"{method} {path}: {await response.Content.ReadAsStringAsync(Ct)}");
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

    [GeneratedRegex("""FROM\s+"?(\w+)"?\s""")]
    private static partial Regex FromTable();
}
