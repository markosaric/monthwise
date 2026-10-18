using Monthwise.Application.Dashboard;

namespace Monthwise.Api.Endpoints;

/// <summary>The Dashboard of the current month (docs/feature-requirements/design-refresh.md FR-10).</summary>
internal static class DashboardEndpoints
{
    public static IEndpointRouteBuilder MapDashboardEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/dashboard", (GetDashboardHandler handler, CancellationToken ct) => handler.HandleAsync(ct))
            .WithTags("Dashboard")
            .WithName("GetDashboard")
            .WithSummary("How the current month is going: key figures, what is still to pay and coming up, and section summaries.");
        return routes;
    }
}
