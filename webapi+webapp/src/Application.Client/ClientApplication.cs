using Application.Client.Authentication.Interfaces;
using Application.Client.Authentication.Interfaces.Infrastructure;
using Application.Client.Authentication.Services;
using Application.Client.Common.Extensions;
using Application.Client.Iam.Interfaces;
using Application.Client.Iam.Services;
using ApplicationBuilderHelpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Client;

public class ClientApplication : Application
{
    public override void AddServices(ApplicationHostBuilder applicationBuilder, IServiceCollection services)
    {
        base.AddServices(applicationBuilder, services);

        // Auth state provider
        services.AddScoped<IAuthStateProvider, ClientAuthStateProvider>();

        // Authentication client (for login/register endpoints - no token needed)
        services.AddHttpClient<IAuthenticationClient, AuthenticationClient>((sp, client) =>
        {
            client.BaseAddress = sp.GetRequiredService<IConfiguration>().GetApiEndpoint();
        });

        // Token refresh handler for authenticated requests
        services.AddTransient<TokenRefreshHandler>();

        // Authenticated HTTP client for API calls
        services.AddHttpClient("API", (sp, client) =>
        {
            client.BaseAddress = sp.GetRequiredService<IConfiguration>().GetApiEndpoint();
        }).AddHttpMessageHandler<TokenRefreshHandler>();

        // Factory for creating authenticated HttpClient
        services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("API"));

        // Auth-related clients (sessions, API keys, 2FA, profile)
        services.AddHttpClient<ISessionsClient, SessionsClient>((sp, client) =>
        {
            client.BaseAddress = sp.GetRequiredService<IConfiguration>().GetApiEndpoint();
        }).AddHttpMessageHandler<TokenRefreshHandler>();

        services.AddHttpClient<IApiKeysClient, ApiKeysClient>((sp, client) =>
        {
            client.BaseAddress = sp.GetRequiredService<IConfiguration>().GetApiEndpoint();
        }).AddHttpMessageHandler<TokenRefreshHandler>();

        services.AddHttpClient<ITwoFactorClient, TwoFactorClient>((sp, client) =>
        {
            client.BaseAddress = sp.GetRequiredService<IConfiguration>().GetApiEndpoint();
        }).AddHttpMessageHandler<TokenRefreshHandler>();

        services.AddHttpClient<IUserProfileClient, UserProfileClient>((sp, client) =>
        {
            client.BaseAddress = sp.GetRequiredService<IConfiguration>().GetApiEndpoint();
        }).AddHttpMessageHandler<TokenRefreshHandler>();

        // IAM clients (users, roles, permissions)
        services.AddHttpClient<IUsersClient, UsersClient>((sp, client) =>
        {
            client.BaseAddress = sp.GetRequiredService<IConfiguration>().GetApiEndpoint();
        }).AddHttpMessageHandler<TokenRefreshHandler>();

        services.AddHttpClient<IRolesClient, RolesClient>((sp, client) =>
        {
            client.BaseAddress = sp.GetRequiredService<IConfiguration>().GetApiEndpoint();
        }).AddHttpMessageHandler<TokenRefreshHandler>();

        services.AddHttpClient<IPermissionsClient, PermissionsClient>((sp, client) =>
        {
            client.BaseAddress = sp.GetRequiredService<IConfiguration>().GetApiEndpoint();
        }).AddHttpMessageHandler<TokenRefreshHandler>();
    }

    public override async ValueTask RunPreparationAsync(ApplicationHost applicationHost, CancellationToken cancellationToken)
    {
        await base.RunPreparationAsync(applicationHost, cancellationToken);
        var authStateProvider = applicationHost.Services.GetRequiredService<IAuthStateProvider>();
        await authStateProvider.InitializeAsync();
    }
}
