using Application.Authorization.Interfaces;
using Application.Authorization.Interfaces.Application;
using Application.Authorization.Interfaces.Infrastructure;
using Application.Authorization.Models;
using Application.Authorization.Services;
using ApplicationBuilderHelpers.Services;
using Infrastructure.EFCore.Identity.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Application.Abstractions.Application;

namespace Infrastructure.EFCore.Identity.Extensions;

/// <summary>
/// Extension methods for registering JWT token services.
/// </summary>
public static class JwtServiceCollectionExtensions
{
    /// <summary>
    /// Registers JWT token services (both internal IJwtTokenService and public ITokenService) as default (non-keyed) services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="jwtConfigurationFactory">Factory to provide JWT configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddJwtTokenServices(
        this IServiceCollection services,
        Func<IServiceProvider, CancellationToken, Task<JwtConfiguration>> jwtConfigurationFactory)
    {
        return services.AddJwtTokenServices(null, jwtConfigurationFactory);
    }

    /// <summary>
    /// Registers JWT token services (both internal IJwtTokenService and public ITokenService).
    /// If serviceKey is null, registers as default (non-keyed) services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="serviceKey">The service key for keyed DI, or null for default registration.</param>
    /// <param name="jwtConfigurationFactory">Factory to provide JWT configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddJwtTokenServices(
        this IServiceCollection services,
        string? serviceKey,
        Func<IServiceProvider, CancellationToken, Task<JwtConfiguration>> jwtConfigurationFactory)
    {
        if (serviceKey is null)
        {
            return AddDefaultJwtTokenServices(services, jwtConfigurationFactory);
        }

        return AddKeyedJwtTokenServices(services, serviceKey, jwtConfigurationFactory);
    }

    private static IServiceCollection AddDefaultJwtTokenServices(
        IServiceCollection services,
        Func<IServiceProvider, CancellationToken, Task<JwtConfiguration>> jwtConfigurationFactory)
    {
        // Register internal JWT token service (non-keyed)
        services.TryAddScoped<IJwtTokenService>(provider =>
        {
            return new Services.JwtTokenService(new Lazy<Func<CancellationToken, Task<JwtConfiguration>>>(() =>
                async ct => await jwtConfigurationFactory(provider, ct)));
        });

        // Register ITokenProvider (non-keyed) - implemented by Infrastructure
        services.TryAddScoped<ITokenProvider>(provider =>
        {
            return new Services.TokenProvider(async ct =>
                provider.GetRequiredService<IJwtTokenService>());
        });

        // Register ITokenService (non-keyed) - implemented by Application layer
        services.TryAddScoped<ITokenService>(provider =>
        {
            return new TokenService(provider.GetRequiredService<ITokenProvider>());
        });

        // Register permission service (non-keyed) - uses ITokenService
        services.TryAddScoped<IPermissionService>(provider =>
        {
            return new PermissionService(provider.GetRequiredService<ITokenService>());
        });

        // Register HttpClient with a default name
        const string defaultHttpClientName = "JwtTokenServices";
        services.AddHttpClient(defaultHttpClientName, (sp, client) =>
        {
            using var scope = sp.CreateScope();
            var applicationConstans = scope.ServiceProvider.GetRequiredService<IApplicationConstants>();
            client.DefaultRequestHeaders.Add("Client-Agent", applicationConstans.AppName);
        });

        services.TryAddTransient<IAuthorizedHttpClientFactory>(sp =>
        {
            return new AuthorizedHttpClientFactory(
                defaultHttpClientName,
                sp.GetRequiredService<ILogger<AuthorizedHttpClientFactory>>(),
                sp.GetRequiredService<IHttpClientFactory>(),
                sp.GetRequiredService<IPermissionService>());
        });

        return services;
    }

    private static IServiceCollection AddKeyedJwtTokenServices(
        IServiceCollection services,
        string serviceKey,
        Func<IServiceProvider, CancellationToken, Task<JwtConfiguration>> jwtConfigurationFactory)
    {
        // Register internal JWT token service (keyed)
        services.AddKeyedScoped<IJwtTokenService>(serviceKey, (provider, _) =>
        {
            return new Services.JwtTokenService(new Lazy<Func<CancellationToken, Task<JwtConfiguration>>>(() =>
                async ct => await jwtConfigurationFactory(provider, ct)));
        });

        // Register ITokenProvider (keyed) - implemented by Infrastructure
        services.AddKeyedScoped<ITokenProvider>(serviceKey, (provider, _) =>
        {
            return new Services.TokenProvider(async ct =>
                provider.GetRequiredKeyedService<IJwtTokenService>(serviceKey));
        });
        
        // Also register non-keyed ITokenProvider for services that need it
        services.TryAddScoped<ITokenProvider>(provider =>
        {
            return new Services.TokenProvider(async ct =>
                provider.GetRequiredKeyedService<IJwtTokenService>(serviceKey));
        });

        // Register ITokenService (keyed) - implemented by Application layer
        services.AddKeyedScoped<ITokenService>(serviceKey, (provider, _) =>
        {
            return new TokenService(provider.GetRequiredKeyedService<ITokenProvider>(serviceKey));
        });
        
        // Also register non-keyed ITokenService for services that need it
        services.TryAddScoped<ITokenService>(provider =>
        {
            return new TokenService(provider.GetRequiredService<ITokenProvider>());
        });

        // Register permission service (keyed) - uses ITokenService
        services.AddKeyedScoped<IPermissionService>(serviceKey, (provider, _) =>
        {
            return new PermissionService(provider.GetRequiredKeyedService<ITokenService>(serviceKey));
        });
        
        // Also register non-keyed IPermissionService for services that need it
        services.TryAddScoped<IPermissionService>(provider =>
        {
            return new PermissionService(provider.GetRequiredService<ITokenService>());
        });

        services.AddHttpClient(serviceKey, (sp, client) =>
        {
            using var scope = sp.CreateScope();
            var applicationConstans = scope.ServiceProvider.GetRequiredService<IApplicationConstants>();
            client.DefaultRequestHeaders.Add("Client-Agent", applicationConstans.AppName);
        });

        services.AddKeyedTransient<IAuthorizedHttpClientFactory>(serviceKey, (sp, _) =>
        {
            return new AuthorizedHttpClientFactory(
                serviceKey,
                sp.GetRequiredService<ILogger<AuthorizedHttpClientFactory>>(),
                sp.GetRequiredService<IHttpClientFactory>(),
                sp.GetRequiredKeyedService<IPermissionService>(serviceKey));
        });

        return services;
    }
}
