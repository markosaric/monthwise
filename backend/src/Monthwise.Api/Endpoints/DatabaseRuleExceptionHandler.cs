using Microsoft.AspNetCore.Diagnostics;
using Monthwise.Application.Common;

namespace Monthwise.Api.Endpoints;

/// <summary>
/// The database refused a change that passed the handler's checks (usually a race): report 409 or
/// 400 with a plain message instead of 500 (error mapping FR-1 to FR-5). The body holds only the
/// status and the message, never the refused values.
/// </summary>
internal sealed class DatabaseRuleExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not DatabaseRuleException { Rule: var rule })
        {
            return false;
        }

        var status = StatusOf(rule);
        httpContext.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = { Status = status, Title = exception.Message },
            Exception = exception,
        });
    }

    internal static int StatusOf(DatabaseRule rule) => rule switch
    {
        DatabaseRule.ValueNotAllowed => StatusCodes.Status400BadRequest,
        _ => StatusCodes.Status409Conflict,
    };
}
