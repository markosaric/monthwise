using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Monthwise.Application;

public static class DependencyInjection
{
    /// <summary>Registers use case handlers and validators.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);
        return services;
    }
}
