using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Monthwise.Application.Cards;

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
        return services;
    }
}
