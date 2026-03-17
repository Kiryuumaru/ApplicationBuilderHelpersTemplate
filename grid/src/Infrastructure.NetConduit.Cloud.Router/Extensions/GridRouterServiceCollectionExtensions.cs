using Application.Cloud.Router.Grid.Interfaces.Inbound;
using Application.Cloud.Router.Grid.Models;
using Infrastructure.NetConduit.Cloud.Router.Adapters;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.NetConduit.Cloud.Router.Extensions;

/// <summary>
/// Service collection extensions for Grid Router using NetConduit.
/// </summary>
public static class GridRouterServiceCollectionExtensions
{
    /// <summary>
    /// Registers Grid router service using NetConduit.
    /// </summary>
    public static IServiceCollection AddGridRouter(this IServiceCollection services, GridRouterOptions? options = null)
    {
        services.AddSingleton(options ?? new GridRouterOptions());
        services.AddSingleton<IGridRouter, GridRouter>();
        services.AddSingleton<GridRouter>(sp => (GridRouter)sp.GetRequiredService<IGridRouter>());
        return services;
    }
}
