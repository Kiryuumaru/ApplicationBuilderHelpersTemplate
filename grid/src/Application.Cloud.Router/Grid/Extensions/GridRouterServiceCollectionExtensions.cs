using Application.Cloud.Router.Grid.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Cloud.Router.Grid.Extensions;

/// <summary>
/// Service collection extensions for Grid Router options.
/// Implementations are registered in Infrastructure.NetConduit.
/// </summary>
public static class GridRouterServiceCollectionExtensions
{
    /// <summary>
    /// Registers GridRouterOptions for router configuration.
    /// </summary>
    public static IServiceCollection AddGridRouterOptions(this IServiceCollection services, GridRouterOptions? options = null)
    {
        services.AddSingleton(options ?? new GridRouterOptions());
        return services;
    }
}
