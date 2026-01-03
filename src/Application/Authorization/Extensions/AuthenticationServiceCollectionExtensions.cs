using Application.Abstractions.Application;
using Application.Authorization.Interfaces;
using Application.Authorization.Interfaces.Application;
using Application.Authorization.Interfaces.Infrastructure;
using Application.Authorization.Models;
using Application.Authorization.Services;
using ApplicationBuilderHelpers.Services;
using Domain.Authorization.Constants;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Application.Authorization.Extensions;

internal static class AuthenticationServiceCollectionExtensions
{
    internal static IServiceCollection AddAuthenticationServices(this IServiceCollection services)
    {
        services.AddScoped<CredentialsService>();
        services.AddJwtAuthentication("GOAT_CLOUD", async (sp, ct) =>
        {
            using var scope = sp.CreateScope();
            var cloudAuthenticationServices = scope.ServiceProvider.GetRequiredService<CredentialsService>();
            var cloudCreds = await cloudAuthenticationServices.GetCredentials(ct);
            return cloudCreds.JwtConfiguration;
        });

        return services;
    }

    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services,
        string serviceKey,
        Func<IServiceProvider, CancellationToken, Task<JwtConfiguration>> jwtConfigurationFactory)
    {
        // Register internal JWT token service (keyed)
        services.AddKeyedScoped<IJwtTokenService>(serviceKey, (provider, _) =>
        {
            return new JwtTokenService(new Lazy<Func<CancellationToken, Task<JwtConfiguration>>>(() =>
                async ct => await jwtConfigurationFactory(provider, ct)));
        });

        // Register public ITokenService (keyed)
        services.AddKeyedScoped<ITokenService>(serviceKey, (provider, _) =>
        {
            return new TokenService(async ct =>
                provider.GetRequiredKeyedService<IJwtTokenService>(serviceKey));
        });
        
        // Also register non-keyed ITokenService for services that need it
        services.TryAddScoped<ITokenService>(provider =>
        {
            return new TokenService(async ct =>
                provider.GetRequiredKeyedService<IJwtTokenService>(serviceKey));
        });

        // Register permission service (keyed)
        services.AddKeyedScoped<IPermissionService>(serviceKey, (provider, _) =>
        {
            return new PermissionService(async ct =>
                provider.GetRequiredKeyedService<IJwtTokenService>(serviceKey));
        });
        
        // Also register non-keyed IPermissionService for services that need it
        services.TryAddScoped<IPermissionService>(provider =>
        {
            return new PermissionService(async ct =>
                provider.GetRequiredKeyedService<IJwtTokenService>(serviceKey));
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
