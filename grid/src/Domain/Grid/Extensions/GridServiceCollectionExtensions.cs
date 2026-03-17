using Microsoft.Extensions.DependencyInjection;

namespace Domain.Grid.Extensions;

internal static class GridServiceCollectionExtensions
{
    internal static IServiceCollection AddGridServices(this IServiceCollection services)
    {
        // Domain services (pure logic, no I/O) would be registered here as Singleton
        return services;
    }
}
