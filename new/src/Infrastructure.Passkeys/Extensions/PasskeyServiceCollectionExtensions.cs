using Application.Identity.Interfaces;
using Fido2NetLib;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Passkeys.Extensions;

/// <summary>
/// Extension methods for registering passkey infrastructure services.
/// </summary>
public static class PasskeyServiceCollectionExtensions
{
    /// <summary>
    /// Adds passkey (WebAuthn/FIDO2) services to the service collection.
    /// </summary>
    public static IServiceCollection AddPasskeyInfrastructure(
        this IServiceCollection services,
        Fido2Configuration config)
    {
        // Add Fido2 library
        services.AddSingleton(config);
        services.AddSingleton<IFido2, Fido2>();

        // Add passkey service
        services.AddScoped<IPasskeyService, PasskeyService>();

        return services;
    }

    /// <summary>
    /// Adds passkey (WebAuthn/FIDO2) services to the service collection with configuration action.
    /// </summary>
    public static IServiceCollection AddPasskeyInfrastructure(
        this IServiceCollection services,
        Action<Fido2Configuration> configure)
    {
        var config = new Fido2Configuration();
        configure(config);
        return services.AddPasskeyInfrastructure(config);
    }
}
