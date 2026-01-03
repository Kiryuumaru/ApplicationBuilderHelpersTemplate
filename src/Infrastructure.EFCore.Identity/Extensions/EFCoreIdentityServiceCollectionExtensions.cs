using Application.Authorization.Interfaces;
using Application.Identity.Interfaces;
using Domain.Authorization.Models;
using Domain.Identity.Models;
using Infrastructure.EFCore.Identity.Configurations;
using Infrastructure.EFCore.Identity.Services;
using Infrastructure.EFCore.Identity.Workers;
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

        services.AddScoped<EFCoreUserStore>();
        services.AddScoped<IUserStore<User>>(sp => sp.GetRequiredService<EFCoreUserStore>());
        services.AddScoped<IUserStore>(sp => sp.GetRequiredService<EFCoreUserStore>());
        services.AddScoped<IRoleStore<Role>, EFCoreRoleStore>();
        services.AddScoped<EFCoreRoleRepository>();
        services.AddScoped<IRoleRepository>(sp => sp.GetRequiredService<EFCoreRoleRepository>());

        // Passkey stores
        services.AddScoped<IPasskeyChallengeStore, EFCorePasskeyChallengeStore>();
        services.AddScoped<IPasskeyCredentialStore, EFCorePasskeyCredentialStore>();

        // Session store
        services.AddScoped<ISessionStore, EFCoreSessionStore>();

        // External login store
        services.AddScoped<IExternalLoginStore, EFCoreExternalLoginStore>();

        // Background worker for anonymous user cleanup
        services.AddHostedService<AnonymousUserCleanupWorker>();

        return services;
    }
}

