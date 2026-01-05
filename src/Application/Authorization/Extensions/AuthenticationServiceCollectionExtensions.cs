using Application.Authorization.Interfaces;
using Application.Authorization.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Authorization.Extensions;

internal static class AuthenticationServiceCollectionExtensions
{
    internal static IServiceCollection AddAuthenticationServices(this IServiceCollection services)
    {
        services.AddScoped<CredentialsService>();
        
        // Application layer services - ITokenService wraps the infrastructure ITokenProvider
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IPermissionService, PermissionService>();
        
        return services;
    }
}
