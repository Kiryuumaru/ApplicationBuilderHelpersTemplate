using Application.Authorization.Interfaces;
using Application.Authorization.Services;
using Domain.Identity.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Authorization.Extensions;

public static class RoleServiceCollectionExtensions
{
    /// <summary>
    /// Registers role services. Note: IRoleRepository and IRoleLookup must be registered
    /// by the Infrastructure layer (e.g., via AddEFCoreIdentity).
    /// </summary>
    public static IServiceCollection AddRoleServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IUserRoleResolver>(static sp => new UserRoleResolver(sp.GetRequiredService<IRoleLookup>()));
        return services;
    }
}
