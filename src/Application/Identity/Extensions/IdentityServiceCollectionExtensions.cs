using Application.Authorization.Extensions;
using Application.Identity.Interfaces;
using Application.Identity.Services;
using Domain.Authorization.Models;
using Domain.Identity.Interfaces;
using Domain.Identity.Models;
using Domain.Identity.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Identity.Extensions;

internal static class IdentityServiceCollectionExtensions
{
    public static IServiceCollection AddIdentityServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddRoleServices();

        services.AddIdentityCore<User>()
            .AddRoles<Role>()
            .AddSignInManager();

        services.AddScoped<IPasswordVerifier, PasswordHasherVerifier>();
        services.AddScoped<UserAuthenticationService>();
        services.AddScoped<IIdentityService, IdentityService>();
        return services;
    }
}
