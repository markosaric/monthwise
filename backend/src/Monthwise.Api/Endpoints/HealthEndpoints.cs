using Microsoft.Extensions.Diagnostics.HealthChecks;
using Monthwise.Api.Hosting;
using Monthwise.Application.DataTransfer;
using Monthwise.Infrastructure;
using Monthwise.Infrastructure.Persistence;

namespace Monthwise.Api.Endpoints;

internal static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/health", GetHealthAsync)
            .WithName("GetHealth")
            .WithTags("Health")
            .WithSummary("Reports whether the API and its database work, the migration state and the running version.")
            .Produces<HealthResponse>(StatusCodes.Status200OK)
            .Produces<HealthResponse>(StatusCodes.Status503ServiceUnavailable);

        return routes;
    }

    private static async Task<Microsoft.AspNetCore.Http.HttpResults.JsonHttpResult<HealthResponse>> GetHealthAsync(
        HealthCheckService healthChecks,
        DatabaseMigrationState migrationState,
        ImportGate importGate,
        CancellationToken cancellationToken)
    {
        var report = await healthChecks.CheckHealthAsync(cancellationToken);
        var database = report.Entries.TryGetValue(DependencyInjection.DatabaseHealthCheckName, out var entry)
            ? ToStatus(entry.Status)
            : HealthStatusValue.Unhealthy;

        var migrations = migrationState.Status switch
        {
            MigrationStatus.Applied => MigrationStatusValue.Applied,
            MigrationStatus.Failed => MigrationStatusValue.Failed,
            _ => MigrationStatusValue.Pending,
        };
        var status = migrations == MigrationStatusValue.Failed ? HealthStatusValue.Unhealthy : ToStatus(report.Status);

        var response = new HealthResponse(status, database, migrations, AppVersion.Current, importGate.LastImportedAt);
        var statusCode = status == HealthStatusValue.Unhealthy
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

/// <summary>
/// Only states, the version and when data was last imported (so open screens reload): the health
/// address is reachable without login and shows no household data.
/// </summary>
internal sealed record HealthResponse(
    HealthStatusValue Status,
    HealthStatusValue Database,
    MigrationStatusValue Migrations,
    string Version,
    DateTimeOffset? DataImportedAt);

[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<HealthStatusValue>))]
internal enum HealthStatusValue
{
    Healthy,
    Degraded,
    Unhealthy,
}

[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<MigrationStatusValue>))]
internal enum MigrationStatusValue
{
    Pending,
    Applied,
    Failed,
}
