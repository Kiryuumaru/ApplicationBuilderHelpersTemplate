using Application.Authorization.Interfaces;
using Domain.Authorization.Models;
using Domain.Identity.Models;
using Infrastructure.EFCore.Identity.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.EFCore.Identity.Extensions;

internal static class EFCoreIdentityServiceCollectionExtensions
{
    public static IServiceCollection AddEFCoreIdentityStores(this IServiceCollection services)
    {
        services.AddScoped<IUserStore<User>, EFCoreUserStore>();
        services.AddScoped<IRoleStore<Role>, EFCoreRoleStore>();
        services.AddScoped<EFCoreRoleRepository>();
        services.AddScoped<IRoleRepository>(sp => sp.GetRequiredService<EFCoreRoleRepository>());
        services.AddScoped<IRoleLookup>(sp => sp.GetRequiredService<EFCoreRoleRepository>());
        return services;
    }
}
