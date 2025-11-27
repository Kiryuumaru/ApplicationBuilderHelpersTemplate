using Application.Authorization.Interfaces;
using Application.Authorization.Services;
using Domain.Identity.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Authorization.Extensions;

public static class RoleServiceCollectionExtensions
{
    public static IServiceCollection AddRoleServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<InMemoryRoleRepository>();
        services.AddSingleton<IRoleRepository>(static sp => sp.GetRequiredService<InMemoryRoleRepository>());
        services.AddSingleton<IRoleLookup>(static sp => sp.GetRequiredService<InMemoryRoleRepository>());
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IUserRoleResolver>(static sp => new UserRoleResolver(sp.GetRequiredService<IRoleLookup>()));
        return services;
    }
}
