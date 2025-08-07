using Application.LocalStore.Interfaces;
using Application.LocalStore.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Application.LocalStore.Extensions;

internal static class LocalStoreServiceCollectionExtensions
{
    public static IServiceCollection AddLocalStoreServices(this IServiceCollection services)
    {
        services.AddScoped<LocalStoreFactory>();
        services.AddTransient<ILocalStoreService, SqliteLocalStoreService>();
        return services;
    }
}
