using Microsoft.AspNetCore.Http.HttpResults;
using Monthwise.Domain.Common;

namespace Monthwise.Api.Endpoints;

/// <summary>Turns <see cref="Result"/>s into responses; failures become RFC 9457 problem details.</summary>
internal static class ResultHttpExtensions
{
    public static Results<Ok<T>, ValidationProblem, ProblemHttpResult> ToOk<T>(this Result<T> result)
    {
        if (result.IsSuccess)
        {
            return TypedResults.Ok(result.Value);
        }
        var (validation, problem) = ToProblem(result);
        return validation is not null ? validation : problem!;
    }

    public static Results<Created<T>, ValidationProblem, ProblemHttpResult> ToCreated<T>(
        this Result<T> result, Func<T, string> location)
    {
        if (result.IsSuccess)
        {
            return TypedResults.Created(location(result.Value), result.Value);
        }
        var (validation, problem) = ToProblem(result);
        return validation is not null ? validation : problem!;
    }

    public static Results<FileContentHttpResult, ValidationProblem, ProblemHttpResult> ToFile<T>(
        this Result<T> result, Func<T, FileContentHttpResult> file)
    {
        if (result.IsSuccess)
        {
            return file(result.Value);
        }
        var (validation, problem) = ToProblem(result);
        return validation is not null ? validation : problem!;
    }

    public static Results<NoContent, ValidationProblem, ProblemHttpResult> ToNoContent(this Result result)
    {
        if (result.IsSuccess)
        {
            return TypedResults.NoContent();
        }
        var (validation, problem) = ToProblem(result);
        return validation is not null ? validation : problem!;
    }

    /// <summary>Validation errors become a 400 with field keys; conflicts a 409; missing items a 404.</summary>
    private static (ValidationProblem? Validation, ProblemHttpResult? Problem) ToProblem(Result result)
    {
        var first = result.Errors[0];
        return first.Kind switch
        {
            ErrorKind.NotFound => (null, TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: first.Message)),
            ErrorKind.Conflict => (null, TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: first.Message,
                extensions: new Dictionary<string, object?> { ["code"] = first.Code })),
            _ => (TypedResults.ValidationProblem(
                result.Errors
                    .Where(e => e.Kind == ErrorKind.Validation)
                    .GroupBy(e => e.Field ?? string.Empty)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.Message).Distinct().ToArray())), null),
        };
    }
}
