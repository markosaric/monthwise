using Microsoft.AspNetCore.Http.HttpResults;
using Monthwise.Application.Lendings;

namespace Monthwise.Api.Endpoints;

/// <summary>Money lent to borrowers and its repayments (docs/feature-requirements/money-lending.md).</summary>
internal static class LendingEndpoints
{
    public static IEndpointRouteBuilder MapLendingEndpoints(this IEndpointRouteBuilder routes)
    {
        var lendings = routes.MapGroup("/lendings").WithTags("Lending");

        lendings.MapGet("/", (LendingFilter? state, GetLendingsHandler handler, CancellationToken ct) =>
                handler.HandleAsync(state ?? LendingFilter.Open, ct))
            .WithName("GetLendings")
            .WithSummary("Open and overdue lendings by expected return date, or returned ones latest first, with the outstanding and overdue totals.");

        lendings.MapGet("/borrowers", (GetLendingsHandler handler, CancellationToken ct) => handler.BorrowersAsync(ct))
            .WithName("GetLendingBorrowers")
            .WithSummary("Per borrower: lent, returned and outstanding.");

        lendings.MapGet("/month", async Task<Results<Ok<MonthLendingResponse>, ValidationProblem>> (
                string month, GetMonthLendingHandler handler, CancellationToken ct) =>
                MonthParameter.TryParse(month, out var parsed)
                    ? TypedResults.Ok(await handler.HandleAsync(parsed, ct))
                    : MonthParameter.Invalid("month", month))
            .WithName("GetMonthLending")
            .WithSummary("The money lent, returned and expected back in a month (yyyy-MM), with totals.");

        lendings.MapGet("/{id:guid}", async (Guid id, LendingHandler handler, CancellationToken ct) =>
                (await handler.GetAsync(id, ct)).ToOk())
            .WithName("GetLending")
            .ProducesProblem(StatusCodes.Status404NotFound);

        lendings.MapPost("/", async (LendingRequest request, LendingHandler handler, CancellationToken ct) =>
                (await handler.CreateAsync(request, ct)).ToCreated(l => $"/api/lendings/{l.Id}"))
            .WithName("CreateLending")
            .WithSummary("Records money lent from an account; the borrower is found or added in the payers list.");

        lendings.MapPut("/{id:guid}", async (Guid id, LendingRequest request, LendingHandler handler, CancellationToken ct) =>
                (await handler.UpdateAsync(id, request, ct)).ToOk())
            .WithName("UpdateLending")
            .ProducesProblem(StatusCodes.Status404NotFound);

        lendings.MapDelete("/{id:guid}", async (Guid id, LendingHandler handler, CancellationToken ct) =>
                (await handler.DeleteAsync(id, ct)).ToNoContent())
            .WithName("DeleteLending")
            .WithSummary("Deletes the lending with its repayments.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        lendings.MapPost("/{id:guid}/repayments", async (Guid id, RepaymentRequest request, LendingHandler handler, CancellationToken ct) =>
                (await handler.AddRepaymentAsync(id, request, ct)).ToCreated(
                    r => $"/api/lendings/{id}/repayments/{r.RepaymentId}", r => r.Lending))
            .WithName("AddLendingRepayment")
            .WithSummary("Records a repayment of at most the outstanding amount; returns the lending.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        lendings.MapPut("/{id:guid}/repayments/{repaymentId:guid}", async (
                Guid id, Guid repaymentId, RepaymentRequest request, LendingHandler handler, CancellationToken ct) =>
                (await handler.UpdateRepaymentAsync(id, repaymentId, request, ct)).ToOk())
            .WithName("UpdateLendingRepayment")
            .ProducesProblem(StatusCodes.Status404NotFound);

        lendings.MapDelete("/{id:guid}/repayments/{repaymentId:guid}", async (
                Guid id, Guid repaymentId, LendingHandler handler, CancellationToken ct) =>
                (await handler.DeleteRepaymentAsync(id, repaymentId, ct)).ToOk())
            .WithName("DeleteLendingRepayment")
            .WithSummary("Deletes a repayment; returns the lending.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        return routes;
    }
}
