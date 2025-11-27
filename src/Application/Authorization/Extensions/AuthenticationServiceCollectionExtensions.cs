using Application.Abstractions.Application;
using Application.Authorization.Interfaces;
using Application.Authorization.Models;
using Application.Authorization.Services;
using ApplicationBuilderHelpers.Services;
using Domain.Authorization.Constants;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Application.Authorization.Extensions;

internal static class AuthenticationServiceCollectionExtensions
{
    internal static IServiceCollection AddAuthenticationServices(this IServiceCollection services)
    {
        services.AddScoped<CredentialsService>();
        services.AddSingleton<IJwtTokenServiceFactory, JwtTokenServiceFactory>();
        services.AddSingleton<IPermissionServiceFactory, PermissionServiceFactory>();
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
        services.AddKeyedScoped(serviceKey, (provider, _) =>
        {
            return provider
                .GetRequiredService<IJwtTokenServiceFactory>()
                .Create(async (ct) =>
                {
                    return await jwtConfigurationFactory(provider, ct);
                });
        });
        services.AddKeyedScoped(serviceKey, (provider, _) =>
        {
            return provider
                .GetRequiredService<IPermissionServiceFactory>()
                .Create(async (ct) =>
                {
                    await Task.Yield();
                    return provider.GetRequiredKeyedService<IJwtTokenService>(serviceKey);
                });
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
