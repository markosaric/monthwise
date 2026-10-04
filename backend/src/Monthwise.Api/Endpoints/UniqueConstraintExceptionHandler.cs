using Microsoft.AspNetCore.Diagnostics;
using Monthwise.Application.Common;

namespace Monthwise.Api.Endpoints;

/// <summary>A unique index rejected a change that raced past the handler's checks: report 409, not 500.</summary>
internal sealed class UniqueConstraintExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not UniqueConstraintException)
        {
            return false;
        }

        httpContext.Response.StatusCode = StatusCodes.Status409Conflict;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = { Status = StatusCodes.Status409Conflict, Title = exception.Message },
            Exception = exception,
        });
    }
}
