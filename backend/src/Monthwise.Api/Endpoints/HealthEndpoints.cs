using Microsoft.Extensions.Diagnostics.HealthChecks;
using Monthwise.Infrastructure;

namespace Monthwise.Api.Endpoints;

internal static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/health", GetHealthAsync)
            .WithName("GetHealth")
            .WithTags("Health")
            .WithSummary("Reports whether the API and its database are reachable.")
            .Produces<HealthResponse>(StatusCodes.Status200OK)
            .Produces<HealthResponse>(StatusCodes.Status503ServiceUnavailable);

        return routes;
    }

    private static async Task<Microsoft.AspNetCore.Http.HttpResults.JsonHttpResult<HealthResponse>> GetHealthAsync(
        HealthCheckService healthChecks,
        CancellationToken cancellationToken)
    {
        var report = await healthChecks.CheckHealthAsync(cancellationToken);
        var database = report.Entries.TryGetValue(DependencyInjection.DatabaseHealthCheckName, out var entry)
            ? ToStatus(entry.Status)
            : HealthStatusValue.Unhealthy;

        var response = new HealthResponse(ToStatus(report.Status), database);
        var statusCode = report.Status == HealthStatus.Unhealthy
            ? StatusCodes.Status503ServiceUnavailable
            : StatusCodes.Status200OK;

        return TypedResults.Json(response, statusCode: statusCode);
    }

    private static HealthStatusValue ToStatus(HealthStatus status) => status switch
    {
        HealthStatus.Healthy => HealthStatusValue.Healthy,
        HealthStatus.Degraded => HealthStatusValue.Degraded,
        _ => HealthStatusValue.Unhealthy,
    };
}

internal sealed record HealthResponse(HealthStatusValue Status, HealthStatusValue Database);

[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<HealthStatusValue>))]
internal enum HealthStatusValue
{
    Healthy,
    Degraded,
    Unhealthy,
}
