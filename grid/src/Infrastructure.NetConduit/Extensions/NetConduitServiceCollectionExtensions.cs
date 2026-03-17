using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.NetConduit.Extensions;

internal static class NetConduitServiceCollectionExtensions
{
    internal static IServiceCollection AddNetConduitServices(this IServiceCollection services)
    {
        // Services are registered via Grid-specific extension methods
        return services;
    }
}
