using Microsoft.AspNetCore.Http.HttpResults;
using Monthwise.Application.CashFlow;
using Monthwise.Domain.Common;

namespace Monthwise.Api.Endpoints;

/// <summary>Input cash flow (docs/feature-requirements/input-cash-flow.md).</summary>
internal static class CashFlowEndpoints
{
    public static IEndpointRouteBuilder MapCashFlowEndpoints(this IEndpointRouteBuilder routes)
    {
        MapInputs(routes);
        MapRecurringInputs(routes);
        MapSettings(routes);
        MapBalances(routes);
        MapReports(routes);
        return routes;
    }

    private static void MapInputs(IEndpointRouteBuilder routes)
    {
        var inputs = routes.MapGroup("/inputs").WithTags("Inputs");

        inputs.MapGet("/", async Task<Results<Ok<MonthInputsResponse>, ValidationProblem>> (
                string month, GetMonthInputsHandler handler, CancellationToken ct) =>
                YearMonth.TryParse(month, out var parsed)
                    ? TypedResults.Ok(await handler.HandleAsync(parsed, ct))
                    : InvalidMonth())
            .WithName("GetMonthInputs")
            .WithSummary("Inputs of a month (yyyy-MM): stored inputs and calculated occurrences of recurring inputs, with totals.");

        inputs.MapGet("/{id:guid}", async (Guid id, GetInputHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(id, ct)).ToOk())
            .WithName("GetInput")
            .ProducesProblem(StatusCodes.Status404NotFound);

        inputs.MapPost("/", async (InputRequest request, CreateInputHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(request, ct)).ToOk())
            .WithName("CreateInput")
            .WithSummary("Adds an input. With a recurrence, adds a recurring input whose first occurrence has these dates.");

        inputs.MapPut("/{id:guid}", async (Guid id, InputRequest request, UpdateInputHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(id, request, ct)).ToOk())
            .WithName("UpdateInput")
            .WithSummary("Edits a stored input. The recurrence field is ignored.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        inputs.MapDelete("/{id:guid}", async (Guid id, DeleteInputHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(id, ct)).ToNoContent())
            .WithName("DeleteInput")
            .ProducesProblem(StatusCodes.Status404NotFound);

        inputs.MapPost("/{id:guid}/received", async (Guid id, SetInputReceivedHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(id, received: true, ct)).ToOk())
            .WithName("MarkInputReceived")
            .WithSummary("Ticks Received: sets the received date to today.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        inputs.MapDelete("/{id:guid}/received", async (Guid id, SetInputReceivedHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(id, received: false, ct)).ToOk())
            .WithName("MarkInputExpected")
            .WithSummary("Unticks Received: clears the received date.")
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static void MapRecurringInputs(IEndpointRouteBuilder routes)
    {
        var recurring = routes.MapGroup("/recurring-inputs").WithTags("Inputs");

        recurring.MapGet("/{id:guid}", async (Guid id, GetRecurringInputHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(id, ct)).ToOk())
            .WithName("GetRecurringInput")
            .ProducesProblem(StatusCodes.Status404NotFound);

        recurring.MapPut("/{id:guid}", async (Guid id, RecurringInputRequest request, UpdateRecurringInputHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(id, request, ct)).ToOk())
            .WithName("UpdateRecurringInput")
            .WithSummary("Changes the template; occurrences not yet received from today on follow it.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        recurring.MapPost("/{id:guid}/stop", async (Guid id, StopRecurringInputHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(id, ct)).ToOk())
            .WithName("StopRecurringInput")
            .WithSummary("Stops it: no occurrences after today.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        var occurrences = recurring.MapGroup("/{id:guid}/occurrences/{date}");

        occurrences.MapPut("/", async (Guid id, DateOnly date, InputRequest request, UpdateOccurrenceHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(id, date, request, ct)).ToOk())
            .WithName("UpdateOccurrence")
            .WithSummary("Changes one occurrence (by its scheduled expected date) without changing the others.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        occurrences.MapPost("/received", async (Guid id, DateOnly date, ReceiveOccurrenceHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(id, date, ct)).ToOk())
            .WithName("MarkOccurrenceReceived")
            .ProducesProblem(StatusCodes.Status404NotFound);

        occurrences.MapDelete("/", async (Guid id, DateOnly date, DeleteOccurrenceHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(id, date, ct)).ToNoContent())
            .WithName("DeleteOccurrence")
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static void MapSettings(IEndpointRouteBuilder routes)
    {
        var payers = routes.MapGroup("/payers").WithTags("Inputs");

        payers.MapGet("/", (string? search, PayerHandler handler, CancellationToken ct) => handler.ListAsync(search, ct))
            .WithName("GetPayers")
            .WithSummary("Saved payers, optionally only those whose name contains the search text.");

        payers.MapPut("/{id:guid}", async (Guid id, NameRequest request, PayerHandler handler, CancellationToken ct) =>
                (await handler.RenameAsync(id, request, ct)).ToOk())
            .WithName("RenamePayer")
            .ProducesProblem(StatusCodes.Status404NotFound);

        payers.MapDelete("/{id:guid}", async (Guid id, PayerHandler handler, CancellationToken ct) =>
                (await handler.RemoveAsync(id, ct)).ToNoContent())
            .WithName("RemovePayer")
            .WithSummary("Removes the payer from the list; inputs keep its name.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        var categories = routes.MapGroup("/input-categories").WithTags("Inputs");

        categories.MapGet("/", (bool? includeHidden, InputCategoryHandler handler, CancellationToken ct) =>
                handler.ListAsync(includeHidden ?? false, ct))
            .WithName("GetInputCategories");

        categories.MapPost("/", async (NameRequest request, InputCategoryHandler handler, CancellationToken ct) =>
                (await handler.CreateAsync(request, ct)).ToOk())
            .WithName("CreateInputCategory");

        categories.MapPut("/{id:guid}", async (Guid id, NameRequest request, InputCategoryHandler handler, CancellationToken ct) =>
                (await handler.RenameAsync(id, request, ct)).ToOk())
            .WithName("RenameInputCategory")
            .ProducesProblem(StatusCodes.Status404NotFound);

        categories.MapPost("/{id:guid}/hide", async (Guid id, InputCategoryHandler handler, CancellationToken ct) =>
                (await handler.HideAsync(id, ct)).ToOk())
            .WithName("HideInputCategory")
            .ProducesProblem(StatusCodes.Status404NotFound);

        categories.MapPost("/{id:guid}/show", async (Guid id, InputCategoryHandler handler, CancellationToken ct) =>
                (await handler.ShowAsync(id, ct)).ToOk())
            .WithName("ShowInputCategory")
            .ProducesProblem(StatusCodes.Status404NotFound);

        categories.MapDelete("/{id:guid}", async (Guid id, InputCategoryHandler handler, CancellationToken ct) =>
                (await handler.DeleteAsync(id, ct)).ToNoContent())
            .WithName("DeleteInputCategory")
            .WithSummary("Deletes an unused category; a used one returns 409 and can only be hidden.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static void MapBalances(IEndpointRouteBuilder routes)
    {
        routes.MapGet("/cards/{id:guid}/balance", async (Guid id, BalanceEntryHandler handler, CancellationToken ct) =>
                (await handler.GetAsync(id, ct)).ToOk())
            .WithTags("Balances")
            .WithName("GetAccountBalance")
            .WithSummary("Current and expected balance of an account, with its starting balance and corrections.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        routes.MapPost("/cards/{id:guid}/balance-entries", async (Guid id, BalanceEntryRequest request, BalanceEntryHandler handler, CancellationToken ct) =>
                (await handler.CreateAsync(id, request, ct)).ToOk())
            .WithTags("Balances")
            .WithName("CreateBalanceEntry")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        var entries = routes.MapGroup("/balance-entries").WithTags("Balances");

        entries.MapPut("/{id:guid}", async (Guid id, BalanceEntryRequest request, BalanceEntryHandler handler, CancellationToken ct) =>
                (await handler.UpdateAsync(id, request, ct)).ToOk())
            .WithName("UpdateBalanceEntry")
            .WithSummary("Changes date, balance and note; the kind stays.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        entries.MapDelete("/{id:guid}", async (Guid id, BalanceEntryHandler handler, CancellationToken ct) =>
                (await handler.DeleteAsync(id, ct)).ToNoContent())
            .WithName("DeleteBalanceEntry")
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static void MapReports(IEndpointRouteBuilder routes)
    {
        var cashFlow = routes.MapGroup("/cash-flow").WithTags("Cash flow");

        cashFlow.MapGet("/chart", async (
                DateOnly from, DateOnly to, ChartGranularity? granularity, Guid? accountId, Guid? payerId, Guid? categoryId, Guid? loanCategoryId,
                Guid? cardId, Guid? expenseCategoryId, GetCashFlowChartHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(
                    from,
                    to,
                    granularity ?? ChartGranularity.Day,
                    new ChartFilter(accountId, payerId, categoryId, loanCategoryId, cardId, expenseCategoryId),
                    ct)).ToOk())
            .WithName("GetCashFlowChart")
            .WithSummary(
                "Received and expected inputs, and paid and expected loan and card payments, bills and expenses, per day or month between two dates.");

        cashFlow.MapGet("/report", async Task<Results<Ok<MonthlyReportResponse>, ValidationProblem>> (
                string month, GetMonthlyReportHandler handler, CancellationToken ct) =>
                YearMonth.TryParse(month, out var parsed)
                    ? TypedResults.Ok(await handler.HandleAsync(parsed, ct))
                    : InvalidMonth())
            .WithName("GetMonthlyReport")
            .WithSummary("Monthly cash flow report of a month (yyyy-MM).");
    }

    private static ValidationProblem InvalidMonth() =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["month"] = ["Enter the month as yyyy-MM."] });
}
