using Microsoft.AspNetCore.Http.HttpResults;
using Monthwise.Application.Loans;
using Monthwise.Application.SharedCosts;
using Monthwise.Domain.Common;
using Monthwise.Domain.Loans;
using Monthwise.Domain.SharedCosts;

namespace Monthwise.Api.Endpoints;

/// <summary>
/// Participants, shares of single loan payments, contributions and the Shared costs screen
/// (docs/feature-requirements/shared-bills.md). Bills, recurring bills and loans are shared through their
/// own requests.
/// </summary>
internal static class SharedCostEndpoints
{
    public static IEndpointRouteBuilder MapSharedCostEndpoints(this IEndpointRouteBuilder routes)
    {
        MapParticipants(routes);
        MapLoanPaymentShares(routes);
        MapContributions(routes);

        routes.MapGet("/shared-costs", async Task<Results<Ok<SharedCostsResponse>, ValidationProblem>> (
                string month, GetSharedCostsHandler handler, CancellationToken ct) =>
                YearMonth.TryParse(month, out var parsed)
                    ? TypedResults.Ok(await handler.HandleAsync(parsed, ct))
                    : InvalidMonth())
            .WithTags("Shared costs")
            .WithName("GetSharedCosts")
            .WithSummary("Each participant's shared costs, the month's (yyyy-MM) parts and contributions, and what is outstanding.");
        return routes;
    }

    private static void MapParticipants(IEndpointRouteBuilder routes)
    {
        var participants = routes.MapGroup("/participants").WithTags("Shared costs");

        participants.MapGet("/", (bool? includeArchived, ParticipantHandler handler, CancellationToken ct) =>
                handler.ListAsync(includeArchived ?? false, ct))
            .WithName("GetParticipants")
            .WithSummary("The people costs are shared with.");

        participants.MapGet("/{id:guid}", async (Guid id, ParticipantHandler handler, CancellationToken ct) =>
                (await handler.GetAsync(id, ct)).ToOk())
            .WithName("GetParticipant")
            .ProducesProblem(StatusCodes.Status404NotFound);

        participants.MapPost("/", async (ParticipantRequest request, ParticipantHandler handler, CancellationToken ct) =>
                (await handler.CreateAsync(request, ct)).ToCreated(p => $"/api/participants/{p.Id}"))
            .WithName("CreateParticipant");

        participants.MapPut("/{id:guid}", async (Guid id, ParticipantRequest request, ParticipantHandler handler, CancellationToken ct) =>
                (await handler.UpdateAsync(id, request, ct)).ToOk())
            .WithName("UpdateParticipant")
            .WithSummary("Edits a participant; the payer of their contributions is renamed with them.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        participants.MapPost("/{id:guid}/archive", async (Guid id, ParticipantHandler handler, CancellationToken ct) =>
                (await handler.ArchiveAsync(id, ct)).ToOk())
            .WithName("ArchiveParticipant")
            .ProducesProblem(StatusCodes.Status404NotFound);

        participants.MapPost("/{id:guid}/restore", async (Guid id, ParticipantHandler handler, CancellationToken ct) =>
                (await handler.RestoreAsync(id, ct)).ToOk())
            .WithName("RestoreParticipant")
            .ProducesProblem(StatusCodes.Status404NotFound);

        participants.MapDelete("/{id:guid}", async (Guid id, ParticipantHandler handler, CancellationToken ct) =>
                (await handler.DeleteAsync(id, ct)).ToNoContent())
            .WithName("DeleteParticipant")
            .WithSummary("Deletes a participant that shares no cost.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static void MapLoanPaymentShares(IEndpointRouteBuilder routes)
    {
        var share = routes.MapGroup("/loans/{id:guid}/payments/{month}/{kind}/share").WithTags("Loans");

        share.MapPut("/", async (
                Guid id, string month, LoanPaymentKind kind, LoanPaymentShareRequest request,
                LoanPaymentHandler handler, CancellationToken ct) =>
                YearMonth.TryParse(month, out var parsed)
                    ? (await handler.ChangeShareAsync(id, parsed, kind, request, ct)).ToOk()
                    : InvalidMonth())
            .WithName("ChangeLoanPaymentShare")
            .WithSummary("Shares one payment, changes its share or stops sharing it (share null), whatever the loan's share is.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        share.MapDelete("/", async (
                Guid id, string month, LoanPaymentKind kind, LoanPaymentHandler handler, CancellationToken ct) =>
                YearMonth.TryParse(month, out var parsed)
                    ? (await handler.ResetShareAsync(id, parsed, kind, ct)).ToOk()
                    : InvalidMonth())
            .WithName("ResetLoanPaymentShare")
            .WithSummary("The payment follows the loan's share again.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static void MapContributions(IEndpointRouteBuilder routes)
    {
        var bill = routes.MapGroup("/bills/{id:guid}/contribution").WithTags("Shared costs");

        bill.MapPut("/", async (Guid id, ContributionRequest request, ContributionHandler handler, CancellationToken ct) =>
                (await handler.EditAsync(ContributionSource.ForBill(id), request, ct)).ToOk())
            .WithName("EditBillContribution")
            .WithSummary(EditSummary)
            .ProducesProblem(StatusCodes.Status404NotFound);

        bill.MapPost("/received", async (Guid id, ContributionReceivedRequest? request, ContributionHandler handler, CancellationToken ct) =>
                (await handler.MarkReceivedAsync(ContributionSource.ForBill(id), request, ct)).ToOk())
            .WithName("MarkBillContributionReceived")
            .WithSummary(ReceivedSummary)
            .ProducesProblem(StatusCodes.Status404NotFound);

        bill.MapDelete("/received", async (Guid id, ContributionHandler handler, CancellationToken ct) =>
                (await handler.MarkExpectedAsync(ContributionSource.ForBill(id), ct)).ToOk())
            .WithName("MarkBillContributionExpected")
            .WithSummary(ExpectedSummary)
            .ProducesProblem(StatusCodes.Status404NotFound);

        bill.MapPost("/reset", async (Guid id, ContributionHandler handler, CancellationToken ct) =>
                (await handler.ResetAsync(ContributionSource.ForBill(id), ct)).ToOk())
            .WithName("ResetBillContribution")
            .WithSummary(ResetSummary)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        var payment = routes.MapGroup("/loans/{id:guid}/payments/{month}/{kind}/contribution").WithTags("Shared costs");

        payment.MapPut("/", async (
                Guid id, string month, LoanPaymentKind kind, ContributionRequest request, ContributionHandler handler, CancellationToken ct) =>
                YearMonth.TryParse(month, out var parsed)
                    ? (await handler.EditAsync(ContributionSource.ForLoanPayment(id, parsed, kind), request, ct)).ToOk()
                    : InvalidMonth())
            .WithName("EditLoanPaymentContribution")
            .WithSummary(EditSummary)
            .ProducesProblem(StatusCodes.Status404NotFound);

        payment.MapPost("/received", async (
                Guid id, string month, LoanPaymentKind kind, ContributionReceivedRequest? request,
                ContributionHandler handler, CancellationToken ct) =>
                YearMonth.TryParse(month, out var parsed)
                    ? (await handler.MarkReceivedAsync(ContributionSource.ForLoanPayment(id, parsed, kind), request, ct)).ToOk()
                    : InvalidMonth())
            .WithName("MarkLoanPaymentContributionReceived")
            .WithSummary(ReceivedSummary)
            .ProducesProblem(StatusCodes.Status404NotFound);

        payment.MapDelete("/received", async (
                Guid id, string month, LoanPaymentKind kind, ContributionHandler handler, CancellationToken ct) =>
                YearMonth.TryParse(month, out var parsed)
                    ? (await handler.MarkExpectedAsync(ContributionSource.ForLoanPayment(id, parsed, kind), ct)).ToOk()
                    : InvalidMonth())
            .WithName("MarkLoanPaymentContributionExpected")
            .WithSummary(ExpectedSummary)
            .ProducesProblem(StatusCodes.Status404NotFound);

        payment.MapPost("/reset", async (
                Guid id, string month, LoanPaymentKind kind, ContributionHandler handler, CancellationToken ct) =>
                YearMonth.TryParse(month, out var parsed)
                    ? (await handler.ResetAsync(ContributionSource.ForLoanPayment(id, parsed, kind), ct)).ToOk()
                    : InvalidMonth())
            .WithName("ResetLoanPaymentContribution")
            .WithSummary(ResetSummary)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private const string EditSummary = "Edits the contribution; it then keeps its values until it is reset to the part.";
    private const string ReceivedSummary = "Ticks the contribution as received: today, to its account, unless given.";
    private const string ExpectedSummary = "Unticks Received; a contribution that was not edited follows its cost again.";
    private const string ResetSummary =
        "Reset to part: an expected contribution follows its cost again, a received one gets the current part.";

    private static ValidationProblem InvalidMonth() =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["month"] = ["Enter the month as yyyy-MM."] });
}
