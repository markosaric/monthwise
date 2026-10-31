using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Monthwise.Application.DataTransfer;

namespace Monthwise.Api.Endpoints;

/// <summary>Export, import and their history (docs/feature-requirements/backup-and-restore.md).</summary>
internal static class DataEndpoints
{
    /// <summary>Who may export and import (FR-1). Login comes in a later phase; until then it allows every request.</summary>
    public const string HouseholdAdminPolicy = "HouseholdAdmin";

    /// <summary>The largest export file an import accepts.</summary>
    public const long MaxFileBytes = 100L * 1024 * 1024;

    public static IServiceCollection AddDataPolicies(this IServiceCollection services) =>
        services.AddAuthorizationBuilder()
            .AddPolicy(HouseholdAdminPolicy, policy => policy.RequireAssertion(_ => true))
            .Services;

    public static IEndpointRouteBuilder MapDataEndpoints(this IEndpointRouteBuilder routes)
    {
        var data = routes.MapGroup("/data").WithTags("Data and backups");

        data.MapGet("/", (DataOverviewHandler handler, CancellationToken ct) => handler.HandleAsync(ct))
            .WithName("GetDataOverview")
            .WithSummary("The last export and import, the safety copy, the export reminder and whether this user may export and import.");

        data.MapGet("/history", (int? page, DataHistoryHandler handler, CancellationToken ct) => handler.HandleAsync(page ?? 1, ct))
            .WithName("GetDataHistory")
            .WithSummary("Every export, import and safety copy, newest first, 50 per page.");

        data.MapPost("/export", async Task<Results<FileContentHttpResult, ValidationProblem, ProblemHttpResult>> (
                ExportRequest request, HttpContext http, ExportDataHandler handler, CancellationToken ct) =>
            {
                // The file holds personal financial data: never cache it in the browser or a proxy.
                http.Response.Headers.CacheControl = "no-store";
                return (await handler.HandleAsync(request, ct))
                    .ToFile(file => TypedResults.File(file.Content, "application/octet-stream", file.FileName));
            })
            .WithName("ExportData")
            .WithSummary("Exports all household data to a file protected with the given password (at least 6 characters).")
            .Produces<byte[]>(StatusCodes.Status200OK, "application/octet-stream")
            .RequireAuthorization(HouseholdAdminPolicy);

        data.MapPost("/import/check", async (
                [FromForm] IFormFile? file, [FromForm] string? password, ImportDataHandler handler, CancellationToken ct) =>
                (await handler.CheckAsync(await ReadAsync(file, ct), password, ct)).ToOk())
            .WithName("CheckImport")
            .WithSummary("Reads an export file and shows what it holds next to what Monthwise holds now. Changes nothing.")
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(MaxFileBytes))
            .RequireAuthorization(HouseholdAdminPolicy);

        data.MapPost("/import", async (
                [FromForm] IFormFile? file,
                [FromForm] string? password,
                [FromForm] string? confirmation,
                ImportDataHandler handler,
                CancellationToken ct) =>
                (await handler.ImportAsync(await ReadAsync(file, ct), file?.FileName, password, confirmation, ct)).ToOk())
            .WithName("ImportData")
            .WithSummary("Replaces all household data with the data of an export file. Needs the confirmation word REPLACE; makes a safety copy first.")
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(MaxFileBytes))
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization(HouseholdAdminPolicy);

        data.MapPost("/safety-copy/restore", async (SafetyCopyRestoreRequest request, ImportDataHandler handler, CancellationToken ct) =>
                (await handler.RestoreSafetyCopyAsync(request, ct)).ToOk())
            .WithName("RestoreSafetyCopy")
            .WithSummary("Brings back the data as it was before the newest import. Needs the confirmation word REPLACE.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization(HouseholdAdminPolicy);

        data.MapPut("/reminder", async (BackupReminderRequest request, BackupReminderHandler handler, CancellationToken ct) =>
                (await handler.UpdateAsync(request, ct)).ToOk())
            .WithName("UpdateBackupReminder")
            .WithSummary("Turns the export reminder on or off and sets its interval (7, 14, 30, 60 or 90 days).")
            .RequireAuthorization(HouseholdAdminPolicy);

        data.MapPost("/reminder/dismiss", (BackupReminderHandler handler, CancellationToken ct) => handler.DismissAsync(ct))
            .WithName("DismissBackupReminder")
            .WithSummary("Hides the export reminder on the Dashboard for one interval.");

        return routes;
    }

    private static async Task<byte[]?> ReadAsync(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return null;
        }
        using var buffer = new MemoryStream((int)Math.Min(file.Length, MaxFileBytes));
        await file.CopyToAsync(buffer, cancellationToken);
        return buffer.ToArray();
    }
}
