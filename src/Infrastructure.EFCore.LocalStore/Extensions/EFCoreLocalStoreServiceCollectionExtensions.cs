using Application.LocalStore.Interfaces;
using Infrastructure.EFCore.LocalStore.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.EFCore.LocalStore.Extensions;

internal static class EFCoreLocalStoreServiceCollectionExtensions
{
    public static IServiceCollection AddEFCoreLocalStore(this IServiceCollection services)
    {
        services.AddScoped<ILocalStoreService, EFCoreLocalStoreService>();
        return services;
    }
}
