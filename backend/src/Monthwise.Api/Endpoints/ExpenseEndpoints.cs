using Microsoft.AspNetCore.Http.HttpResults;
using Monthwise.Application.CashFlow;
using Monthwise.Application.Expenses;
using Monthwise.Domain.Common;

namespace Monthwise.Api.Endpoints;

/// <summary>
/// Expense categories, recurring bills, bills, expenses and cash withdrawals
/// (docs/feature-requirements/monthly-bills-and-expenses.md).
/// </summary>
internal static class ExpenseEndpoints
{
    public static IEndpointRouteBuilder MapExpenseEndpoints(this IEndpointRouteBuilder routes)
    {
        MapCategories(routes);
        MapRecurringBills(routes);
        MapBills(routes);
        MapExpenses(routes);
        return routes;
    }

    private static void MapCategories(IEndpointRouteBuilder routes)
    {
        var categories = routes.MapGroup("/expense-categories").WithTags("Expense categories");

        categories.MapGet("/", (bool? includeHidden, ExpenseCategoryHandler handler, CancellationToken ct) =>
                handler.ListAsync(includeHidden ?? false, ct))
            .WithName("GetExpenseCategories")
            .WithSummary("The shared categories of card purchases, bills and expenses.");

        categories.MapPost("/", async (NameRequest request, ExpenseCategoryHandler handler, CancellationToken ct) =>
                (await handler.CreateAsync(request, ct)).ToOk())
            .WithName("CreateExpenseCategory");

        categories.MapPut("/{id:guid}", async (Guid id, NameRequest request, ExpenseCategoryHandler handler, CancellationToken ct) =>
                (await handler.RenameAsync(id, request, ct)).ToOk())
            .WithName("RenameExpenseCategory")
            .WithSummary("Renames a category; Cash withdrawal returns 409.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        categories.MapPost("/{id:guid}/hide", async (Guid id, ExpenseCategoryHandler handler, CancellationToken ct) =>
                (await handler.HideAsync(id, ct)).ToOk())
            .WithName("HideExpenseCategory")
            .WithSummary("Hides a category from selection lists; Cash withdrawal returns 409.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        categories.MapPost("/{id:guid}/show", async (Guid id, ExpenseCategoryHandler handler, CancellationToken ct) =>
                (await handler.ShowAsync(id, ct)).ToOk())
            .WithName("ShowExpenseCategory")
            .ProducesProblem(StatusCodes.Status404NotFound);

        categories.MapDelete("/{id:guid}", async (Guid id, ExpenseCategoryHandler handler, CancellationToken ct) =>
                (await handler.DeleteAsync(id, ct)).ToNoContent())
            .WithName("DeleteExpenseCategory")
            .WithSummary("Deletes an unused category; a used one, or Cash withdrawal, returns 409.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static void MapRecurringBills(IEndpointRouteBuilder routes)
    {
        var recurring = routes.MapGroup("/recurring-bills").WithTags("Bills");

        recurring.MapGet("/", (bool? includeStopped, RecurringBillHandler handler, CancellationToken ct) =>
                handler.ListAsync(includeStopped ?? false, ct))
            .WithName("GetRecurringBills")
            .WithSummary("Recurring bills: active ones by name, then stopped ones when asked.");

        recurring.MapGet("/{id:guid}", async (Guid id, RecurringBillHandler handler, CancellationToken ct) =>
                (await handler.GetAsync(id, ct)).ToOk())
            .WithName("GetRecurringBill")
            .ProducesProblem(StatusCodes.Status404NotFound);

        recurring.MapPost("/", async (RecurringBillRequest request, RecurringBillHandler handler, CancellationToken ct) =>
                (await handler.CreateAsync(request, ct)).ToOk())
            .WithName("CreateRecurringBill");

        recurring.MapPut("/{id:guid}", async (Guid id, RecurringBillRequest request, RecurringBillHandler handler, CancellationToken ct) =>
                (await handler.UpdateAsync(id, request, ct)).ToOk())
            .WithName("UpdateRecurringBill")
            .WithSummary("Edits a recurring bill; bills already added from it stay unchanged.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        recurring.MapPost("/{id:guid}/stop", async (Guid id, RecurringBillHandler handler, CancellationToken ct) =>
                (await handler.StopAsync(id, ct)).ToOk())
            .WithName("StopRecurringBill")
            .WithSummary("Stops adding the recurring bill to new months.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        recurring.MapPost("/{id:guid}/restart", async (Guid id, RecurringBillHandler handler, CancellationToken ct) =>
                (await handler.RestartAsync(id, ct)).ToOk())
            .WithName("RestartRecurringBill")
            .ProducesProblem(StatusCodes.Status404NotFound);

        recurring.MapDelete("/{id:guid}", async (Guid id, RecurringBillHandler handler, CancellationToken ct) =>
                (await handler.DeleteAsync(id, ct)).ToNoContent())
            .WithName("DeleteRecurringBill")
            .WithSummary("Deletes a recurring bill that was never added to a month; otherwise 409.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static void MapBills(IEndpointRouteBuilder routes)
    {
        var bills = routes.MapGroup("/bills").WithTags("Bills");

        bills.MapGet("/", async Task<Results<Ok<MonthBillsResponse>, ValidationProblem>> (
                string month, GetMonthBillsHandler handler, CancellationToken ct) =>
                YearMonth.TryParse(month, out var parsed)
                    ? TypedResults.Ok(await handler.HandleAsync(parsed, ct))
                    : InvalidMonth())
            .WithName("GetMonthBills")
            .WithSummary("The bills of a month (yyyy-MM) with totals.");

        bills.MapPost("/add-recurring", async Task<Results<Ok<AddRecurringBillsResponse>, ValidationProblem>> (
                string month, AddRecurringBillsHandler handler, CancellationToken ct) =>
                YearMonth.TryParse(month, out var parsed)
                    ? TypedResults.Ok(await handler.HandleAsync(parsed, ct))
                    : InvalidMonth())
            .WithName("AddRecurringBills")
            .WithSummary("Adds a Not issued bill for every active recurring bill that has none in the month (yyyy-MM) yet.");

        bills.MapGet("/{id:guid}", async (Guid id, BillHandler handler, CancellationToken ct) =>
                (await handler.GetAsync(id, ct)).ToOk())
            .WithName("GetBill")
            .ProducesProblem(StatusCodes.Status404NotFound);

        bills.MapPost("/", async (string month, BillRequest request, BillHandler handler, CancellationToken ct) =>
                YearMonth.TryParse(month, out var parsed)
                    ? (await handler.CreateAsync(parsed, request, ct)).ToOk()
                    : InvalidMonth())
            .WithName("CreateBill")
            .WithSummary("Adds a one-off bill to a month (yyyy-MM), expected or paid.");

        bills.MapPut("/{id:guid}", async (Guid id, BillRequest request, BillHandler handler, CancellationToken ct) =>
                (await handler.UpdateAsync(id, request, ct)).ToOk())
            .WithName("UpdateBill")
            .WithSummary("Edits a bill: issues it, changes it or sets its paid state.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        bills.MapPost("/{id:guid}/paid", async (Guid id, BillPaidRequest request, BillHandler handler, CancellationToken ct) =>
                (await handler.MarkPaidAsync(id, request, ct)).ToOk())
            .WithName("MarkBillPaid")
            .WithSummary("Ticks Paid. The paid date defaults to today and what it was paid with to the bill's own.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        bills.MapDelete("/{id:guid}/paid", async (Guid id, BillHandler handler, CancellationToken ct) =>
                (await handler.MarkUnpaidAsync(id, ct)).ToOk())
            .WithName("MarkBillUnpaid")
            .ProducesProblem(StatusCodes.Status404NotFound);

        bills.MapDelete("/{id:guid}", async (Guid id, BillHandler handler, CancellationToken ct) =>
                (await handler.DeleteAsync(id, ct)).ToNoContent())
            .WithName("DeleteBill")
            .WithSummary("Deletes a bill from its month; its recurring bill stays.")
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static void MapExpenses(IEndpointRouteBuilder routes)
    {
        var expenses = routes.MapGroup("/expenses").WithTags("Expenses");

        expenses.MapGet("/", async Task<Results<Ok<MonthExpensesResponse>, ValidationProblem>> (
                string month, GetMonthExpensesHandler handler, CancellationToken ct) =>
                YearMonth.TryParse(month, out var parsed)
                    ? TypedResults.Ok(await handler.HandleAsync(parsed, ct))
                    : InvalidMonth())
            .WithName("GetMonthExpenses")
            .WithSummary("The expenses and cash withdrawals of a month (yyyy-MM), newest first, with totals.");

        expenses.MapGet("/last-paid-from", (ExpenseHandler handler, CancellationToken ct) => handler.LastPaidFromAsync(ct))
            .WithName("GetLastExpensePaidFrom")
            .WithSummary("What the last expense was paid with, to prefill a new one.");

        expenses.MapGet("/{id:guid}", async (Guid id, ExpenseHandler handler, CancellationToken ct) =>
                (await handler.GetAsync(id, ct)).ToOk())
            .WithName("GetExpense")
            .ProducesProblem(StatusCodes.Status404NotFound);

        expenses.MapPost("/", async (ExpenseRequest request, ExpenseHandler handler, CancellationToken ct) =>
                (await handler.CreateAsync(request, ct)).ToOk())
            .WithName("CreateExpense")
            .WithSummary("Records an expense, paid from an account or with a credit card.");

        expenses.MapPut("/{id:guid}", async (Guid id, ExpenseRequest request, ExpenseHandler handler, CancellationToken ct) =>
                (await handler.UpdateAsync(id, request, ct)).ToOk())
            .WithName("UpdateExpense")
            .ProducesProblem(StatusCodes.Status404NotFound);

        expenses.MapPost("/cash-withdrawals", async (CashWithdrawalRequest request, ExpenseHandler handler, CancellationToken ct) =>
                (await handler.CreateCashWithdrawalAsync(request, ct)).ToOk())
            .WithName("CreateCashWithdrawal")
            .WithSummary("Records a cash withdrawal from a primary account.");

        expenses.MapPut("/cash-withdrawals/{id:guid}", async (
                Guid id, CashWithdrawalRequest request, ExpenseHandler handler, CancellationToken ct) =>
                (await handler.UpdateCashWithdrawalAsync(id, request, ct)).ToOk())
            .WithName("UpdateCashWithdrawal")
            .ProducesProblem(StatusCodes.Status404NotFound);

        expenses.MapDelete("/{id:guid}", async (Guid id, ExpenseHandler handler, CancellationToken ct) =>
                (await handler.DeleteAsync(id, ct)).ToNoContent())
            .WithName("DeleteExpense")
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static ValidationProblem InvalidMonth() =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["month"] = ["Enter the month as yyyy-MM."] });
}
