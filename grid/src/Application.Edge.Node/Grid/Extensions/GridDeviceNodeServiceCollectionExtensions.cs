using Application.Edge.Node.Grid.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Edge.Node.Grid.Extensions;

/// <summary>
/// Service collection extensions for Grid Device Node options.
/// Implementations are registered in Infrastructure.NetConduit.
/// </summary>
public static class GridDeviceNodeServiceCollectionExtensions
{
    /// <summary>
    /// Registers GridDeviceOptions for edge device node configuration.
    /// </summary>
    public static IServiceCollection AddGridDeviceOptions(this IServiceCollection services, GridDeviceOptions options)
    {
        services.AddSingleton(options);
        return services;
    }
}
