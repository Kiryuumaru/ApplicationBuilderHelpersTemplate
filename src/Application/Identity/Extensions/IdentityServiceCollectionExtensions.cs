using Application.Authorization.Extensions;
using Application.Identity.Interfaces;
using Application.Identity.Services;
using Domain.Authorization.Models;
using Domain.Identity.Interfaces;
using Domain.Identity.Models;
using Domain.Identity.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Application.Identity.Extensions;

internal static class IdentityServiceCollectionExtensions
{
    public static IServiceCollection AddIdentityServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddRoleServices();
        services.AddAuthenticationServices();

        services.AddIdentityCore<User>()
            .AddRoles<Role>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        // Replace default user validator with our custom one that allows null usernames for anonymous users
        // Must be done after AddIdentityCore to override the default UserValidator<User>
        var defaultValidatorDescriptor = services.FirstOrDefault(d =>
            d.ServiceType == typeof(IUserValidator<User>) &&
            d.ImplementationType == typeof(UserValidator<User>));
        if (defaultValidatorDescriptor != null)
        {
            services.Remove(defaultValidatorDescriptor);
        }
        services.AddScoped<IUserValidator<User>, AnonymousUserValidator>();

        services.AddScoped<IPasswordVerifier, PasswordHasherVerifier>();
        services.AddScoped<UserAuthenticationService>();
        
        // Focused identity services
        services.AddScoped<IUserRegistrationService, UserRegistrationService>();
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<IPasswordService, PasswordService>();
        services.AddScoped<ITwoFactorService, TwoFactorService>();
        services.AddScoped<IUserProfileService, UserProfileService>();
        services.AddScoped<IUserAuthorizationService, UserAuthorizationService>();
        services.AddScoped<ISessionService, SessionService>();
        
        services.AddScoped<IAnonymousUserCleanupService, AnonymousUserCleanupService>();

        // OAuth service - using mock implementation by default
        // Replace with real implementation when configuring actual OAuth providers
        services.AddScoped<IOAuthService, MockOAuthService>();

        return services;
    }
}
