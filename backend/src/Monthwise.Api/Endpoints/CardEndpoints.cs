using Monthwise.Application.Cards;
using Monthwise.Domain.Cards;

namespace Monthwise.Api.Endpoints;

/// <summary>Cards and bank accounts (docs/feature-requirements/credit-card-settings.md).</summary>
internal static class CardEndpoints
{
    public static IEndpointRouteBuilder MapCardEndpoints(this IEndpointRouteBuilder routes)
    {
        var cards = routes.MapGroup("/cards").WithTags("Cards");

        cards.MapGet("/", (CardListFilter? show, GetCardsHandler handler, CancellationToken ct) =>
                handler.HandleAsync(show ?? CardListFilter.Default, ct))
            .WithName("GetCards")
            .WithSummary("Cards for the Settings list: Default hides archived cards and expired cards without ongoing transactions.");

        cards.MapGet("/dashboard", (GetDashboardCardsHandler handler, CancellationToken ct) => handler.HandleAsync(ct))
            .WithName("GetDashboardCards")
            .WithSummary("Cards shown on the Dashboard, and whether any card exists at all.");

        cards.MapGet("/{id:guid}", async (Guid id, GetCardHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(id, ct)).ToOk())
            .WithName("GetCard")
            .WithSummary("One card, with masked numbers.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        cards.MapGet("/{id:guid}/numbers/{kind}", async (
                Guid id, SensitiveNumberKind kind, HttpContext http, RevealCardNumberHandler handler, CancellationToken ct) =>
            {
                // Never cache a revealed number in the browser or a proxy.
                http.Response.Headers.CacheControl = "no-store";
                return (await handler.HandleAsync(id, kind, ct)).ToOk();
            })
            .WithName("RevealCardNumber")
            .WithSummary("Reveals one full number (CardNumber or Iban) when the user asks for it.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        cards.MapPost("/", async (CardRequest request, Guid? replaces, CreateCardHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(request, replaces, ct)).ToCreated(card => $"/api/cards/{card.Id}"))
            .WithName("CreateCard")
            .WithSummary("Adds a card. With ?replaces={id}, the new card is linked as the replacement of that card.")
            .ProducesProblem(StatusCodes.Status409Conflict);

        cards.MapPut("/{id:guid}", async (Guid id, CardRequest request, UpdateCardHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(id, request, ct)).ToOk())
            .WithName("UpdateCard")
            .WithSummary("Edits a card. A null cardNumber or iban keeps the stored value; an empty string removes it.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        cards.MapPost("/{id:guid}/block", async (Guid id, CardStateRequest request, BlockCardHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(id, request, ct)).ToOk())
            .WithName("BlockCard")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        cards.MapPost("/{id:guid}/unblock", async (Guid id, UnblockCardHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(id, ct)).ToOk())
            .WithName("UnblockCard")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        cards.MapPost("/{id:guid}/expire", async (Guid id, ExpireCardRequest request, ExpireCardHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(id, request, ct)).ToOk())
            .WithName("ExpireCard")
            .WithSummary("Marks the card as expired from today. Cannot be undone.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        cards.MapPost("/{id:guid}/archive", async (Guid id, ArchiveCardHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(id, ct)).ToOk())
            .WithName("ArchiveCard")
            .WithSummary("Archives the card instead of deleting it. Cannot be undone.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        MapList<CardIssuer>(routes, "/card-issuers", "CardIssuer", CardIssuer.Create);
        MapList<Bank>(routes, "/banks", "Bank", Bank.Create);
        return routes;
    }

    private static void MapList<TEntry>(
        IEndpointRouteBuilder routes,
        string path,
        string name,
        Func<string?, Domain.Common.Result<TEntry>> create)
        where TEntry : CardListEntry
    {
        var group = routes.MapGroup(path).WithTags("Cards");

        group.MapGet("/", (CardListHandler<TEntry> handler, CancellationToken ct) => handler.ListAsync(ct))
            .WithName($"Get{name}s");

        group.MapPost("/", async (CardListEntryRequest request, CardListHandler<TEntry> handler, CancellationToken ct) =>
                (await handler.CreateAsync(request, create, ct)).ToOk())
            .WithName($"Create{name}");
    }
}
