using Microsoft.Extensions.DependencyInjection;

namespace Application.Common.Extensions;

internal static class CommonServiceCollectionExtensions
{
    public static IServiceCollection AddCommonServices(this IServiceCollection services)
    {
        return services;
    }
}
