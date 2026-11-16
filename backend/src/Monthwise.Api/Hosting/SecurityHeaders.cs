using Microsoft.Net.Http.Headers;

namespace Monthwise.Api.Hosting;

/// <summary>
/// Tells the browser how to protect the app (security headers FR-1 to FR-4). Every answer gets the
/// headers, including 404s, gate refusals and answers rebuilt by the exception handler: they are
/// added when the answer starts, after the exception handler has cleared the failed answer's headers.
/// All values live here, so a later change (for example a new allowed source) touches one place.
/// </summary>
internal static class SecurityHeaders
{
    /// <summary>
    /// Only the app's own files load and only its own script files run (FR-2). Styles may also be
    /// added at run time, because the notification component inserts a style element; this allows
    /// styles only, never scripts. Small images may be inline data addresses.
    /// </summary>
    public const string ContentSecurityPolicy =
        "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; "
        + "font-src 'self'; connect-src 'self'; object-src 'none'; base-uri 'none'; form-action 'self'; "
        + "frame-src 'none'; frame-ancestors 'none'";

    /// <summary>The device features the app never uses (FR-3).</summary>
    public const string PermissionsPolicy =
        "camera=(), microphone=(), geolocation=(), payment=(), usb=(), browsing-topics=()";

    /// <summary>One year, sent over HTTPS only (FR-4). No subdomains and no preload (out of scope).</summary>
    public const string StrictTransportSecurity = "max-age=31536000";

    /// <summary>
    /// Adds the headers to every answer. Call it right after the forwarded headers, so the scheme
    /// is already the one the browser used, and before anything that can answer a request.
    /// </summary>
    public static WebApplication UseSecurityHeaders(this WebApplication app)
    {
        var development = app.Environment.IsDevelopment();
        app.Use(async (context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                Apply(context, development);
                return Task.CompletedTask;
            });
            await next(context);
        });
        return app;
    }

    private static void Apply(HttpContext context, bool development)
    {
        var headers = context.Response.Headers;

        // The API reference page in development runs inline scripts and loads Scalar's fonts
        // (FR-5); these addresses do not exist in a release.
        if (!(development && IsDevelopmentTool(context.Request.Path)))
        {
            headers.ContentSecurityPolicy = ContentSecurityPolicy;
        }

        headers.XFrameOptions = "DENY";
        headers.XContentTypeOptions = "nosniff";
        headers["Referrer-Policy"] = "no-referrer";
        headers["Permissions-Policy"] = PermissionsPolicy;

        if (context.Request.IsHttps)
        {
            headers[HeaderNames.StrictTransportSecurity] = StrictTransportSecurity;
        }
    }

    private static bool IsDevelopmentTool(PathString path) =>
        path.StartsWithSegments("/scalar", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/openapi", StringComparison.OrdinalIgnoreCase);
}
