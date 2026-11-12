using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Monthwise.Api.Tests;

/// <summary>
/// The answer an API call gives on success: 201 Created with the address of the new item for the
/// endpoints that add one (api-create-responses FR-1, FR-3), 200 OK for everything else.
/// </summary>
public static partial class ExpectedStatus
{
    public static HttpStatusCode For(HttpMethod method, string path) =>
        IsCreate(method, path) ? HttpStatusCode.Created : HttpStatusCode.OK;

    /// <summary>Checks the status and, for a create endpoint, the Location header against the body.</summary>
    public static async Task AssertAsync(HttpResponseMessage response, HttpMethod method, string path, CancellationToken ct)
    {
        var text = await response.Content.ReadAsStringAsync(ct);
        Assert.True(response.StatusCode == For(method, path), $"{method} {path}: {(int)response.StatusCode} {text}");
        if (!IsCreate(method, path))
        {
            return;
        }
        var body = JsonDocument.Parse(text).RootElement;
        var location = response.Headers.Location?.OriginalString;
        var route = Route(path);
        var match = CreatePath().Match(route);
        var collection = match.Groups["collection"].Value;
        var nested = match.Groups["nested"].Value;
        switch (nested.Length > 0 ? nested : collection)
        {
            case "card-issuers" or "banks":
                Assert.Null(location);
                break;
            case "loans":
                Assert.Equal($"/api/loans/{Id(body.GetProperty("loan"))}", location);
                break;
            case "inputs":
                // A recurring input is added as a whole; its address is the recurring input's.
                Assert.Equal(
                    body.GetProperty("id").ValueKind == JsonValueKind.Null
                        ? $"/api/recurring-inputs/{body.GetProperty("recurringInputId").GetGuid()}"
                        : $"/api/inputs/{Id(body)}",
                    location);
                break;
            case "card-purchases":
                Assert.Equal($"/api/card-purchases/{Id(body.GetProperty("purchase"))}", location);
                break;
            case "balance-entries":
                Assert.Equal($"/api/balance-entries/{Id(body)}", location);
                break;
            case "subcategories":
                Assert.Equal($"/api/loan-subcategories/{Id(body)}", location);
                break;
            case "extra-payments" or "debt-entries":
                Assert.Equal($"/api/{nested}/{Id(body)}", location);
                break;
            case "payment-changes":
                var prefix = $"{route.TrimEnd('/')}/";
                Assert.NotNull(location);
                Assert.StartsWith(prefix, location, StringComparison.Ordinal);
                var changeId = Guid.Parse(location[prefix.Length..]);
                Assert.Contains(body.GetProperty("changes").EnumerateArray(), c => c.GetProperty("id").GetGuid() == changeId);
                break;
            default:
                Assert.Equal($"/api/{collection}/{Id(body)}", location);
                break;
        }
    }

    private static bool IsCreate(HttpMethod method, string path) =>
        method == HttpMethod.Post && CreatePath().IsMatch(Route(path));

    private static string Route(string path) => path.Split('?')[0];

    private static Guid Id(JsonElement item) => item.GetProperty("id").GetGuid();

    [GeneratedRegex(
        @"^/api/(?<collection>cards|participants|inputs|input-categories|card-issuers|banks|loans|loan-categories"
        + @"|card-purchases|expense-categories|recurring-bills|bills|expenses|expenses/cash-withdrawals|revolving-cards)"
        + @"(/[0-9a-f-]{36}/(?<nested>balance-entries|payment-changes|subcategories|extra-payments|debt-entries))?/?$")]
    private static partial Regex CreatePath();
}
