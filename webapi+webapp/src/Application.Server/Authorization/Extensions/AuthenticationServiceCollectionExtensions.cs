using Application.Server.Authorization.Interfaces;
using Application.Server.Authorization.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Server.Authorization.Extensions;

public static class AuthenticationServiceCollectionExtensions
{
    internal static IServiceCollection AddAuthenticationServices(this IServiceCollection services)
    {
        services.AddScoped<CredentialsService>();
        services.AddScoped<IPermissionService, PermissionService>();
        
        return services;
    }
}
