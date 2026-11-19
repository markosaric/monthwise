using System.Globalization;
using Microsoft.Net.Http.Headers;
using Monthwise.Application.Localization;

namespace Monthwise.Api.Hosting;

/// <summary>
/// Answers each request in the language the app asks for (croatian-language.md FR-3): the
/// <c>Accept-Language</c> header picks Croatian or English messages. Only the UI culture is set;
/// the culture that reads and writes amounts and dates stays the same in both languages (FR-5).
/// </summary>
internal static class RequestLanguage
{
    public static WebApplication UseRequestLanguage(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            var previous = CultureInfo.CurrentUICulture;
            CultureInfo.CurrentUICulture = Of(context.Request.Headers[HeaderNames.AcceptLanguage]);
            try
            {
                await next(context);
            }
            finally
            {
                CultureInfo.CurrentUICulture = previous;
            }
        });
        return app;
    }

    /// <summary>Croatian when the most preferred language is Croatian; English otherwise.</summary>
    internal static CultureInfo Of(string? acceptLanguage)
    {
        if (string.IsNullOrWhiteSpace(acceptLanguage)
            || !StringWithQualityHeaderValue.TryParseList(acceptLanguage.Split(','), out var values)
            || values.Count == 0)
        {
            return AppLanguage.English;
        }
        var first = values
            .Select((value, index) => (value, index))
            .OrderByDescending(v => v.value.Quality ?? 1)
            .ThenBy(v => v.index)
            .First().value.Value.Value ?? string.Empty;
        return first.Equals("hr", StringComparison.OrdinalIgnoreCase)
            || first.StartsWith("hr-", StringComparison.OrdinalIgnoreCase)
            ? AppLanguage.Croatian
            : AppLanguage.English;
    }
}
