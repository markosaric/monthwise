using Monthwise.Application.Common;
using Npgsql;

namespace Monthwise.Infrastructure.Persistence;

/// <summary>
/// Turns a PostgreSQL rule violation into the <see cref="DatabaseRule"/> the user is told about
/// (error mapping FR-1 to FR-5). Only error codes and table names are read, never the server's
/// message text, which depends on its language and may hold row values.
/// </summary>
internal static class DatabaseRuleTranslator
{
    /// <param name="error">The error PostgreSQL reported.</param>
    /// <param name="writesTo">
    /// Whether the refused change adds or modifies rows of the given table. PostgreSQL names the
    /// referencing table for both kinds of foreign key violation, so this tells them apart.
    /// </param>
    /// <returns>The rule, or <c>null</c> for an error that is not a rule violation (it stays a 500).</returns>
    public static DatabaseRule? Translate(PostgresException error, Func<string, bool> writesTo) => error.SqlState switch
    {
        PostgresErrorCodes.UniqueViolation => DatabaseRule.Unique,
        PostgresErrorCodes.ForeignKeyViolation =>
            error.TableName is { } table && writesTo(table) ? DatabaseRule.MissingReference : DatabaseRule.StillReferenced,
        PostgresErrorCodes.NotNullViolation
            or PostgresErrorCodes.CheckViolation
            or PostgresErrorCodes.ExclusionViolation => DatabaseRule.ValueNotAllowed,
        _ => null,
    };
}
