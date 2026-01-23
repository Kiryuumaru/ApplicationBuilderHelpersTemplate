using System.Diagnostics.CodeAnalysis;
using Application.Client.Authentication.Interfaces;
using Application.Client.Authentication.Interfaces.Infrastructure;
using Application.Client.Authentication.Services;
using Application.Client.Authorization.Interfaces;
using Application.Client.Authorization.Services;
using Application.Client.Common.Extensions;
using Application.Client.Iam.Interfaces;
using Application.Client.Iam.Services;
using ApplicationBuilderHelpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Client;

public class ClientApplication : Application
{
    public override void AddServices(ApplicationHostBuilder applicationBuilder, IServiceCollection services)
    {
        base.AddServices(applicationBuilder, services);

        // Token storage using local store (IndexedDB in browser)
        services.AddScoped<ITokenStorage, LocalStoreTokenStorage>();

        // Auth state provider - registered as both concrete and interface
        // Concrete type used by RunPreparationAsync for initialization
        services.AddScoped<ClientAuthStateProvider>();
        services.AddScoped<IAuthStateProvider>(sp => sp.GetRequiredService<ClientAuthStateProvider>());

        // Client-side permission evaluation service
        services.AddScoped<IClientPermissionService, ClientPermissionService>();

        // Token refresh handler for authenticated requests
        services.AddTransient<TokenRefreshHandler>();

        // Authentication client (no token needed for login/register)
        services.AddApiClient<IAuthenticationClient, AuthenticationClient>(authenticated: false);

        // Authenticated API clients
        services.AddApiClient<ISessionsClient, SessionsClient>();
        services.AddApiClient<IApiKeysClient, ApiKeysClient>();
        services.AddApiClient<ITwoFactorClient, TwoFactorClient>();
        services.AddApiClient<IUserProfileClient, UserProfileClient>();
        services.AddApiClient<IPasskeysClient, PasskeysClient>();
        services.AddApiClient<IUsersClient, UsersClient>();
        services.AddApiClient<IRolesClient, RolesClient>();
        services.AddApiClient<IPermissionsClient, PermissionsClient>();
    }

    public override async ValueTask RunPreparationAsync(ApplicationHost applicationHost, CancellationToken cancellationToken)
    {
        await base.RunPreparationAsync(applicationHost, cancellationToken);
        var authStateProvider = applicationHost.Services.GetRequiredService<ClientAuthStateProvider>();
        await authStateProvider.InitializeAsync();
    }
}

file static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers a typed HTTP client for API calls.
    /// </summary>
    /// <param name="authenticated">If true, adds TokenRefreshHandler for authentication.</param>
    public static IServiceCollection AddApiClient<TInterface, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>(
        this IServiceCollection services,
        bool authenticated = true)
        where TInterface : class
        where TImplementation : class, TInterface
    {
        var builder = services.AddHttpClient<TInterface, TImplementation>((sp, client) =>
        {
            client.BaseAddress = sp.GetRequiredService<IConfiguration>().GetApiEndpoint();
        });

        if (authenticated)
        {
            builder.AddHttpMessageHandler<TokenRefreshHandler>();
        }

        return services;
    }
}
