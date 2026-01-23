using System.Net;
using System.Net.Http.Headers;
using Application.Client.Authentication.Interfaces;
using Application.Client.Authentication.Interfaces.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Client.Authentication.Services;

internal class TokenRefreshHandler : DelegatingHandler
{
    private readonly IServiceProvider _serviceProvider;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    public TokenRefreshHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Resolve scoped services at request time to ensure we get the correct instance
        var tokenStorage = _serviceProvider.GetRequiredService<ITokenStorage>();
        var authStateProvider = _serviceProvider.GetRequiredService<IAuthStateProvider>();

        // Attach access token if available
        var credentials = await tokenStorage.GetCredentialsAsync();
        
        if (credentials != null && !string.IsNullOrEmpty(credentials.AccessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credentials.AccessToken);
        }

        var response = await base.SendAsync(request, cancellationToken);

        // If unauthorized and we have a refresh token, try to refresh
        if (response.StatusCode == HttpStatusCode.Unauthorized && credentials?.RefreshToken != null)
        {
            await _refreshLock.WaitAsync(cancellationToken);
            try
            {
                // Re-check credentials (another thread might have refreshed)
                var currentCredentials = await tokenStorage.GetCredentialsAsync();
                if (currentCredentials?.AccessToken != credentials.AccessToken)
                {
                    // Token was already refreshed, retry with new token
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", currentCredentials!.AccessToken);
                    return await base.SendAsync(CloneRequest(request), cancellationToken);
                }

                // Resolve auth client for refresh (avoid circular dependency)
                var authClient = _serviceProvider.GetRequiredService<IAuthenticationClient>();

                // Attempt token refresh
                var refreshResult = await authClient.RefreshTokenAsync(credentials.RefreshToken, cancellationToken);
                if (refreshResult.Success)
                {
                    var newCredentials = new Models.StoredCredentials
                    {
                        AccessToken = refreshResult.AccessToken!,
                        RefreshToken = refreshResult.RefreshToken!,
                        AccessTokenExpiry = DateTimeOffset.UtcNow.AddSeconds(refreshResult.ExpiresIn),
                        RefreshTokenExpiry = DateTimeOffset.UtcNow.AddDays(7) // Assume 7 day refresh token
                    };

                    await authStateProvider.UpdateStateAsync(newCredentials);

                    // Retry the original request with new token
                    var retryRequest = CloneRequest(request);
                    retryRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", newCredentials.AccessToken);
                    return await base.SendAsync(retryRequest, cancellationToken);
                }
                else
                {
                    // Refresh failed - clear auth state
                    await authStateProvider.ClearStateAsync();
                }
            }
            finally
            {
                _refreshLock.Release();
            }
        }

        return response;
    }

    private static HttpRequestMessage CloneRequest(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri);
        clone.Content = request.Content;
        clone.Version = request.Version;

        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        foreach (var option in request.Options)
        {
            clone.Options.TryAdd(option.Key, option.Value);
        }

        return clone;
    }
}
