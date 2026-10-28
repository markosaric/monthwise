using Monthwise.Infrastructure.Persistence;

namespace Monthwise.Api.Hosting;

/// <summary>While startup migrations failed, every API call except the health check answers 503.</summary>
internal static class MigrationGate
{
    public static WebApplication UseMigrationGate(this WebApplication app)
    {
        var state = app.Services.GetRequiredService<DatabaseMigrationState>();
        app.Use(async (context, next) =>
        {
            if (state.Status == MigrationStatus.Failed
                && context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
                && !context.Request.Path.StartsWithSegments("/api/health", StringComparison.OrdinalIgnoreCase))
            {
                await TypedResults.Problem(
                        title: "The database could not be brought up to date. See the log for details.",
                        statusCode: StatusCodes.Status503ServiceUnavailable)
                    .ExecuteAsync(context);
                return;
            }

            await next(context);
        });
        return app;
    }
}
