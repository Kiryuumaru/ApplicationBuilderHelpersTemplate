using Application.Authorization.Roles.Extensions;
using Application.Identity.Interfaces;
using Application.Identity.Services;
using Domain.Identity.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Identity.Extensions;

public static class IdentityServiceCollectionExtensions
{
    public static IServiceCollection AddIdentityServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddRoleServices();
        services.AddSingleton<IUserStore, InMemoryUserStore>();
        services.AddSingleton<IPasswordCredentialFactory, Pbkdf2PasswordCredentialFactory>();
        services.AddSingleton<IUserSecretValidator, Pbkdf2UserSecretValidator>();
        services.AddSingleton<IIdentityService, IdentityService>();
        return services;
    }
}
