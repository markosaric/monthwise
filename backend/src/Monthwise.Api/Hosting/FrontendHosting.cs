using Microsoft.Net.Http.Headers;

namespace Monthwise.Api.Hosting;

/// <summary>
/// Serves the built React front-end from <c>wwwroot</c> in a release, on the same origin as the API
/// (ADR 0006). In development there is no <c>wwwroot</c> and Vite serves the front-end.
/// </summary>
internal static class FrontendHosting
{
    private const string IndexFile = "index.html";

    /// <summary>
    /// Static files, then for requests no endpoint matched: <c>/api/...</c> answers 404 problem
    /// details, and every other GET of a screen address answers <c>index.html</c>.
    /// </summary>
    /// <remarks>
    /// A middleware instead of <c>MapFallback</c>: a catch-all fallback route wins over some of the
    /// longer API routes (e.g. <c>/loans/{id}/payments/{month}/{kind}/contribution/received</c>).
    /// </remarks>
    public static WebApplication UseFrontend(this WebApplication app)
    {
        var index = app.Environment.WebRootFileProvider.GetFileInfo(IndexFile);
        if (index.Exists)
        {
            app.UseDefaultFiles();
            app.UseStaticFiles(new StaticFileOptions { OnPrepareResponse = context => SetCacheControl(context.Context) });
        }

        app.Use(async (context, next) =>
        {
            if (context.GetEndpoint() is not null)
            {
                await next(context);
                return;
            }

            var path = context.Request.Path;
            if (path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
            {
                await TypedResults.Problem(statusCode: StatusCodes.Status404NotFound).ExecuteAsync(context);
                return;
            }

            if (index.Exists && (HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method))
                && !Path.HasExtension(path.Value))
            {
                SetCacheControl(context);
                context.Response.ContentType = "text/html; charset=utf-8";
                if (HttpMethods.IsGet(context.Request.Method))
                {
                    await context.Response.SendFileAsync(index);
                }

                return;
            }

            await next(context);
        });
        return app;
    }

    private static void SetCacheControl(HttpContext context) =>
        // Vite puts content-hashed files in /assets; everything else (index.html) must be revalidated.
        context.Response.Headers[HeaderNames.CacheControl] =
            context.Request.Path.StartsWithSegments("/assets", StringComparison.OrdinalIgnoreCase)
                ? "public, max-age=31536000, immutable"
                : "no-cache";
}
