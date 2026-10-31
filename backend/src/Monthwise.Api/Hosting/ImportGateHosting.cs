using Monthwise.Application.DataTransfer;

namespace Monthwise.Api.Hosting;

/// <summary>
/// While an import runs, every API call except the health check and the Data and backups overview
/// answers 503, so nobody changes data that is being replaced (backup and restore FR-7).
/// </summary>
internal static class ImportGateHosting
{
    public static WebApplication UseImportGate(this WebApplication app)
    {
        var gate = app.Services.GetRequiredService<ImportGate>();
        app.Use(async (context, next) =>
        {
            var path = context.Request.Path;
            if (gate.IsRunning
                && path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
                && !path.StartsWithSegments("/api/health", StringComparison.OrdinalIgnoreCase)
                && !(HttpMethods.IsGet(context.Request.Method) && path.Equals("/api/data", StringComparison.OrdinalIgnoreCase)))
            {
                await TypedResults.Problem(
                        title: "An import is running. Try again in a moment.",
                        statusCode: StatusCodes.Status503ServiceUnavailable,
                        extensions: new Dictionary<string, object?> { ["code"] = "data.import_running" })
                    .ExecuteAsync(context);
                return;
            }

            await next(context);
        });
        return app;
    }
}
