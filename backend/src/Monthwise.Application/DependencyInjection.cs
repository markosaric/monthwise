using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Monthwise.Application.Cards;
using Monthwise.Application.CashFlow;

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
        return services;
    }
}
