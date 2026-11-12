using System.Text.Json.Nodes;
using Monthwise.Application.DataTransfer;
using Monthwise.Domain.Common;
using Monthwise.Domain.DataTransfer;
using Monthwise.Infrastructure.Persistence;

namespace Monthwise.Infrastructure.DataTransfer;

/// <summary>
/// Refuses a backup whose name list holds two entries with the same name (name comparison FR-7),
/// before anything is restored, with the same rule as the unique indexes (<see cref="NameCollation"/>).
/// </summary>
internal static class SameNameCheck
{
    /// <param name="Table">The table of the name list.</param>
    /// <param name="List">How the list is called in messages.</param>
    /// <param name="OnlyWhereNull">A column that must be empty for the row to count (closed or stopped entries do not).</param>
    /// <param name="Within">A column whose value scopes uniqueness (subcategories within their category).</param>
    private sealed record NameList(string Table, string List, string? OnlyWhereNull = null, string? Within = null);

    private static readonly NameList[] Lists =
    [
        new("card", "cards"),
        new("card_issuer", "card issuers"),
        new("bank", "banks"),
        new("payer", "payers"),
        new("input_category", "input categories"),
        new("loan_category", "loan categories"),
        new("loan_subcategory", "loan subcategories", Within: "category_id"),
        new("loan", "loans", OnlyWhereNull: "closed_on"),
        new("expense_category", "expense categories"),
        new("recurring_bill", "recurring bills", OnlyWhereNull: "stopped_on"),
        new("participant", "participants"),
    ];

    public static Result Check(HouseholdData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        foreach (var list in Lists)
        {
            // A file from an older release may not have the list yet.
            if (!data.Tables.TryGetPropertyValue(list.Table, out var node) || node is not JsonArray rows)
            {
                continue;
            }

            var counted = rows
                .Select(r => r as JsonObject)
                .Where(r => r is not null && (list.OnlyWhereNull is null || r[list.OnlyWhereNull] is null))
                .Select(r => (Within: list.Within is null ? null : r![list.Within]?.ToJsonString(), Name: Text(r!["name"])))
                .Where(r => r.Name is not null)
                .ToList();

            var seen = new List<(string? Within, string Name)>();
            foreach (var row in counted)
            {
                var same = seen.FirstOrDefault(s => s.Within == row.Within && NameCollation.AreSame(s.Name, row.Name!));
                if (same.Name is not null)
                {
                    return Result.Failure(DataTransferErrors.SameNames(list.List, same.Name, row.Name!));
                }
                seen.Add((row.Within, row.Name!));
            }
        }
        return Result.Success();
    }

    private static string? Text(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
}
