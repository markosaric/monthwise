using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Monthwise.Application.CardPurchases;
using Monthwise.Application.Cards;
using Monthwise.Application.CashFlow;
using Monthwise.Application.Expenses;
using Monthwise.Application.Loans;

namespace Monthwise.Application;

public static class DependencyInjection
{
    /// <summary>Registers use case handlers and validators.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);
        services.AddSingleton(TimeProvider.System);

        services.AddScoped<CardInputChecker>();
        services.AddScoped<CardStateChanger>();
        services.AddScoped<GetCardsHandler>();
        services.AddScoped<GetCardHandler>();
        services.AddScoped<GetDashboardCardsHandler>();
        services.AddScoped<RevealCardNumberHandler>();
        services.AddScoped<CreateCardHandler>();
        services.AddScoped<UpdateCardHandler>();
        services.AddScoped<BlockCardHandler>();
        services.AddScoped<UnblockCardHandler>();
        services.AddScoped<ExpireCardHandler>();
        services.AddScoped<ArchiveCardHandler>();
        services.AddScoped(typeof(CardListHandler<>));

        services.AddScoped<InputLedger>();
        services.AddScoped<InputDetailsResolver>();
        services.AddScoped<InputResponder>();
        services.AddScoped<OccurrenceFinder>();
        services.AddScoped<AccountBalanceQuery>();
        services.AddScoped<GetMonthInputsHandler>();
        services.AddScoped<GetInputHandler>();
        services.AddScoped<CreateInputHandler>();
        services.AddScoped<UpdateInputHandler>();
        services.AddScoped<DeleteInputHandler>();
        services.AddScoped<SetInputReceivedHandler>();
        services.AddScoped<UpdateOccurrenceHandler>();
        services.AddScoped<ReceiveOccurrenceHandler>();
        services.AddScoped<DeleteOccurrenceHandler>();
        services.AddScoped<GetRecurringInputHandler>();
        services.AddScoped<UpdateRecurringInputHandler>();
        services.AddScoped<StopRecurringInputHandler>();
        services.AddScoped<PayerHandler>();
        services.AddScoped<InputCategoryHandler>();
        services.AddScoped<BalanceEntryHandler>();
        services.AddScoped<GetCashFlowChartHandler>();
        services.AddScoped<GetMonthlyReportHandler>();

        services.AddScoped<LoanLedger>();
        services.AddScoped<LoanInputChecker>();
        services.AddScoped<GetLoansHandler>();
        services.AddScoped<GetLoanHandler>();
        services.AddScoped<GetMonthLoanPaymentsHandler>();
        services.AddScoped<GetLoansDashboardHandler>();
        services.AddScoped<CreateLoanHandler>();
        services.AddScoped<UpdateLoanHandler>();
        services.AddScoped<CloseLoanHandler>();
        services.AddScoped<DeleteLoanHandler>();
        services.AddScoped<PaymentChangeHandler>();
        services.AddScoped<LoanPaymentHandler>();
        services.AddScoped<LoanCategoryHandler>();

        services.AddScoped<CardPaymentLedger>();
        services.AddScoped<CardPurchaseInputChecker>();
        services.AddScoped<GetInstallmentCardsHandler>();
        services.AddScoped<GetInstallmentCardHandler>();
        services.AddScoped<GetMonthCardPaymentsHandler>();
        services.AddScoped<GetCardPurchaseHandler>();
        services.AddScoped<CreateCardPurchaseHandler>();
        services.AddScoped<UpdateCardPurchaseHandler>();
        services.AddScoped<DeleteCardPurchaseHandler>();
        services.AddScoped<ReverseCardPurchaseHandler>();
        services.AddScoped<CardPaymentHandler>();

        services.AddScoped<ExpenseCategoryHandler>();
        services.AddScoped<ExpenseLedger>();
        services.AddScoped<ExpenseInputChecker>();
        services.AddScoped<RecurringBillHandler>();
        services.AddScoped<GetMonthBillsHandler>();
        services.AddScoped<AddRecurringBillsHandler>();
        services.AddScoped<BillHandler>();
        services.AddScoped<GetMonthExpensesHandler>();
        services.AddScoped<ExpenseHandler>();
        return services;
    }
}
