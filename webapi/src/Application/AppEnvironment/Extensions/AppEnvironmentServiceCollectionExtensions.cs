using Application.AppEnvironment.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Application.AppEnvironment.Extensions;

internal static class AppEnvironmentServiceCollectionExtensions
{
    public static IServiceCollection AddAppEnvironmentServices(this IServiceCollection services)
    {
        services.AddScoped<AppEnvironmentService>();

        return services;
    }
}
