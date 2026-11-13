using Microsoft.AspNetCore.Http.HttpResults;
using Monthwise.Domain.Common;

namespace Monthwise.Api.Endpoints;

/// <summary>
/// The one way endpoints read a month from the route or the query string
/// (docs/feature-requirements/month-range.md FR-1): the ISO form <c>yyyy-MM</c>, inside
/// <see cref="SupportedRange"/>. Every endpoint accepts and refuses the same months.
/// </summary>
internal static class MonthParameter
{
    public const string FormatMessage = "Enter the month as yyyy-MM.";

    public static bool TryParse(string? value, out YearMonth month) => YearMonth.TryParseSupported(value, out month);

    /// <summary>The validation problem for a month <see cref="TryParse"/> refused.</summary>
    public static ValidationProblem Invalid(string field, string? value) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]>
        {
            [field] = [YearMonth.TryParse(value, out _) ? SupportedRange.MonthMessage : FormatMessage],
        });
}
