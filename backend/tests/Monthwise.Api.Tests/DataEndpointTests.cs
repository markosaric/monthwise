using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Monthwise.Application.DataTransfer;
using Npgsql;

namespace Monthwise.Api.Tests;

/// <summary>
/// Export and import against a real PostgreSQL (skipped without MONTHWISE_TEST_DB;
/// docs/feature-requirements/backup-and-restore.md). Each test starts from empty household data
/// and an empty history.
/// </summary>
[Collection(SharedDatabase.Name)]
public sealed class DataEndpointTests : IAsyncLifetime
{
    // Published test values only; never real card numbers or IBANs.
    private const string Visa = "4111111111111111";
    private const string CroatianIban = "HR1210010051863000160";
    private const string Password = "secret1";
    private const string PreviousMigration = "20261010054541_AddSharedCosts";
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
        await Client.GetAsync(new Uri("/api/health", UriKind.Relative), Ct);
        await ExecuteSqlAsync(CashFlowEndpointTests.DeleteAllSql
            + " DELETE FROM data_transfer_log; DELETE FROM data_safety_copy;"
            + " UPDATE data_backup_settings SET reminder_enabled = true, reminder_interval_days = 30, reminder_dismissed_at = NULL;");
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
    public async Task Export_and_import_bring_back_every_table_exactly_with_card_numbers_encrypted_again()
    {
        var card = await CreateCardAsync("Family Visa", Visa, CroatianIban);
        await CreateParticipantAsync("Ana");
        var before = await SnapshotAsync();
        var storedBefore = await StoredNumbersAsync(card);

        var file = await ExportAsync();
        await ExecuteSqlAsync(CashFlowEndpointTests.DeleteAllSql);
        Assert.Empty((await GetJsonAsync("/api/cards")).EnumerateArray());

        var check = await PostFormAsync("/api/data/import/check", file, Password);
        Assert.Equal(HttpStatusCode.OK, check.StatusCode);
        var checkBody = await check.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(1, checkBody.GetProperty("file").GetProperty("cards").GetInt32());
        Assert.Equal(0, checkBody.GetProperty("current").GetProperty("cards").GetInt32());

        var import = await PostFormAsync("/api/data/import", file, Password, "REPLACE");
        Assert.Equal(HttpStatusCode.OK, import.StatusCode);

        Assert.Equal(before, await SnapshotAsync());
        // Encrypted again (a new nonce, the importing installation's key) and hashed again, not copied.
        var storedAfter = await StoredNumbersAsync(card);
        Assert.NotEqual(storedBefore.Number, storedAfter.Number);
        Assert.Equal(storedBefore.NumberHash, storedAfter.NumberHash);
        Assert.Equal("4111 1111 1111 1111", (await GetJsonAsync($"/api/cards/{card}/numbers/CardNumber")).GetProperty("value").GetString());
        Assert.Equal("HR12 1001 0051 8630 0016 0", (await GetJsonAsync($"/api/cards/{card}/numbers/Iban")).GetProperty("value").GetString());

        var health = await GetJsonAsync("/api/health");
        Assert.Equal(JsonValueKind.String, health.GetProperty("dataImportedAt").ValueKind);
    }

    [Fact]
    public async Task The_export_file_is_protected_and_holds_readable_numbers_only_inside()
    {
        await CreateCardAsync("Family Visa", Visa, CroatianIban);

        var response = await Client.PostAsJsonAsync("/api/data/export", new { password = Password }, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Matches(@"^monthwise-export-\d{4}-\d{2}-\d{2}-\d{4}\.monthwise$", response.Content.Headers.ContentDisposition?.FileNameStar);
        var file = await response.Content.ReadAsByteArrayAsync(Ct);
        Assert.DoesNotContain(Visa, System.Text.Encoding.Latin1.GetString(file), StringComparison.Ordinal);

        var data = Codec.Read(file, Password).Value;
        var row = Assert.Single(data.Tables["card"]!.AsArray())!.AsObject();
        Assert.Equal(Visa, (string?)row["card_number"]);
        Assert.Equal(CroatianIban, (string?)row["iban"]);
        Assert.False(row.ContainsKey("card_number_hash"));
        Assert.DoesNotContain(_factory!.LogMessages, m => m.Contains(Password, StringComparison.Ordinal));

        var shortPassword = await Client.PostAsJsonAsync("/api/data/export", new { password = "12345" }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, shortPassword.StatusCode);
    }

    [Fact]
    public async Task Wrong_password_foreign_newer_and_unconfirmed_files_change_nothing()
    {
        await CreateCardAsync("Family Visa", Visa, null);
        var file = await ExportAsync();

        const string WrongOrDamaged = "The password is wrong or the file is damaged.";
        Assert.Contains(WrongOrDamaged, await ErrorsAsync(await PostFormAsync("/api/data/import/check", file, "wrong-password")), StringComparison.Ordinal);
        Assert.Contains(
            "not a Monthwise export file",
            await ErrorsAsync(await PostFormAsync("/api/data/import/check", "not a file"u8.ToArray(), Password)),
            StringComparison.Ordinal);

        var damaged = file.ToArray();
        damaged[^20] ^= 0xFF;
        Assert.Contains(WrongOrDamaged, await ErrorsAsync(await PostFormAsync("/api/data/import/check", damaged, Password)), StringComparison.Ordinal);

        var newer = WithMigration(file, "29991231000000_FromTheFuture");
        Assert.Contains(
            "made by Monthwise 9.9.9, which is newer",
            await ErrorsAsync(await PostFormAsync("/api/data/import/check", newer, Password)),
            StringComparison.Ordinal);

        var unconfirmed = await PostFormAsync("/api/data/import", file, Password, "yes");
        Assert.Equal(HttpStatusCode.BadRequest, unconfirmed.StatusCode);

        Assert.Single((await GetJsonAsync("/api/cards")).EnumerateArray());
        Assert.Equal(JsonValueKind.Null, (await GetJsonAsync("/api/data")).GetProperty("lastImport").ValueKind);
    }

    [Fact]
    public async Task A_file_from_an_older_release_is_brought_up_to_date()
    {
        await CreateCardAsync("Family Visa", Visa, CroatianIban);
        var before = await SnapshotAsync();
        var older = WithMigration(await ExportAsync(), PreviousMigration);
        await ExecuteSqlAsync(CashFlowEndpointTests.DeleteAllSql);

        var import = await PostFormAsync("/api/data/import", older, Password, "REPLACE");

        Assert.Equal(HttpStatusCode.OK, import.StatusCode);
        Assert.Equal(before, await SnapshotAsync());
    }

    [Fact]
    public async Task A_failing_import_changes_nothing_is_recorded_and_the_safety_copy_undoes_an_import()
    {
        await CreateCardAsync("Family Visa", Visa, null);
        var original = await SnapshotAsync();
        var file = await ExportAsync();

        // A card without a name breaks a rule of the database while the data is copied.
        var data = Codec.Read(file, Password).Value;
        data.Tables["card"]![0]!["name"] = null;
        var broken = Codec.Write(data, Password);
        var failed = await PostFormAsync("/api/data/import", broken, Password, "REPLACE");
        Assert.Equal(HttpStatusCode.Conflict, failed.StatusCode);
        Assert.Equal(original, await SnapshotAsync());

        // A good import of other data, then the safety copy brings the original back.
        await ExecuteSqlAsync(CashFlowEndpointTests.DeleteAllSql);
        await CreateCardAsync("Other card", null, null);
        var otherFile = await ExportAsync();
        await ExecuteSqlAsync(CashFlowEndpointTests.DeleteAllSql);
        await CreateCardAsync("Family Visa", Visa, null);
        var current = await SnapshotAsync();
        Assert.Equal(HttpStatusCode.OK, (await PostFormAsync("/api/data/import", otherFile, Password, "REPLACE")).StatusCode);
        Assert.NotEqual(current, await SnapshotAsync());

        var restore = await Client.PostAsJsonAsync("/api/data/safety-copy/restore", new { confirmation = "REPLACE" }, Ct);
        Assert.Equal(HttpStatusCode.OK, restore.StatusCode);
        Assert.Equal(current, await SnapshotAsync());

        var history = (await GetJsonAsync("/api/data/history")).GetProperty("items").EnumerateArray()
            .Select(i => (i.GetProperty("kind").GetString(), i.GetProperty("succeeded").GetBoolean()))
            .Reverse()
            .ToList();
        Assert.Contains(("Import", false), history);
        Assert.Contains(("SafetyCopyRestore", true), history);
        Assert.Equal(("Export", true), history[0]);

        var overview = await GetJsonAsync("/api/data");
        Assert.Equal("SafetyCopyRestore", overview.GetProperty("lastImport").GetProperty("kind").GetString());
        Assert.Equal(JsonValueKind.String, overview.GetProperty("safetyCopyCreatedAt").ValueKind);
    }

    [Fact]
    public async Task While_an_import_runs_other_calls_are_refused_except_health()
    {
        var gate = _factory!.Services.GetRequiredService<ImportGate>();
        Assert.True(gate.TryEnter());
        try
        {
            var cards = await Client.GetAsync(new Uri("/api/cards", UriKind.Relative), Ct);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, cards.StatusCode);
            var health = await Client.GetAsync(new Uri("/api/health", UriKind.Relative), Ct);
            Assert.Equal(HttpStatusCode.OK, health.StatusCode);
            var overview = await Client.GetAsync(new Uri("/api/data", UriKind.Relative), Ct);
            Assert.Equal(HttpStatusCode.OK, overview.StatusCode);
        }
        finally
        {
            gate.Exit();
        }
    }

    [Fact]
    public async Task The_reminder_is_due_without_an_export_and_follows_its_setting()
    {
        var reminder = (await GetJsonAsync("/api/data")).GetProperty("reminder");
        Assert.True(reminder.GetProperty("enabled").GetBoolean());
        Assert.Equal(30, reminder.GetProperty("intervalDays").GetInt32());
        Assert.True(reminder.GetProperty("isDue").GetBoolean());

        await ExportAsync();
        Assert.False((await GetJsonAsync("/api/data")).GetProperty("reminder").GetProperty("isDue").GetBoolean());

        var invalid = await Client.PutAsJsonAsync("/api/data/reminder", new { enabled = true, intervalDays = 10 }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        var changed = await Client.PutAsJsonAsync("/api/data/reminder", new { enabled = false, intervalDays = 7 }, Ct);
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        reminder = (await GetJsonAsync("/api/data")).GetProperty("reminder");
        Assert.False(reminder.GetProperty("enabled").GetBoolean());
        Assert.Equal(7, reminder.GetProperty("intervalDays").GetInt32());
    }

    [Fact]
    public async Task Every_table_is_household_data_or_belongs_to_the_installation()
    {
        await using var scope = _factory!.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Infrastructure.Persistence.MonthwiseDbContext>();
        var modelTables = dbContext.Model.GetEntityTypes().Select(e => e.GetTableName()!).Distinct().ToList();

        var data = Codec.Read(await ExportAsync(), Password).Value;

        // NFR-7: a new table is exported unless it is listed as an installation table on purpose.
        Assert.All(modelTables, table => Assert.True(
            data.Tables.ContainsKey(table) || Infrastructure.DataTransfer.HouseholdDataStore.InstallationTables.Contains(table),
            $"Table '{table}' is neither exported nor an installation table."));
        Assert.All(Infrastructure.DataTransfer.HouseholdDataStore.InstallationTables, table => Assert.False(data.Tables.ContainsKey(table)));
    }

    private IExportFileCodec Codec => _factory!.Services.GetRequiredService<IExportFileCodec>();

    private byte[] WithMigration(byte[] file, string migration)
    {
        var data = Codec.Read(file, Password).Value;
        return Codec.Write(data with { Migration = migration, Release = "9.9.9" }, Password);
    }

    private async Task<byte[]> ExportAsync()
    {
        var response = await Client.PostAsJsonAsync("/api/data/export", new { password = Password }, Ct);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        return await response.Content.ReadAsByteArrayAsync(Ct);
    }

    private async Task<HttpResponseMessage> PostFormAsync(string url, byte[] file, string password, string? confirmation = null)
    {
        using var form = new MultipartFormDataContent();
        var content = new ByteArrayContent(file);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(content, "file", "monthwise-export.monthwise");
        form.Add(new StringContent(password), "password");
        if (confirmation is not null)
        {
            form.Add(new StringContent(confirmation), "confirmation");
        }
        return await Client.PostAsync(new Uri(url, UriKind.Relative), form, Ct);
    }

    /// <summary>The field error messages of a 400 response, all in one text.</summary>
    private static async Task<string> ErrorsAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync(Ct);
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, body);
        return string.Join(" | ", JsonNode.Parse(body)!["errors"]!.AsObject().SelectMany(e => e.Value!.AsArray()).Select(m => (string?)m));
    }

    private async Task<Guid> CreateCardAsync(string name, string? cardNumber, string? iban)
    {
        var response = await Client.PostAsJsonAsync("/api/cards", new
        {
            name,
            issuerId = VisaIssuer,
            bankId = ZaBa,
            cardNumber,
            iban,
            validFrom = "2025-01",
            validThrough = "2029-12",
            type = "Debit",
            isPrimary = false,
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

    private async Task CreateParticipantAsync(string name)
    {
        var response = await Client.PostAsJsonAsync("/api/participants", new { name, description = (string?)null }, Ct);
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
    }

    private async Task<JsonElement> GetJsonAsync(string url)
    {
        var response = await Client.GetAsync(new Uri(url, UriKind.Relative), Ct);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync(Ct));
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    /// <summary>Every household table as sorted JSON rows, without the encrypted values (they differ by nonce).</summary>
    private async Task<string> SnapshotAsync()
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(Ct);
        var tables = new List<string>();
        await using (var command = new NpgsqlCommand(
            "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' AND table_type = 'BASE TABLE' ORDER BY table_name",
            connection))
        await using (var reader = await command.ExecuteReaderAsync(Ct))
        {
            while (await reader.ReadAsync(Ct))
            {
                tables.Add(reader.GetString(0));
            }
        }

        var snapshot = new System.Text.StringBuilder();
        foreach (var table in tables.Where(t => !Infrastructure.DataTransfer.HouseholdDataStore.InstallationTables.Contains(t)))
        {
            await using var command = new NpgsqlCommand(
                $"SELECT coalesce(jsonb_agg(to_jsonb(t) - 'card_number' - 'iban' ORDER BY to_jsonb(t)::text), '[]')::text FROM \"{table}\" t",
                connection);
            snapshot.Append(table).Append(": ").AppendLine((string?)await command.ExecuteScalarAsync(Ct));
        }
        return snapshot.ToString();
    }

    private async Task<(string Number, string NumberHash)> StoredNumbersAsync(Guid card)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand("SELECT card_number, card_number_hash FROM card WHERE id = @id", connection);
        command.Parameters.AddWithValue("id", card);
        await using var reader = await command.ExecuteReaderAsync(Ct);
        Assert.True(await reader.ReadAsync(Ct));
        return (reader.GetString(0), reader.GetString(1));
    }

    private async Task ExecuteSqlAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(Ct);
    }
}
