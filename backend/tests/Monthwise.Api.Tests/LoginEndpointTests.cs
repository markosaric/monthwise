using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Monthwise.Domain.Users;
using Npgsql;

namespace Monthwise.Api.Tests;

/// <summary>
/// Setup, sign-in, sessions, users and the household admin rules against a real PostgreSQL
/// (docs/feature-requirements/login.md). Each test starts without users. All names and passwords are made up.
/// </summary>
[Collection(SharedDatabase.Name)]
public sealed partial class LoginEndpointTests : IAsyncLifetime
{
    private const string SetupCode = "TEST-CODE-ABCD-EFGH";
    private const string CleanUpSql = "DELETE FROM app_user; DELETE FROM setup_code_use; DELETE FROM data_transfer_log;";

    /// <summary>The endpoints that answer without a session, on purpose (FR-2).</summary>
    private static readonly string[] AnonymousEndpoints =
    [
        "GET /api/health",
        "GET /api/auth/state",
        "POST /api/auth/setup",
        "POST /api/auth/sign-in",
    ];

    private readonly List<IDisposable> _disposables = [];
    private MonthwiseApiFactory? _factory;
    private string _connectionString = string.Empty;

    private MonthwiseApiFactory Factory => _factory!;
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _connectionString = TestDatabase.ConnectionString;
        _factory = NewFactory();
        using var client = Factory.CreateClient();
        await client.GetAsync(new Uri("/api/health", UriKind.Relative), Ct);
        await ExecuteSqlAsync(CleanUpSql);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var disposable in _disposables)
        {
            disposable.Dispose();
        }
        if (_factory is not null)
        {
            await ExecuteSqlAsync(CleanUpSql);
            await _factory.DisposeAsync();
        }
    }

    // ---- FR-2: every endpoint needs a signed-in user ----

    [Fact]
    public async Task Every_endpoint_except_the_anonymous_ones_answers_401_without_a_session()
    {
        var endpoints = ApiEndpoints().ToList();
        Assert.True(endpoints.Count > 50, $"Only {endpoints.Count} endpoints found; the search is broken.");

        var anonymous = endpoints.Where(e => e.Endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null).Select(e => e.Name).Order();
        Assert.Equal(AnonymousEndpoints.Order(), anonymous);

        var client = Track(Factory.CreateBrowserClient());
        var answers = new List<string>();
        foreach (var (name, endpoint) in endpoints.Where(e => !AnonymousEndpoints.Contains(e.Name)))
        {
            var method = name.Split(' ')[0];
            using var request = new HttpRequestMessage(new HttpMethod(method), new Uri(SamplePath(endpoint.RoutePattern.RawText!), UriKind.Relative));
            if (endpoint.Metadata.GetMetadata<IAcceptsMetadata>()?.ContentTypes.Contains("multipart/form-data") == true)
            {
                request.Content = new MultipartFormDataContent { { new StringContent("x"), "password" } };
            }
            else if (method is "POST" or "PUT")
            {
                request.Content = JsonContent.Create(new { });
            }
            using var response = await client.SendAsync(request, Ct);
            if (response.StatusCode != HttpStatusCode.Unauthorized)
            {
                answers.Add($"{name}: {(int)response.StatusCode}");
            }
            else
            {
                var body = await response.Content.ReadAsStringAsync(Ct);
                Assert.Contains("Sign in to continue.", body, StringComparison.Ordinal);
            }
        }

        Assert.Empty(answers);
    }

    [Fact]
    public async Task A_changing_call_without_the_request_header_is_refused()
    {
        var client = Track(await Factory.CreateSignedInClientAsync());
        client.DefaultRequestHeaders.Remove(MonthwiseApiFactory.RequestHeader);

        using var response = await client.PostAsJsonAsync(new Uri("/api/payers", UriKind.Relative), new { name = "Ana" }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("request.header_missing", await response.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);

        using var signIn = await client.PostAsJsonAsync(new Uri("/api/auth/sign-in", UriKind.Relative), new { username = "x", password = "y" }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, signIn.StatusCode);
        Assert.Contains("request.header_missing", await signIn.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
    }

    // ---- FR-1, FR-9: setup ----

    [Fact]
    public async Task Without_users_the_state_asks_for_setup_and_without_a_configured_code_setup_is_refused()
    {
        await using var factory = NewFactory(setupCode: null);
        var client = Track(factory.CreateBrowserClient());

        var state = await GetJsonAsync(client, "/api/auth/state");
        Assert.True(state.GetProperty("setupNeeded").GetBoolean());
        Assert.Equal(JsonValueKind.Null, state.GetProperty("user").ValueKind);

        var error = await PostFailureAsync(client, "/api/auth/setup", Setup("ANY-CODE", "ana"));
        Assert.Contains("This installation has no setup code.", error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_right_setup_code_creates_the_first_household_admin_once()
    {
        var client = Track(Factory.CreateBrowserClient());

        var wrong = await PostFailureAsync(client, "/api/auth/setup", Setup("WRONG-CODE", "ana"));
        Assert.Contains("The setup code is not right or was already used.", wrong, StringComparison.Ordinal);

        // Case, spaces and dashes do not matter.
        await PostOkAsync(client, "/api/auth/setup", Setup(" test code abcd efgh ", "ana"));
        var state = await GetJsonAsync(client, "/api/auth/state");
        Assert.False(state.GetProperty("setupNeeded").GetBoolean());
        Assert.Equal("ana", state.GetProperty("user").GetProperty("username").GetString());
        Assert.Equal("HouseholdAdmin", state.GetProperty("user").GetProperty("role").GetString());
        await GetJsonAsync(client, "/api/cards");

        var again = Track(Factory.CreateBrowserClient());
        var used = await PostFailureAsync(again, "/api/auth/setup", Setup(SetupCode, "ivo"));
        Assert.Contains("The setup code is not right or was already used.", used, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_new_setup_code_resets_an_admin_password_or_adds_an_admin_but_never_a_member()
    {
        await using (var first = NewFactory(setupCode: "FIRST-CODE"))
        {
            var setupClient = Track(first.CreateBrowserClient());
            await PostOkAsync(setupClient, "/api/auth/setup", Setup("FIRST-CODE", "ana"));
            await PostOkAsync(setupClient, "/api/users", NewUser("Ivo", "ivo", "Member"));
        }

        await using var factory = NewFactory(setupCode: "SECOND-CODE");
        var client = Track(factory.CreateBrowserClient());
        var member = await PostFailureAsync(client, "/api/auth/setup", Setup("SECOND-CODE", "IVO"));
        Assert.Contains("This username belongs to a member.", member, StringComparison.Ordinal);

        await PostOkAsync(client, "/api/auth/setup", Setup("SECOND-CODE", "ANA", password: "a new admin password"));
        Track(await factory.SignInAsync("ana", "a new admin password"));

        await using var third = NewFactory(setupCode: "THIRD-CODE");
        await PostOkAsync(Track(third.CreateBrowserClient()), "/api/auth/setup", Setup("THIRD-CODE", "marko"));
        Assert.Equal(2, await CountAsync("SELECT count(*) FROM app_user WHERE role = 'HouseholdAdmin'"));
    }

    // ---- FR-2 to FR-5: signing in and out ----

    [Fact]
    public async Task Unknown_user_wrong_password_and_inactive_user_get_the_same_message_and_five_wrong_passwords_lock()
    {
        var admin = await Factory.CreateUserAsync();
        var inactive = await Factory.CreateUserAsync(UserRole.Member);
        await ExecuteSqlAsync($"UPDATE app_user SET is_active = false WHERE id = '{inactive.Id}'");
        var client = Track(Factory.CreateBrowserClient());

        var unknown = Errors(await PostFailureAsync(client, "/api/auth/sign-in", SignIn("nobody-here", MonthwiseApiFactory.TestPassword)));
        var wrong = Errors(await PostFailureAsync(client, "/api/auth/sign-in", SignIn(admin.Username, "not the password")));
        var notActive = Errors(await PostFailureAsync(client, "/api/auth/sign-in", SignIn(inactive.Username, MonthwiseApiFactory.TestPassword)));
        Assert.Equal(unknown, wrong);
        Assert.Equal(unknown, notActive);
        Assert.Contains("The username or password is not right.", unknown, StringComparison.Ordinal);

        for (var i = 0; i < 4; i++)
        {
            await PostFailureAsync(client, "/api/auth/sign-in", SignIn(admin.Username, "not the password"));
        }
        var locked = await PostFailureAsync(client, "/api/auth/sign-in", SignIn(admin.Username, MonthwiseApiFactory.TestPassword));
        Assert.Contains("locked for a while", locked, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Many_sign_in_attempts_from_one_place_are_refused_for_a_minute()
    {
        await using var factory = NewFactory(attemptsPerMinute: 3);
        var client = Track(factory.CreateBrowserClient());
        for (var i = 0; i < 3; i++)
        {
            await PostFailureAsync(client, "/api/auth/sign-in", SignIn("nobody-here", "whatever it is"));
        }

        using var response = await client.PostAsJsonAsync(new Uri("/api/auth/sign-in", UriKind.Relative), SignIn("nobody-here", "x"), Ct);
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Contains("Too many sign-in attempts.", await response.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_session_cookie_is_http_only_strict_and_lasts_30_days_only_with_stay_signed_in()
    {
        var user = await Factory.CreateUserAsync();
        var client = Track(Factory.CreateBrowserClient());

        var shortCookie = await SignInCookieAsync(client, user.Username, staySignedIn: false);
        Assert.Contains("httponly", shortCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", shortCookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("expires=", shortCookie, StringComparison.OrdinalIgnoreCase);

        var longCookie = await SignInCookieAsync(client, user.Username, staySignedIn: true);
        var expires = DateTimeOffset.Parse(ExpiresPattern().Match(longCookie).Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        Assert.InRange(expires - DateTimeOffset.UtcNow, TimeSpan.FromDays(29), TimeSpan.FromDays(31));
    }

    [Fact]
    public async Task A_session_survives_a_restart_and_sign_out_ends_it()
    {
        var user = await Factory.CreateUserAsync();
        var client = Track(Factory.CreateBrowserClient());
        var cookie = (await SignInCookieAsync(client, user.Username, staySignedIn: true)).Split(';')[0];

        // A new instance of Monthwise on the same database (a restart, an update, an Azure cold start).
        await using var restarted = NewFactory();
        var after = Track(restarted.CreateBrowserClient());
        after.DefaultRequestHeaders.Add("Cookie", cookie);
        var state = await GetJsonAsync(after, "/api/auth/state");
        Assert.Equal(user.Username, state.GetProperty("user").GetProperty("username").GetString());

        await PostOkAsync(client, "/api/auth/sign-out", new { });
        using var signedOut = await client.GetAsync(new Uri("/api/cards", UriKind.Relative), Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, signedOut.StatusCode);
    }

    // ---- FR-6 to FR-8: users and passwords ----

    [Fact]
    public async Task A_household_admin_adds_a_member_who_must_change_the_temporary_password_first()
    {
        var admin = Track(await Factory.CreateSignedInClientAsync(displayName: "Ana"));
        var created = await PostJsonAsync(admin, "/api/users", NewUser("Ivo", "Ivo.Horvat", "Member"), HttpStatusCode.Created);
        Assert.True(created.GetProperty("mustChangePassword").GetBoolean());

        var ivo = Track(await Factory.SignInAsync("ivo.horvat", "temporary password 1"));
        using (var refused = await ivo.GetAsync(new Uri("/api/cards", UriKind.Relative), Ct))
        {
            Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
            Assert.Contains("Choose a new password first.", await refused.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
        }

        var wrongCurrent = await PostFailureAsync(ivo, "/api/auth/password", new { currentPassword = "nope", newPassword = "my own password 1" });
        Assert.Contains("The current password is not right.", wrongCurrent, StringComparison.Ordinal);
        var tooShort = await PostFailureAsync(ivo, "/api/auth/password", new { currentPassword = "temporary password 1", newPassword = "short" });
        Assert.Contains("at least 12 characters", tooShort, StringComparison.Ordinal);

        await PostOkAsync(ivo, "/api/auth/password", new { currentPassword = "temporary password 1", newPassword = "my own password 1" });
        await GetJsonAsync(ivo, "/api/cards");
        var state = await GetJsonAsync(ivo, "/api/auth/state");
        Assert.False(state.GetProperty("user").GetProperty("mustChangePassword").GetBoolean());
    }

    [Fact]
    public async Task A_member_sees_only_themself_and_cannot_manage_users_export_or_import()
    {
        var admin = await Factory.CreateUserAsync(displayName: "Ana");
        var member = Track(await Factory.CreateSignedInClientAsync(UserRole.Member, "Ivo"));

        var list = await GetJsonAsync(member, "/api/users");
        Assert.False(list.GetProperty("canManage").GetBoolean());
        Assert.Equal(["Ivo"], list.GetProperty("users").EnumerateArray().Select(u => u.GetProperty("displayName").GetString()));
        using (var other = await member.GetAsync(new Uri($"/api/users/{admin.Id}", UriKind.Relative), Ct))
        {
            Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);
        }

        await AssertForbiddenAsync(member, HttpMethod.Post, "/api/users", NewUser("Eva", "eva", "Member"));
        await AssertForbiddenAsync(member, HttpMethod.Put, $"/api/users/{admin.Id}", new { displayName = "X", username = "x-x", role = "Member", isActive = true });
        await AssertForbiddenAsync(member, HttpMethod.Delete, $"/api/users/{admin.Id}", null);
        await AssertForbiddenAsync(member, HttpMethod.Post, $"/api/users/{admin.Id}/password", new { temporaryPassword = "temporary password 1" });
        await AssertForbiddenAsync(member, HttpMethod.Post, "/api/data/export", new { password = "export password 1" });
        Assert.False((await GetJsonAsync(member, "/api/data")).GetProperty("canManage").GetBoolean());
    }

    [Fact]
    public async Task The_last_active_household_admin_cannot_be_made_a_member_inactive_or_deleted()
    {
        var client = Track(await Factory.CreateSignedInClientAsync(displayName: "Ana"));
        var me = (await GetJsonAsync(client, "/api/auth/state")).GetProperty("user");
        var id = me.GetProperty("id").GetString();
        var username = me.GetProperty("username").GetString();

        foreach (var change in new[] { new { role = "Member", isActive = true }, new { role = "HouseholdAdmin", isActive = false } })
        {
            using var response = await client.PutAsJsonAsync(
                new Uri($"/api/users/{id}", UriKind.Relative), new { displayName = "Ana", username, change.role, change.isActive }, Ct);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Contains("at least one active household admin", await response.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
        }
        using (var delete = await client.DeleteAsync(new Uri($"/api/users/{id}", UriKind.Relative), Ct))
        {
            Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);
        }

        // With a second admin the first one may become a member.
        await PostJsonAsync(client, "/api/users", NewUser("Ivo", "ivo", "HouseholdAdmin"), HttpStatusCode.Created);
        using var allowed = await client.PutAsJsonAsync(
            new Uri($"/api/users/{id}", UriKind.Relative), new { displayName = "Ana", username, role = "Member", isActive = true }, Ct);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }

    [Fact]
    public async Task Deactivating_deleting_a_role_change_and_a_password_reset_reach_open_sessions_at_once()
    {
        var admin = Track(await Factory.CreateSignedInClientAsync(displayName: "Ana"));
        var users = new Dictionary<string, (string Id, HttpClient Client)>();
        foreach (var name in new[] { "deactivated", "deleted", "promoted", "reset" })
        {
            var user = await PostJsonAsync(admin, "/api/users", NewUser(name, name, "Member"), HttpStatusCode.Created);
            var id = user.GetProperty("id").GetString()!;
            await ExecuteSqlAsync($"UPDATE app_user SET must_change_password = false WHERE id = '{id}'");
            users[name] = (id, Track(await Factory.SignInAsync(name, "temporary password 1")));
            await GetJsonAsync(users[name].Client, "/api/cards");
        }

        await PutAsync(admin, $"/api/users/{users["deactivated"].Id}", new { displayName = "deactivated", username = "deactivated", role = "Member", isActive = false });
        using (var deleted = await admin.DeleteAsync(new Uri($"/api/users/{users["deleted"].Id}", UriKind.Relative), Ct))
        {
            Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        }
        await PutAsync(admin, $"/api/users/{users["promoted"].Id}", new { displayName = "promoted", username = "promoted", role = "HouseholdAdmin", isActive = true });
        await PostJsonAsync(admin, $"/api/users/{users["reset"].Id}/password", new { temporaryPassword = "another temporary 1" }, HttpStatusCode.OK);

        foreach (var name in new[] { "deactivated", "deleted", "reset" })
        {
            using var response = await users[name].Client.GetAsync(new Uri("/api/cards", UriKind.Relative), Ct);
            Assert.True(response.StatusCode == HttpStatusCode.Unauthorized, $"{name}: {response.StatusCode}");
        }
        Assert.True((await GetJsonAsync(users["promoted"].Client, "/api/data")).GetProperty("canManage").GetBoolean());
        await GetJsonAsync(admin, "/api/cards");
    }

    [Fact]
    public async Task Changing_ones_own_password_ends_the_other_sessions_only()
    {
        var user = await Factory.CreateUserAsync();
        var here = Track(await Factory.SignInAsync(user.Username, MonthwiseApiFactory.TestPassword));
        var phone = Track(await Factory.SignInAsync(user.Username, MonthwiseApiFactory.TestPassword));

        await PostOkAsync(here, "/api/auth/password", new { currentPassword = MonthwiseApiFactory.TestPassword, newPassword = "a brand new password" });

        await GetJsonAsync(here, "/api/cards");
        using var other = await phone.GetAsync(new Uri("/api/cards", UriKind.Relative), Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, other.StatusCode);
    }

    [Fact]
    public async Task A_username_is_unique_without_regard_to_case_and_follows_the_rules()
    {
        var admin = Track(await Factory.CreateSignedInClientAsync());
        await PostJsonAsync(admin, "/api/users", NewUser("Ivo", "Ivo", "Member"), HttpStatusCode.Created);

        var taken = await PostFailureAsync(admin, "/api/users", NewUser("Ivo again", "IVO", "Member"));
        Assert.Contains("This username is already used.", taken, StringComparison.Ordinal);
        var invalid = await PostFailureAsync(admin, "/api/users", NewUser("Bad", "a b", "Member"));
        Assert.Contains("Username must be 3 to 50 characters", invalid, StringComparison.Ordinal);
        var croatian = await PostJsonAsync(admin, "/api/users", NewUser("Đuro", "đuro.žic", "Member"), HttpStatusCode.Created);
        Assert.Equal("đuro.žic", croatian.GetProperty("username").GetString());
    }

    // ---- FR-10, NFR-3 ----

    [Fact]
    public async Task The_history_names_who_exported_and_no_secret_is_logged()
    {
        var setup = Track(Factory.CreateBrowserClient());
        await PostOkAsync(setup, "/api/auth/setup", Setup(SetupCode, "ana", displayName: "Ana Anić"));
        await PostFailureAsync(Track(Factory.CreateBrowserClient()), "/api/auth/sign-in", SignIn("ana", "a wrong password 9"));

        using (var export = await setup.PostAsJsonAsync(new Uri("/api/data/export", UriKind.Relative), new { password = "export password 1" }, Ct))
        {
            Assert.Equal(HttpStatusCode.OK, export.StatusCode);
        }

        var history = await GetJsonAsync(setup, "/api/data/history");
        Assert.Equal("Ana Anić", history.GetProperty("items")[0].GetProperty("userName").GetString());

        var log = string.Join('\n', Factory.LogMessages);
        Assert.Contains("Sign-in failed: ana", log, StringComparison.Ordinal);
        foreach (var secret in new[] { "an admin password", "a wrong password 9", "TEST-CODE", "TESTCODE", "export password 1", "monthwise.session=" })
        {
            Assert.DoesNotContain(secret, log, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Sign_in_messages_are_in_croatian_when_the_app_is()
    {
        var client = Track(Factory.CreateBrowserClient());
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("hr");

        var error = await PostFailureAsync(client, "/api/auth/sign-in", SignIn("nobody-here", "whatever it is"));
        Assert.Contains("Korisničko ime ili lozinka nisu ispravni.", error, StringComparison.Ordinal);
        using var signedOut = await client.GetAsync(new Uri("/api/cards", UriKind.Relative), Ct);
        Assert.Contains("Prijavite se za nastavak.", await signedOut.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
    }

    // ---- helpers ----

    private MonthwiseApiFactory NewFactory(string? setupCode = SetupCode, int? attemptsPerMinute = null)
    {
        var settings = new Dictionary<string, string?>();
        if (setupCode is not null)
        {
            settings["Setup:Code"] = setupCode;
        }
        if (attemptsPerMinute is { } attempts)
        {
            settings["Login:SignInAttemptsPerMinute"] = attempts.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        return new MonthwiseApiFactory(_connectionString, migrateOnStartup: true, settings: settings);
    }

    private HttpClient Track(HttpClient client)
    {
        _disposables.Add(client);
        return client;
    }

    private IEnumerable<(string Name, RouteEndpoint Endpoint)> ApiEndpoints() =>
        Factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText?.StartsWith("/api", StringComparison.Ordinal) == true)
            .SelectMany(e => (e.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? []).Select(m => ($"{m} {e.RoutePattern.RawText}", e)))
            .Where(e => !e.Item1.StartsWith("OPTIONS", StringComparison.Ordinal));

    /// <summary>A path for a route pattern: ids become a new id, months and dates a valid value.</summary>
    private static string SamplePath(string pattern) => RouteParameter().Replace(pattern, m => m.Groups["constraint"].Value switch
    {
        "guid" => Guid.NewGuid().ToString(),
        "int" => "1",
        _ when m.Groups["name"].Value.Contains("month", StringComparison.OrdinalIgnoreCase) => "2026-01",
        _ => "x",
    });

    /// <summary>The validation errors of a problem, without the parts that differ per request.</summary>
    private static string Errors(string problem) => JsonDocument.Parse(problem).RootElement.GetProperty("errors").GetRawText();

    private static object Setup(string code, string username, string password = "an admin password", string displayName = "Ana") =>
        new { setupCode = code, displayName, username, password };

    private static object SignIn(string username, string password, bool staySignedIn = false) => new { username, password, staySignedIn };

    private static object NewUser(string displayName, string username, string role) =>
        new { displayName, username, role, temporaryPassword = "temporary password 1" };

    private static async Task<string> SignInCookieAsync(HttpClient client, string username, bool staySignedIn)
    {
        using var response = await client.PostAsJsonAsync(
            new Uri("/api/auth/sign-in", UriKind.Relative), SignIn(username, MonthwiseApiFactory.TestPassword, staySignedIn), Ct);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        return response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("monthwise.session=", StringComparison.Ordinal));
    }

    private static async Task<JsonElement> GetJsonAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(new Uri(path, UriKind.Relative), Ct);
        var body = await response.Content.ReadAsStringAsync(Ct);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"GET {path}: {(int)response.StatusCode} {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static async Task<JsonElement> PostJsonAsync(HttpClient client, string path, object body, HttpStatusCode expected)
    {
        using var response = await client.PostAsJsonAsync(new Uri(path, UriKind.Relative), body, Ct);
        var text = await response.Content.ReadAsStringAsync(Ct);
        Assert.True(response.StatusCode == expected, $"POST {path}: {(int)response.StatusCode} {text}");
        return JsonDocument.Parse(text).RootElement.Clone();
    }

    private static async Task PostOkAsync(HttpClient client, string path, object body)
    {
        using var response = await client.PostAsJsonAsync(new Uri(path, UriKind.Relative), body, Ct);
        Assert.True(response.IsSuccessStatusCode, $"POST {path}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync(Ct)}");
    }

    private static async Task PutAsync(HttpClient client, string path, object body)
    {
        using var response = await client.PutAsJsonAsync(new Uri(path, UriKind.Relative), body, Ct);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"PUT {path}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync(Ct)}");
    }

    private static async Task<string> PostFailureAsync(HttpClient client, string path, object body)
    {
        using var response = await client.PostAsJsonAsync(new Uri(path, UriKind.Relative), body, Ct);
        var text = await response.Content.ReadAsStringAsync(Ct);
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, $"POST {path}: {(int)response.StatusCode} {text}");
        return text;
    }

    private static async Task AssertForbiddenAsync(HttpClient client, HttpMethod method, string path, object? body)
    {
        using var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative));
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }
        using var response = await client.SendAsync(request, Ct);
        Assert.True(response.StatusCode == HttpStatusCode.Forbidden, $"{method} {path}: {(int)response.StatusCode}");
        Assert.Contains("Only a household admin can do this.", await response.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
    }

    private async Task ExecuteSqlAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(Ct);
    }

    private async Task<long> CountAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(sql, connection);
        return (long)(await command.ExecuteScalarAsync(Ct))!;
    }

    [GeneratedRegex(@"\{(?<name>\w+)(?::(?<constraint>\w+))?\??\}")]
    private static partial Regex RouteParameter();

    [GeneratedRegex("expires=([^;]+)", RegexOptions.IgnoreCase)]
    private static partial Regex ExpiresPattern();
}
