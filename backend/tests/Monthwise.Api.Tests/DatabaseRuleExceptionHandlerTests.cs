using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Monthwise.Api.Endpoints;
using Monthwise.Application.Common;
using Monthwise.Infrastructure.Persistence;
using Npgsql;

namespace Monthwise.Api.Tests;

/// <summary>Database rule errors become 409 or 400 problem details without record values (error mapping FR-1 to FR-5).</summary>
public sealed class DatabaseRuleExceptionHandlerTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(DatabaseRule.Unique, StatusCodes.Status409Conflict)]
    [InlineData(DatabaseRule.MissingReference, StatusCodes.Status409Conflict)]
    [InlineData(DatabaseRule.StillReferenced, StatusCodes.Status409Conflict)]
    [InlineData(DatabaseRule.ValueNotAllowed, StatusCodes.Status400BadRequest)]
    public async Task Each_rule_answers_its_status_with_only_the_plain_message(DatabaseRule rule, int status)
    {
        var secret = new PostgresException("Key (name)=(Secret card) already exists.", "ERROR", "ERROR", "23505");
        var (handled, context, body) = await HandleAsync(new DatabaseRuleException(rule, secret));

        Assert.True(handled);
        Assert.Equal(status, context.Response.StatusCode);
        Assert.Equal(status, body.GetProperty("status").GetInt32());
        Assert.Equal(DatabaseRuleException.MessageOf(rule), body.GetProperty("title").GetString());
        Assert.False(body.TryGetProperty("detail", out _));
        Assert.False(body.TryGetProperty("errors", out _));
        Assert.DoesNotContain("Secret card", body.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Other_exceptions_are_left_to_the_default_handling()
    {
        var (handled, _, _) = await HandleAsync(new InvalidOperationException("boom"));

        Assert.False(handled);
    }

    [Theory]
    [InlineData(PostgresErrorCodes.UniqueViolation, null, DatabaseRule.Unique)]
    [InlineData(PostgresErrorCodes.ForeignKeyViolation, "card", DatabaseRule.MissingReference)]
    [InlineData(PostgresErrorCodes.ForeignKeyViolation, "loan", DatabaseRule.StillReferenced)]
    [InlineData(PostgresErrorCodes.ForeignKeyViolation, null, DatabaseRule.StillReferenced)]
    [InlineData(PostgresErrorCodes.NotNullViolation, null, DatabaseRule.ValueNotAllowed)]
    [InlineData(PostgresErrorCodes.CheckViolation, null, DatabaseRule.ValueNotAllowed)]
    [InlineData(PostgresErrorCodes.ExclusionViolation, null, DatabaseRule.ValueNotAllowed)]
    public void PostgreSQL_errors_are_translated_by_code_and_written_table(string sqlState, string? table, DatabaseRule expected)
    {
        var error = PostgresError(sqlState, table);

        Assert.Equal(expected, DatabaseRuleTranslator.Translate(error, written => written == "card"));
    }

    [Theory]
    [InlineData(PostgresErrorCodes.StringDataRightTruncation)]
    [InlineData(PostgresErrorCodes.DeadlockDetected)]
    [InlineData(PostgresErrorCodes.SerializationFailure)]
    public void Other_PostgreSQL_errors_are_not_translated(string sqlState)
    {
        Assert.Null(DatabaseRuleTranslator.Translate(PostgresError(sqlState, null), _ => true));
    }

    private static PostgresException PostgresError(string sqlState, string? table) =>
        new("refused", "ERROR", "ERROR", sqlState, tableName: table);

    private static async Task<(bool Handled, HttpContext Context, JsonElement Body)> HandleAsync(Exception exception)
    {
        var services = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Response.Body = new MemoryStream();
        var handler = ActivatorUtilities.CreateInstance<DatabaseRuleExceptionHandler>(services);

        var handled = await handler.TryHandleAsync(context, exception, Ct);

        context.Response.Body.Position = 0;
        var body = handled && context.Response.Body.Length > 0
            ? await JsonSerializer.DeserializeAsync<JsonElement>(context.Response.Body, cancellationToken: Ct)
            : default;
        return (handled, context, body);
    }
}
