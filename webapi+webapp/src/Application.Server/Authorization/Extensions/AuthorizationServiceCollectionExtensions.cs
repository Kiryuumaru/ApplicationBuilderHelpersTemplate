using Application.Server.Authorization.Interfaces;
using Application.Server.Authorization.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Server.Authorization.Extensions;

public static class AuthorizationServiceCollectionExtensions
{
    internal static IServiceCollection AddAuthorizationServices(this IServiceCollection services)
    {
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IUserRoleResolver, UserRoleResolver>();

        return services;
    }
}
