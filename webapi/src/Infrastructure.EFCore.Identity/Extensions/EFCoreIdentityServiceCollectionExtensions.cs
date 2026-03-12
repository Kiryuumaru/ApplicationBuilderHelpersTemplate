using Domain.Authorization.Entities;
using Domain.Authorization.Interfaces;
using Domain.Identity.Entities;
using Domain.Identity.Interfaces;
using Infrastructure.EFCore.Identity.Configurations;
using Infrastructure.EFCore.Identity.Repositories;
using Infrastructure.EFCore.Identity.Services;
using Infrastructure.EFCore.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.EFCore.Identity.Extensions;

internal static class EFCoreIdentityServiceCollectionExtensions
{
    public static IServiceCollection AddEFCoreIdentity(this IServiceCollection services)
    {
        // Register entity configuration for modular DbContext composition
        services.AddSingleton<IEFCoreEntityConfiguration, IdentityEntityConfiguration>();

        // ASP.NET Core Identity stores (required for UserManager/SignInManager)
        services.AddScoped<IUserStore<User>, EFCoreAspNetUserStore>();
        services.AddScoped<IRoleStore<Role>, EFCoreAspNetRoleStore>();

        // Unit of Work implementations
        services.AddScoped<IIdentityUnitOfWork, EFCoreIdentityUnitOfWork>();
        services.AddScoped<IAuthorizationUnitOfWork, EFCoreAuthorizationUnitOfWork>();

        // Internal repositories for Application layer
        services.AddScoped<IUserRepository, EFCoreUserRepository>();
        services.AddScoped<ISessionRepository, EFCoreSessionRepository>();
        services.AddScoped<IPasskeyRepository, EFCorePasskeyRepository>();
        services.AddScoped<IRoleRepository, EFCoreRoleRepository>();
        services.AddScoped<IApiKeyRepository, EFCoreApiKeyRepository>();

        return services;
    }
}

