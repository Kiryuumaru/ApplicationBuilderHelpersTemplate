using Application.Shared.Interfaces.Outbound;
using Application.Shared.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Shared.Extensions;

public static class SharedServiceCollectionExtensions
{
    internal static IServiceCollection AddSharedServices(this IServiceCollection services)
    {
        return services;
    }

    /// <summary>
    /// Adds the mock email service for development/testing.
    /// </summary>
    public static IServiceCollection AddMockEmailService(this IServiceCollection services)
    {
        services.AddSingleton<IEmailService, MockEmailService>();
        return services;
    }
}
