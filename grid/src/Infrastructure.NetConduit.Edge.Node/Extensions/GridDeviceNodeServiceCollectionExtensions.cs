using Application.Edge.Node.Grid.Interfaces.Inbound;
using Application.Edge.Node.Grid.Models;
using Infrastructure.NetConduit.Edge.Node.Adapters;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.NetConduit.Edge.Node.Extensions;

/// <summary>
/// Service collection extensions for Grid Device Node using NetConduit.
/// </summary>
public static class GridDeviceNodeServiceCollectionExtensions
{
    /// <summary>
    /// Registers Grid device node service using NetConduit.
    /// </summary>
    public static IServiceCollection AddGridDeviceNode(this IServiceCollection services, GridDeviceOptions options)
    {
        services.AddSingleton(options);
        services.AddSingleton<IGridDeviceNode, GridDeviceNode>();
        return services;
    }
}
