using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Monthwise.Application.Localization;
using Npgsql;

namespace Monthwise.Api.Tests;

/// <summary>
/// The API answers in the language the app names in Accept-Language (croatian-language.md FR-3,
/// FR-6, FR-7, acceptance criteria 4 to 6), against a real PostgreSQL (see TestDatabase).
/// </summary>
[Collection(SharedDatabase.Name)]
public sealed class CroatianEndpointTests : IAsyncLifetime
{
    private const string RestoreSql =
        CashFlowEndpointTests.DeleteAllSql
        + "UPDATE input_category SET name = 'Salary' WHERE id = '0199b0c0-0000-7000-8000-000000000201';";

    private const string BankLoan = "0199c2a0-0000-7000-8000-000000000301";

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
        await ExecuteSqlAsync(RestoreSql);
    }

    public async ValueTask DisposeAsync()
    {
        if (_factory is not null)
        {
            await ExecuteSqlAsync(RestoreSql);
        }
        _client?.Dispose();
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
    }

    [Fact]
    public async Task Errors_are_in_the_language_of_the_request()
    {
        var english = await NameErrorAsync("/api/input-categories", new { name = "" }, "en");
        var croatian = await NameErrorAsync("/api/input-categories", new { name = "" }, "hr");

        Assert.Equal("Name is required.", english);
        Assert.Equal(ServerTexts.ToCroatian("Name is required."), croatian);
        Assert.NotEqual(english, croatian);
        Assert.Equal(english, await NameErrorAsync("/api/input-categories", new { name = "" }, null));
    }

    [Fact]
    public async Task A_month_refused_in_croatian_is_explained_in_croatian()
    {
        using var request = Request(HttpMethod.Get, $"/api/cards/{Guid.NewGuid()}/card-payments/2026-13", "hr");
        using var response = await Client.SendAsync(request, Ct);
        var text = await response.Content.ReadAsStringAsync(Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(ServerTexts.ToCroatian("Enter the month as yyyy-MM.")!, text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Predefined_categories_are_shown_in_croatian_and_banks_are_not_translated()
    {
        Assert.Contains("Plaća", await NamesAsync("/api/input-categories", "hr"));
        Assert.Contains("Salary", await NamesAsync("/api/input-categories", "en"));
        Assert.Contains("Podizanje gotovine", await NamesAsync("/api/expense-categories", "hr"));
        Assert.Contains("Bankovni kredit", await NamesAsync("/api/loan-categories", "hr"));
        Assert.Equal(await NamesAsync("/api/banks", "en"), await NamesAsync("/api/banks", "hr"));
    }

    [Fact]
    public async Task A_renamed_predefined_category_keeps_the_users_name_in_both_languages()
    {
        var salary = (await ListAsync("/api/input-categories", "en")).Single(c => c.GetProperty("name").GetString() == "Salary");
        using var rename = Request(HttpMethod.Put, $"/api/input-categories/{salary.GetProperty("id").GetGuid()}", "hr");
        rename.Content = JsonContent.Create(new { name = "Primanja" });
        using var renamed = await Client.SendAsync(rename, Ct);
        Assert.True(renamed.IsSuccessStatusCode, await renamed.Content.ReadAsStringAsync(Ct));

        Assert.Contains("Primanja", await NamesAsync("/api/input-categories", "hr"));
        Assert.Contains("Primanja", await NamesAsync("/api/input-categories", "en"));
        Assert.DoesNotContain("Plaća", await NamesAsync("/api/input-categories", "hr"));
    }

    [Fact]
    public async Task A_name_like_a_predefined_name_in_either_language_is_refused()
    {
        Assert.NotNull(await NameErrorAsync("/api/input-categories", new { name = "Salary" }, "hr"));
        Assert.NotNull(await NameErrorAsync("/api/input-categories", new { name = "plaća" }, "en"));
        Assert.NotNull(await NameErrorAsync("/api/expense-categories", new { name = "Namirnice" }, "hr"));
        Assert.NotNull(await NameErrorAsync("/api/loan-categories", new { name = "Leasing", allowsResidualValue = true }, "hr"));
        Assert.NotNull(await NameErrorAsync($"/api/loan-categories/{BankLoan}/subcategories", new { name = "Stambeni kredit" }, "hr"));

        // Accents still count, as for every name.
        Assert.Null(await NameErrorAsync("/api/input-categories", new { name = "Placa" }, "hr"));
    }

    /// <summary>The name field's error, or null when the name was accepted.</summary>
    private async Task<string?> NameErrorAsync(string path, object body, string? language)
    {
        using var request = Request(HttpMethod.Post, path, language);
        request.Content = JsonContent.Create(body);
        using var response = await Client.SendAsync(request, Ct);
        var text = await response.Content.ReadAsStringAsync(Ct);
        if (response.IsSuccessStatusCode)
        {
            return null;
        }
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, $"{path}: {(int)response.StatusCode} {text}");
        return JsonDocument.Parse(text).RootElement.GetProperty("errors").GetProperty("name")[0].GetString();
    }

    private async Task<List<string?>> NamesAsync(string path, string language) =>
        (await ListAsync(path, language)).Select(e => e.GetProperty("name").GetString()).ToList();

    private async Task<List<JsonElement>> ListAsync(string path, string language)
    {
        using var request = Request(HttpMethod.Get, path, language);
        using var response = await Client.SendAsync(request, Ct);
        var text = await response.Content.ReadAsStringAsync(Ct);
        Assert.True(response.StatusCode == HttpStatusCode.OK, text);
        return JsonDocument.Parse(text).RootElement.EnumerateArray().Select(e => e.Clone()).ToList();
    }

    private static HttpRequestMessage Request(HttpMethod method, string path, string? language)
    {
        var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative));
        if (language is not null)
        {
            request.Headers.AcceptLanguage.ParseAdd(language);
        }
        return request;
    }

    private async Task ExecuteSqlAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(Ct);
    }
}
