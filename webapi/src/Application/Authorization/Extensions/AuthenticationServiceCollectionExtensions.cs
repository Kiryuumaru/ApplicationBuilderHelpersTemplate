using Application.Authorization.Interfaces;
using Application.Authorization.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Authorization.Extensions;

internal static class AuthenticationServiceCollectionExtensions
{
    internal static IServiceCollection AddAuthenticationServices(this IServiceCollection services)
    {
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IUserRoleResolver, UserRoleResolver>();

        return services;
    }
}
