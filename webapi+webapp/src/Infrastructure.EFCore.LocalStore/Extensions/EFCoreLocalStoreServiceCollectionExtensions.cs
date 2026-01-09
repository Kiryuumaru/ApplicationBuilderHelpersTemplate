using Application.LocalStore.Interfaces;
using Infrastructure.EFCore.Interfaces;
using Infrastructure.EFCore.LocalStore.Configurations;
using Infrastructure.EFCore.LocalStore.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.EFCore.LocalStore.Extensions;

public static class EFCoreLocalStoreServiceCollectionExtensions
{
    public static IServiceCollection AddEFCoreLocalStore(this IServiceCollection services)
    {
        // Register entity configuration for modular DbContext composition
        services.AddSingleton<IEFCoreEntityConfiguration, LocalStoreEntityConfiguration>();

        services.AddScoped<ILocalStoreService, EFCoreLocalStoreService>();
        return services;
    }
}
