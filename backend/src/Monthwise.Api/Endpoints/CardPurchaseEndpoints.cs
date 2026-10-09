using Microsoft.AspNetCore.Http.HttpResults;
using Monthwise.Application.CardPurchases;
using Monthwise.Domain.Common;

namespace Monthwise.Api.Endpoints;

/// <summary>Card purchases, installments and card payments (docs/feature-requirements/credit-card-installments.md).</summary>
internal static class CardPurchaseEndpoints
{
    public static IEndpointRouteBuilder MapCardPurchaseEndpoints(this IEndpointRouteBuilder routes)
    {
        MapCards(routes);
        MapPurchases(routes);
        MapCardPayments(routes);
        return routes;
    }

    private static void MapCards(IEndpointRouteBuilder routes)
    {
        routes.MapGet("/card-purchases/cards", (GetInstallmentCardsHandler handler, CancellationToken ct) => handler.HandleAsync(ct))
            .WithTags("Card purchases")
            .WithName("GetInstallmentCards")
            .WithSummary("Cards that allow monthly installments, with their billing details, next card payment and remaining installments.");

        var card = routes.MapGroup("/cards/{id:guid}").WithTags("Card purchases");

        card.MapGet("/installments", async (Guid id, GetInstallmentCardHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(id, ct)).ToOk())
            .WithName("GetInstallmentCard")
            .WithSummary("One card of the Cards screen: billing details, next card payment and remaining installments.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        card.MapGet("/purchases", async (Guid id, GetInstallmentCardHandler handler, CancellationToken ct) =>
                (await handler.PurchasesAsync(id, ct)).ToOk())
            .WithName("GetCardPurchases")
            .WithSummary("The purchases of a card, newest first.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        card.MapGet("/card-payments", async (Guid id, GetInstallmentCardHandler handler, CancellationToken ct) =>
                (await handler.PaymentsAsync(id, ct)).ToOk())
            .WithName("GetCardPayments")
            .WithSummary("The card payments of a card by month, newest first.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        card.MapGet("/card-payments/{month}", async (Guid id, string month, GetInstallmentCardHandler handler, CancellationToken ct) =>
                YearMonth.TryParse(month, out var parsed)
                    ? (await handler.PaymentDetailsAsync(id, parsed, ct)).ToOk()
                    : InvalidMonth())
            .WithName("GetCardPayment")
            .WithSummary("One card payment (yyyy-MM) with every purchase, installment and credit in it.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        card.MapPost("/card-payments/{month}/paid", async (
                Guid id, string month, CardPaymentPaidRequest request, CardPaymentHandler handler, CancellationToken ct) =>
                YearMonth.TryParse(month, out var parsed)
                    ? (await handler.MarkPaidAsync(id, parsed, request, ct)).ToOk()
                    : InvalidMonth())
            .WithName("MarkCardPaymentPaid")
            .WithSummary("Ticks Paid. The paid date defaults to today and the account to the card's paying account.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        card.MapDelete("/card-payments/{month}/paid", async (Guid id, string month, CardPaymentHandler handler, CancellationToken ct) =>
                YearMonth.TryParse(month, out var parsed)
                    ? (await handler.MarkUnpaidAsync(id, parsed, ct)).ToOk()
                    : InvalidMonth())
            .WithName("MarkCardPaymentUnpaid")
            .WithSummary("Unticks Paid, also for a card payment paid automatically.")
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static void MapPurchases(IEndpointRouteBuilder routes)
    {
        var purchases = routes.MapGroup("/card-purchases").WithTags("Card purchases");

        purchases.MapGet("/{id:guid}", async (Guid id, GetCardPurchaseHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(id, ct)).ToOk())
            .WithName("GetCardPurchase")
            .WithSummary("A purchase with its installments and the card payments they are in.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        purchases.MapPost("/", async (CardPurchaseRequest request, CreateCardPurchaseHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(request, ct)).ToOk())
            .WithName("CreateCardPurchase")
            .WithSummary("Records a purchase, paid in full or in monthly installments.");

        purchases.MapPut("/{id:guid}", async (Guid id, CardPurchaseRequest request, UpdateCardPurchaseHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(id, request, ct)).ToOk())
            .WithName("UpdateCardPurchase")
            .WithSummary("Edits a purchase that is not reversed; its installments are recalculated.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        purchases.MapDelete("/{id:guid}", async (Guid id, DeleteCardPurchaseHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(id, ct)).ToNoContent())
            .WithName("DeleteCardPurchase")
            .WithSummary("Deletes a purchase with all its installments.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        purchases.MapPost("/{id:guid}/reverse", async (Guid id, ReversalRequest request, ReverseCardPurchaseHandler handler, CancellationToken ct) =>
                (await handler.ReversePurchaseAsync(id, request, ct)).ToOk())
            .WithName("ReverseCardPurchase")
            .WithSummary("Reverses the whole purchase today: charged installments are credited, the others removed.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        purchases.MapPost("/{id:guid}/installments/{number:int}/reverse", async (
                Guid id, int number, ReversalRequest request, ReverseCardPurchaseHandler handler, CancellationToken ct) =>
                (await handler.ReverseInstallmentAsync(id, number, request, ct)).ToOk())
            .WithName("ReverseCardInstallment")
            .WithSummary("Reverses one installment today: credited when already charged, removed otherwise.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static void MapCardPayments(IEndpointRouteBuilder routes)
    {
        routes.MapGet("/card-payments", async Task<Results<Ok<MonthCardPaymentsResponse>, ValidationProblem>> (
                string month, GetMonthCardPaymentsHandler handler, CancellationToken ct) =>
                YearMonth.TryParse(month, out var parsed)
                    ? TypedResults.Ok(await handler.HandleAsync(parsed, ct))
                    : InvalidMonth())
            .WithTags("Card purchases")
            .WithName("GetMonthCardPayments")
            .WithSummary("Card payments of a month (yyyy-MM), one per card, with totals.");
    }

    private static ValidationProblem InvalidMonth() =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["month"] = ["Enter the month as yyyy-MM."] });
}
