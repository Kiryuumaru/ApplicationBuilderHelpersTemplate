using Application.LocalStore.Interfaces;
using Infrastructure.EFCore.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.EFCore.Extensions;

public static class EFCoreInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddEFCoreInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<EFCoreDatabaseInitializationState>();
        services.AddSingleton<IDatabaseInitializationState>(sp => sp.GetRequiredService<EFCoreDatabaseInitializationState>());
        
        return services;
    }
}
