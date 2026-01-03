using Application.Authorization.Interfaces;
using Domain.Authorization.Models;
using Domain.Identity.Models;
using Infrastructure.EFCore.Identity.Configurations;
using Infrastructure.EFCore.Identity.Services;
using Infrastructure.EFCore.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.EFCore.Identity.Extensions;

public static class EFCoreIdentityServiceCollectionExtensions
{
    public static IServiceCollection AddEFCoreIdentity(this IServiceCollection services)
    {
        // Register entity configuration for modular DbContext composition
        services.AddSingleton<IEFCoreEntityConfiguration, IdentityEntityConfiguration>();

        services.AddScoped<IUserStore<User>, EFCoreUserStore>();
        services.AddScoped<IRoleStore<Role>, EFCoreRoleStore>();
        services.AddScoped<EFCoreRoleRepository>();
        services.AddScoped<IRoleRepository>(sp => sp.GetRequiredService<EFCoreRoleRepository>());
        return services;
    }
}

