using Microsoft.Extensions.DependencyInjection;

namespace Application.Shared.Extensions;

internal static class SharedServiceCollectionExtensions
{
    public static IServiceCollection AddSharedServices(this IServiceCollection services)
    {
        return services;
    }
}
