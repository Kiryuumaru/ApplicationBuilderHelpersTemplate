using Application.Authorization.Interfaces;
using Application.Authorization.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Authorization.Extensions;

public static class RoleServiceCollectionExtensions
{
    /// <summary>
    /// Registers role services. The infrastructure layer is expected to provide IRoleRepository.
    /// </summary>
    public static IServiceCollection AddRoleServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IRoleLookup>(static sp =>
        {
            var repository = sp.GetService<IRoleRepository>();
            return new RoleLookupService(repository);
        });
        services.AddScoped<IUserRoleResolver, UserRoleResolver>();
        return services;
    }
}
