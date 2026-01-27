using Application.Shared.Interfaces;
using Application.Shared.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Shared.Extensions;

internal static class SharedServiceCollectionExtensions
{
    internal static IServiceCollection AddSharedServices(this IServiceCollection services)
    {
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        return services;
    }
}
