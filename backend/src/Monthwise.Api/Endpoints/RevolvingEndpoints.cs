using Microsoft.AspNetCore.Http.HttpResults;
using Monthwise.Application.Revolving;
using Monthwise.Domain.Common;

namespace Monthwise.Api.Endpoints;

/// <summary>Revolving credit cards: debt, monthly bills and extra payments (docs/feature-requirements/revolving-credit-card.md).</summary>
internal static class RevolvingEndpoints
{
    public static IEndpointRouteBuilder MapRevolvingEndpoints(this IEndpointRouteBuilder routes)
    {
        MapCards(routes);
        MapBills(routes);
        MapPaymentsAndEntries(routes);
        return routes;
    }

    private static void MapCards(IEndpointRouteBuilder routes)
    {
        var cards = routes.MapGroup("/revolving-cards").WithTags("Revolving cards");

        cards.MapGet("/", (GetRevolvingCardsHandler handler, CancellationToken ct) => handler.HandleAsync(ct))
            .WithName("GetRevolvingCards")
            .WithSummary("Revolving cards with their debt, remaining limit and next monthly bill, and the debt of all of them.");

        cards.MapGet("/{id:guid}", async Task<Results<Ok<RevolvingCardDetailsResponse>, ValidationProblem, ProblemHttpResult>> (
                Guid id, string? to, GetRevolvingCardHandler handler, CancellationToken ct) =>
            {
                if (to is null)
                {
                    return (await handler.HandleAsync(id, null, ct)).ToOk();
                }
                return YearMonth.TryParse(to, out var parsed) ? (await handler.HandleAsync(id, parsed, ct)).ToOk() : InvalidMonth("to");
            })
            .WithName("GetRevolvingCard")
            .WithSummary("A revolving card with its terms, monthly bills (estimated up to `to`, yyyy-MM), extra payments, charges, debt entries and debt history.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        cards.MapGet("/{id:guid}/debt", async (Guid id, DateOnly? date, GetRevolvingCardHandler handler, CancellationToken ct) =>
                (await handler.DebtAsync(id, date, ct)).ToOk())
            .WithName("GetRevolvingDebt")
            .WithSummary("The debt of a revolving card at the end of a date (today by default).")
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static void MapBills(IEndpointRouteBuilder routes)
    {
        routes.MapGet("/revolving-bills", async Task<Results<Ok<MonthRevolvingResponse>, ValidationProblem>> (
                string month, GetMonthRevolvingHandler handler, CancellationToken ct) =>
                YearMonth.TryParse(month, out var parsed)
                    ? TypedResults.Ok(await handler.HandleAsync(parsed, ct))
                    : InvalidMonth("month"))
            .WithTags("Revolving cards")
            .WithName("GetMonthRevolvingBills")
            .WithSummary("Monthly bills and extra payments of revolving cards in a month (yyyy-MM), with totals.");

        var bill = routes.MapGroup("/revolving-cards/{id:guid}/bills/{month}").WithTags("Revolving cards");

        bill.MapPost("/paid", async (
                Guid id, string month, RevolvingBillPaidRequest request, RevolvingBillHandler handler, CancellationToken ct) =>
                YearMonth.TryParse(month, out var parsed) ? (await handler.MarkPaidAsync(id, parsed, request, ct)).ToOk() : InvalidMonth("month"))
            .WithName("MarkRevolvingBillPaid")
            .WithSummary("Pays a monthly bill. Defaults: today, the card's paying account and the minimum payment.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        bill.MapDelete("/paid", async (Guid id, string month, RevolvingBillHandler handler, CancellationToken ct) =>
                YearMonth.TryParse(month, out var parsed) ? (await handler.MarkUnpaidAsync(id, parsed, ct)).ToOk() : InvalidMonth("month"))
            .WithName("MarkRevolvingBillUnpaid")
            .WithSummary("Unticks Paid, also for a bill paid automatically.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        bill.MapPut("/correction", async (
                Guid id, string month, RevolvingBillCorrectionRequest request, RevolvingBillHandler handler, CancellationToken ct) =>
                YearMonth.TryParse(month, out var parsed) ? (await handler.CorrectAsync(id, parsed, request, ct)).ToOk() : InvalidMonth("month"))
            .WithName("CorrectRevolvingBill")
            .WithSummary("Sets the minimum payment and interest of the bank's bill; null keeps the calculated value.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        bill.MapDelete("/correction", async (Guid id, string month, RevolvingBillHandler handler, CancellationToken ct) =>
                YearMonth.TryParse(month, out var parsed) ? (await handler.ClearCorrectionAsync(id, parsed, ct)).ToOk() : InvalidMonth("month"))
            .WithName("ClearRevolvingBillCorrection")
            .WithSummary("Goes back to the calculated minimum payment and interest.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static void MapPaymentsAndEntries(IEndpointRouteBuilder routes)
    {
        routes.MapPost("/revolving-cards/{id:guid}/extra-payments", async (
                Guid id, ExtraPaymentRequest request, ExtraPaymentHandler handler, CancellationToken ct) =>
                (await handler.CreateAsync(id, request, ct)).ToCreated(p => $"/api/extra-payments/{p.Id}"))
            .WithTags("Revolving cards")
            .WithName("CreateExtraPayment")
            .WithSummary("Records an extra payment from an account, paid on its date, of at most the debt on that date.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        var payments = routes.MapGroup("/extra-payments").WithTags("Revolving cards");

        payments.MapPut("/{id:guid}", async (Guid id, ExtraPaymentRequest request, ExtraPaymentHandler handler, CancellationToken ct) =>
                (await handler.UpdateAsync(id, request, ct)).ToOk())
            .WithName("UpdateExtraPayment")
            .WithSummary("Edits an extra payment.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        payments.MapDelete("/{id:guid}", async (Guid id, ExtraPaymentHandler handler, CancellationToken ct) =>
                (await handler.DeleteAsync(id, ct)).ToNoContent())
            .WithName("DeleteExtraPayment")
            .WithSummary("Deletes an extra payment.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        routes.MapPost("/revolving-cards/{id:guid}/debt-entries", async (
                Guid id, DebtEntryRequest request, DebtEntryHandler handler, CancellationToken ct) =>
                (await handler.CreateAsync(id, request, ct)).ToCreated(e => $"/api/debt-entries/{e.Id}"))
            .WithTags("Revolving cards")
            .WithName("CreateDebtEntry")
            .WithSummary("Records a starting debt or a correction: the debt at the end of a date.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        var entries = routes.MapGroup("/debt-entries").WithTags("Revolving cards");

        entries.MapPut("/{id:guid}", async (Guid id, DebtEntryRequest request, DebtEntryHandler handler, CancellationToken ct) =>
                (await handler.UpdateAsync(id, request, ct)).ToOk())
            .WithName("UpdateDebtEntry")
            .WithSummary("Edits a starting debt or a correction; transfers cannot be changed.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        entries.MapDelete("/{id:guid}", async (Guid id, DebtEntryHandler handler, CancellationToken ct) =>
                (await handler.DeleteAsync(id, ct)).ToNoContent())
            .WithName("DeleteDebtEntry")
            .WithSummary("Deletes a starting debt or a correction; transfers cannot be deleted.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static ValidationProblem InvalidMonth(string field) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> { [field] = ["Enter the month as yyyy-MM."] });
}
