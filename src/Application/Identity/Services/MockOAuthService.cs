using Application.Identity.Interfaces;
using Application.Identity.Models;
using Domain.Identity.Enums;
using Domain.Identity.Exceptions;
using System.Security.Cryptography;

namespace Application.Identity.Services;

/// <summary>
/// Mock OAuth service for development and testing.
/// This implementation simulates OAuth flows without connecting to real providers.
/// Replace with real provider implementations when ready to integrate.
/// </summary>
public sealed class MockOAuthService : IOAuthService
{
    private static readonly Dictionary<ExternalLoginProvider, OAuthProviderConfig> _providers = new()
    {
        [ExternalLoginProvider.Mock] = new OAuthProviderConfig
        {
            Provider = ExternalLoginProvider.Mock,
            DisplayName = "Mock Provider",
            IsEnabled = true,
            IconName = "test"
        },
        [ExternalLoginProvider.Google] = new OAuthProviderConfig
        {
            Provider = ExternalLoginProvider.Google,
            DisplayName = "Google",
            IsEnabled = false, // Disabled until configured
            IconName = "google"
        },
        [ExternalLoginProvider.GitHub] = new OAuthProviderConfig
        {
            Provider = ExternalLoginProvider.GitHub,
            DisplayName = "GitHub",
            IsEnabled = false,
            IconName = "github"
        },
        [ExternalLoginProvider.Microsoft] = new OAuthProviderConfig
        {
            Provider = ExternalLoginProvider.Microsoft,
            DisplayName = "Microsoft",
            IsEnabled = false,
            IconName = "microsoft"
        },
        [ExternalLoginProvider.Discord] = new OAuthProviderConfig
        {
            Provider = ExternalLoginProvider.Discord,
            DisplayName = "Discord",
            IsEnabled = false,
            IconName = "discord"
        }
    };

    // In-memory state storage for mock provider (in production, use distributed cache)
    private static readonly Dictionary<string, MockOAuthState> _pendingStates = new();

    public Task<IReadOnlyCollection<OAuthProviderConfig>> GetProvidersAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyCollection<OAuthProviderConfig>>(_providers.Values.ToList());
    }

    public Task<bool> IsProviderEnabledAsync(ExternalLoginProvider provider, CancellationToken cancellationToken)
    {
        return Task.FromResult(_providers.TryGetValue(provider, out var config) && config.IsEnabled);
    }

    public Task<OAuthAuthorizationUrl> GetAuthorizationUrlAsync(
        ExternalLoginProvider provider,
        string redirectUri,
        CancellationToken cancellationToken)
    {
        if (!_providers.TryGetValue(provider, out var config) || !config.IsEnabled)
        {
            throw new OAuthProviderException($"OAuth provider '{provider}' is not enabled.", provider.ToString());
        }

        // Generate a secure state parameter
        var state = GenerateSecureState();

        // For the mock provider, we'll return a fake URL that the test can use
        string authorizationUrl;
        if (provider == ExternalLoginProvider.Mock)
        {
            // Mock provider returns a URL pointing to our callback with a test code
            // In real implementation, this would be the provider's authorization endpoint
            var mockCode = $"mock_code_{Guid.NewGuid():N}";
            _pendingStates[state] = new MockOAuthState(provider, redirectUri, mockCode);

            // Return URL that simulates immediate callback (for testing)
            authorizationUrl = $"{redirectUri}?code={mockCode}&state={state}";
        }
        else
        {
            // Placeholder URLs for real providers (not functional until configured)
            authorizationUrl = provider switch
            {
                ExternalLoginProvider.Google => $"https://accounts.google.com/o/oauth2/v2/auth?client_id=PLACEHOLDER&redirect_uri={Uri.EscapeDataString(redirectUri)}&state={state}&response_type=code&scope=openid%20email%20profile",
                ExternalLoginProvider.GitHub => $"https://github.com/login/oauth/authorize?client_id=PLACEHOLDER&redirect_uri={Uri.EscapeDataString(redirectUri)}&state={state}&scope=user:email",
                ExternalLoginProvider.Microsoft => $"https://login.microsoftonline.com/common/oauth2/v2.0/authorize?client_id=PLACEHOLDER&redirect_uri={Uri.EscapeDataString(redirectUri)}&state={state}&response_type=code&scope=openid%20email%20profile",
                ExternalLoginProvider.Discord => $"https://discord.com/api/oauth2/authorize?client_id=PLACEHOLDER&redirect_uri={Uri.EscapeDataString(redirectUri)}&state={state}&response_type=code&scope=identify%20email",
                _ => throw new NotSupportedException($"Provider '{provider}' is not supported.")
            };

            _pendingStates[state] = new MockOAuthState(provider, redirectUri, null);
        }

        return Task.FromResult(new OAuthAuthorizationUrl
        {
            AuthorizationUrl = authorizationUrl,
            State = state
        });
    }

    public Task<OAuthCallbackResult> ProcessCallbackAsync(
        ExternalLoginProvider provider,
        string code,
        string state,
        string expectedState,
        string redirectUri,
        CancellationToken cancellationToken)
    {
        // Validate state
        if (!ValidateState(state, expectedState))
        {
            return Task.FromResult(OAuthCallbackResult.Failure("invalid_state", "The state parameter is invalid or expired."));
        }

        // For mock provider, return mock user info
        if (provider == ExternalLoginProvider.Mock)
        {
            // Clean up state
            _pendingStates.Remove(state);

            // Parse mock code to extract test data (format: mock_code_{guid} or mock_auth_code)
            // In tests, code can be overridden to specify user details
            var mockUserId = Guid.NewGuid().ToString("N");
            if (code.Contains("_"))
            {
                var parts = code.Split('_');
                if (parts.Length > 0 && parts[^1].Length >= 8)
                {
                    mockUserId = parts[^1];
                }
            }

            var shortId = mockUserId.Length >= 8 ? mockUserId[..8] : mockUserId;

            var userInfo = new OAuthUserInfo
            {
                Provider = ExternalLoginProvider.Mock,
                ProviderSubject = $"mock_user_{mockUserId}",
                Email = $"mockuser_{shortId}@example.com",
                EmailVerified = true,
                Name = $"Mock User {shortId}",
                PictureUrl = null,
                AdditionalClaims = new Dictionary<string, string>
                {
                    ["mock_code"] = code
                }
            };

            return Task.FromResult(OAuthCallbackResult.Success(userInfo));
        }

        // For real providers (not implemented yet)
        return Task.FromResult(OAuthCallbackResult.Failure(
            "not_implemented",
            $"OAuth provider '{provider}' is not yet implemented. Please configure the provider credentials."));
    }

    public bool ValidateState(string state, string expectedState)
    {
        return !string.IsNullOrEmpty(state) &&
               !string.IsNullOrEmpty(expectedState) &&
               string.Equals(state, expectedState, StringComparison.Ordinal);
    }

    private static string GenerateSecureState()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }

    private sealed record MockOAuthState(ExternalLoginProvider Provider, string RedirectUri, string? MockCode);
}
