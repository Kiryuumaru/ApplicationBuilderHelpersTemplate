using Application.Abstractions.Storage;
using Application.LocalStore.Services;
using Infrastructure.Storage.Features;
using Microsoft.Extensions.DependencyInjection;

namespace Application.LocalStore.Extensions;

internal static class LocalStoreServiceCollectionExtensions
{
    public static IServiceCollection AddLocalStoreServices(this IServiceCollection services)
    {
        services.AddScoped<ConcurrentLocalStore>();
        services.AddScoped<LocalStoreFactory>();
        return services;
    }
}
