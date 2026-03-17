using Application.Cloud.Node.Grid.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Cloud.Node.Grid.Extensions;

/// <summary>
/// Service collection extensions for Grid Cloud Node options.
/// Implementations are registered in Infrastructure.NetConduit.
/// </summary>
public static class GridCloudNodeServiceCollectionExtensions
{
    /// <summary>
    /// Registers GridCloudOptions for cloud node configuration.
    /// </summary>
    public static IServiceCollection AddGridCloudOptions(this IServiceCollection services, GridCloudOptions options)
    {
        services.AddSingleton(options);
        return services;
    }
}
