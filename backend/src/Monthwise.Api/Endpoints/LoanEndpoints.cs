using Microsoft.AspNetCore.Http.HttpResults;
using Monthwise.Application.CashFlow;
using Monthwise.Application.Loans;
using Monthwise.Domain.Common;
using Monthwise.Domain.Loans;

namespace Monthwise.Api.Endpoints;

/// <summary>Bank loans and leases (docs/feature-requirements/loans-and-leases.md).</summary>
internal static class LoanEndpoints
{
    public static IEndpointRouteBuilder MapLoanEndpoints(this IEndpointRouteBuilder routes)
    {
        MapLoans(routes);
        MapPaymentChanges(routes);
        MapPayments(routes);
        MapCategories(routes);
        return routes;
    }

    private static void MapLoans(IEndpointRouteBuilder routes)
    {
        var loans = routes.MapGroup("/loans").WithTags("Loans");

        loans.MapGet("/", (LoanListFilter? show, GetLoansHandler handler, CancellationToken ct) =>
                handler.HandleAsync(show ?? LoanListFilter.Active, ct))
            .WithName("GetLoans")
            .WithSummary("Active loans (active and paid off), or closed loans.");

        loans.MapGet("/dashboard", (GetLoansDashboardHandler handler, CancellationToken ct) => handler.HandleAsync(ct))
            .WithName("GetLoansDashboard")
            .WithSummary("Active loans with their next payment, and the totals of the monthly payments and the remaining amount.");

        loans.MapGet("/{id:guid}", async (Guid id, GetLoanHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(id, ct)).ToOk())
            .WithName("GetLoan")
            .WithSummary("A loan with its payment changes and all its payments.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        loans.MapPost("/", async (LoanRequest request, CreateLoanHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(request, ct)).ToOk())
            .WithName("CreateLoan")
            .WithSummary("Adds a loan. Give the number of payments or the end month (or both, when they agree).");

        loans.MapPut("/{id:guid}", async (Guid id, LoanRequest request, UpdateLoanHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(id, request, ct)).ToOk())
            .WithName("UpdateLoan")
            .WithSummary("Edits a loan. Paid payments stay as they are.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        loans.MapPost("/{id:guid}/close", async (Guid id, CloseLoanHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(id, ct)).ToOk())
            .WithName("CloseLoan")
            .WithSummary("Closes the loan today: it keeps its paid payments and has no payments after today.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        loans.MapDelete("/{id:guid}", async (Guid id, DeleteLoanHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(id, ct)).ToNoContent())
            .WithName("DeleteLoan")
            .WithSummary("Deletes a loan without paid payments; a loan with paid payments returns 409 and can only be closed.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static void MapPaymentChanges(IEndpointRouteBuilder routes)
    {
        var changes = routes.MapGroup("/loans/{id:guid}/payment-changes").WithTags("Loans");

        changes.MapPost("/", async (Guid id, PaymentChangeRequest request, PaymentChangeHandler handler, CancellationToken ct) =>
                (await handler.CreateAsync(id, request, ct)).ToOk())
            .WithName("CreatePaymentChange")
            .WithSummary("Changes the monthly payment or the payment day from a month on.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        changes.MapPut("/{changeId:guid}", async (
                Guid id, Guid changeId, PaymentChangeRequest request, PaymentChangeHandler handler, CancellationToken ct) =>
                (await handler.UpdateAsync(id, changeId, request, ct)).ToOk())
            .WithName("UpdatePaymentChange")
            .WithSummary("Edits a payment change while none of the payments it covers is paid.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        changes.MapDelete("/{changeId:guid}", async (Guid id, Guid changeId, PaymentChangeHandler handler, CancellationToken ct) =>
                (await handler.DeleteAsync(id, changeId, ct)).ToOk())
            .WithName("DeletePaymentChange")
            .WithSummary("Removes a payment change while none of the payments it covers is paid.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static void MapPayments(IEndpointRouteBuilder routes)
    {
        routes.MapGet("/loan-payments", async Task<Results<Ok<MonthLoanPaymentsResponse>, ValidationProblem>> (
                string month, GetMonthLoanPaymentsHandler handler, CancellationToken ct) =>
                YearMonth.TryParse(month, out var parsed)
                    ? TypedResults.Ok(await handler.HandleAsync(parsed, ct))
                    : InvalidMonth())
            .WithTags("Loans")
            .WithName("GetMonthLoanPayments")
            .WithSummary("Loan payments of a month (yyyy-MM), with totals.");

        var payment = routes.MapGroup("/loans/{id:guid}/payments/{month}/{kind}").WithTags("Loans");

        payment.MapPut("/", async (
                Guid id, string month, LoanPaymentKind kind, LoanPaymentChangeRequest request,
                LoanPaymentHandler handler, CancellationToken ct) =>
                YearMonth.TryParse(month, out var parsed)
                    ? (await handler.ChangeAsync(id, parsed, kind, request, ct)).ToOk()
                    : InvalidMonth())
            .WithName("ChangeLoanPayment")
            .WithSummary("Changes the amount and due date of one payment without changing the loan.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        payment.MapPost("/paid", async (
                Guid id, string month, LoanPaymentKind kind, LoanPaymentPaidRequest request,
                LoanPaymentHandler handler, CancellationToken ct) =>
                YearMonth.TryParse(month, out var parsed)
                    ? (await handler.MarkPaidAsync(id, parsed, kind, request, ct)).ToOk()
                    : InvalidMonth())
            .WithName("MarkLoanPaymentPaid")
            .WithSummary("Ticks Paid. The paid date defaults to today and the account to the loan's account.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        payment.MapDelete("/paid", async (
                Guid id, string month, LoanPaymentKind kind, LoanPaymentHandler handler, CancellationToken ct) =>
                YearMonth.TryParse(month, out var parsed)
                    ? (await handler.MarkUnpaidAsync(id, parsed, kind, ct)).ToOk()
                    : InvalidMonth())
            .WithName("MarkLoanPaymentUnpaid")
            .WithSummary("Unticks Paid.")
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static void MapCategories(IEndpointRouteBuilder routes)
    {
        var categories = routes.MapGroup("/loan-categories").WithTags("Loans");

        categories.MapGet("/", (bool? includeHidden, LoanCategoryHandler handler, CancellationToken ct) =>
                handler.ListAsync(includeHidden ?? false, ct))
            .WithName("GetLoanCategories")
            .WithSummary("Loan categories with their subcategories.");

        categories.MapPost("/", async (LoanCategoryRequest request, LoanCategoryHandler handler, CancellationToken ct) =>
                (await handler.CreateAsync(request, ct)).ToOk())
            .WithName("CreateLoanCategory");

        categories.MapPut("/{id:guid}", async (Guid id, LoanCategoryRequest request, LoanCategoryHandler handler, CancellationToken ct) =>
                (await handler.UpdateAsync(id, request, ct)).ToOk())
            .WithName("UpdateLoanCategory")
            .WithSummary("Renames the category and sets whether its loans may have a residual value.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        categories.MapPost("/{id:guid}/hide", async (Guid id, LoanCategoryHandler handler, CancellationToken ct) =>
                (await handler.HideAsync(id, ct)).ToOk())
            .WithName("HideLoanCategory")
            .ProducesProblem(StatusCodes.Status404NotFound);

        categories.MapPost("/{id:guid}/show", async (Guid id, LoanCategoryHandler handler, CancellationToken ct) =>
                (await handler.ShowAsync(id, ct)).ToOk())
            .WithName("ShowLoanCategory")
            .ProducesProblem(StatusCodes.Status404NotFound);

        categories.MapDelete("/{id:guid}", async (Guid id, LoanCategoryHandler handler, CancellationToken ct) =>
                (await handler.DeleteAsync(id, ct)).ToNoContent())
            .WithName("DeleteLoanCategory")
            .WithSummary("Deletes an unused category; a used one returns 409 and can only be hidden.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        categories.MapPost("/{id:guid}/subcategories", async (Guid id, NameRequest request, LoanCategoryHandler handler, CancellationToken ct) =>
                (await handler.CreateSubcategoryAsync(id, request, ct)).ToOk())
            .WithName("CreateLoanSubcategory")
            .ProducesProblem(StatusCodes.Status404NotFound);

        var subcategories = routes.MapGroup("/loan-subcategories").WithTags("Loans");

        subcategories.MapPut("/{id:guid}", async (Guid id, NameRequest request, LoanCategoryHandler handler, CancellationToken ct) =>
                (await handler.RenameSubcategoryAsync(id, request, ct)).ToOk())
            .WithName("RenameLoanSubcategory")
            .ProducesProblem(StatusCodes.Status404NotFound);

        subcategories.MapPost("/{id:guid}/hide", async (Guid id, LoanCategoryHandler handler, CancellationToken ct) =>
                (await handler.HideSubcategoryAsync(id, ct)).ToOk())
            .WithName("HideLoanSubcategory")
            .ProducesProblem(StatusCodes.Status404NotFound);

        subcategories.MapPost("/{id:guid}/show", async (Guid id, LoanCategoryHandler handler, CancellationToken ct) =>
                (await handler.ShowSubcategoryAsync(id, ct)).ToOk())
            .WithName("ShowLoanSubcategory")
            .ProducesProblem(StatusCodes.Status404NotFound);

        subcategories.MapDelete("/{id:guid}", async (Guid id, LoanCategoryHandler handler, CancellationToken ct) =>
                (await handler.DeleteSubcategoryAsync(id, ct)).ToNoContent())
            .WithName("DeleteLoanSubcategory")
            .WithSummary("Deletes an unused subcategory; a used one returns 409 and can only be hidden.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static ValidationProblem InvalidMonth() =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["month"] = ["Enter the month as yyyy-MM."] });
}
