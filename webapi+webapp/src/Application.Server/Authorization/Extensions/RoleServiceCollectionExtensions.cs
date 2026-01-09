using Application.Server.Authorization.Interfaces;
using Application.Server.Authorization.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Server.Authorization.Extensions;

public static class RoleServiceCollectionExtensions
{
    /// <summary>
    /// Registers role services. The infrastructure layer is expected to provide IRoleRepository.
    /// </summary>
    public static IServiceCollection AddRoleServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IUserRoleResolver, UserRoleResolver>();
        return services;
    }
}
