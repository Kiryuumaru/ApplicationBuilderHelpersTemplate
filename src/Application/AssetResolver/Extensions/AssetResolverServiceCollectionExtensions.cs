using Application.AssetResolver.Services;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.AssetResolver.Extensions;

internal static class AssetResolverServiceCollectionExtensions
{
    public static IServiceCollection AddAssetResolverServices(this IServiceCollection services)
    {
        services.AddScoped<AssetResolverService>();
        return services;
    }
}
