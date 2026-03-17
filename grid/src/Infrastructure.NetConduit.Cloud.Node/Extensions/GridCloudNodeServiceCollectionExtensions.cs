using Application.Cloud.Node.Grid.Interfaces.Inbound;
using Application.Cloud.Node.Grid.Models;
using Infrastructure.NetConduit.Cloud.Node.Adapters;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.NetConduit.Cloud.Node.Extensions;

/// <summary>
/// Service collection extensions for Grid Cloud Node using NetConduit.
/// </summary>
public static class GridCloudNodeServiceCollectionExtensions
{
    /// <summary>
    /// Registers Grid cloud node service using NetConduit.
    /// </summary>
    public static IServiceCollection AddGridCloudNode(this IServiceCollection services, GridCloudOptions options)
    {
        services.AddSingleton(options);
        services.AddSingleton<IGridCloudNode, GridCloudNode>();
        services.AddSingleton<GridCloudNode>(sp => (GridCloudNode)sp.GetRequiredService<IGridCloudNode>());
        return services;
    }
}
