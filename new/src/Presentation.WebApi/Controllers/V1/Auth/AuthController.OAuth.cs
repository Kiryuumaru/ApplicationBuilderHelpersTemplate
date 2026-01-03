using Application.Identity.Models;
using Domain.Authorization.Constants;
using Domain.Identity.Enums;
using Domain.Identity.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Presentation.WebApi.Attributes;
using Presentation.WebApi.Models.Requests;
using Presentation.WebApi.Models.Responses;
using System.ComponentModel.DataAnnotations;

namespace Presentation.WebApi.Controllers.V1;

public partial class AuthController
{
    #region OAuth External Login

    /// <summary>
    /// Gets available OAuth providers.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of available OAuth providers.</returns>
    /// <response code="200">Returns available providers.</response>
    [HttpGet("external/providers")]
    [AllowAnonymous]
    [ProducesResponseType<OAuthProvidersResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOAuthProviders(CancellationToken cancellationToken)
    {
        var providers = await oauthService.GetProvidersAsync(cancellationToken);

        return Ok(new OAuthProvidersResponse
        {
            Providers = providers.Select(p => new OAuthProviderResponse
            {
                Provider = p.Provider.ToString().ToLowerInvariant(),
                DisplayName = p.DisplayName,
                IsEnabled = p.IsEnabled,
                IconName = p.IconName
            }).ToList()
        });
    }

    /// <summary>
    /// Initiates OAuth login flow with an external provider.
    /// </summary>
    /// <param name="provider">The OAuth provider name (e.g., "google", "github", "mock").</param>
    /// <param name="request">OAuth login request with redirect URI.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Authorization URL and state for OAuth flow.</returns>
    /// <response code="200">Returns authorization URL and state.</response>
    /// <response code="400">Provider not found or not enabled.</response>
    [HttpPost("external/{provider}")]
    [AllowAnonymous]
    [ProducesResponseType<OAuthAuthorizationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> InitiateOAuthLogin(
        string provider,
        [FromBody] OAuthLoginRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryParseProvider(provider, out var parsedProvider))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid provider",
                Detail = $"OAuth provider '{provider}' is not supported."
            });
        }

        var isEnabled = await oauthService.IsProviderEnabledAsync(parsedProvider, cancellationToken);
        if (!isEnabled)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Provider not enabled",
                Detail = $"OAuth provider '{provider}' is not currently enabled."
            });
        }

        var authUrl = await oauthService.GetAuthorizationUrlAsync(
            parsedProvider,
            request.RedirectUri,
            cancellationToken);

        return Ok(new OAuthAuthorizationResponse
        {
            AuthorizationUrl = authUrl.AuthorizationUrl,
            State = authUrl.State
        });
    }

    /// <summary>
    /// Processes OAuth callback and completes login or registration.
    /// </summary>
    /// <param name="request">OAuth callback data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>JWT tokens on success.</returns>
    /// <response code="200">Login successful, returns tokens.</response>
    /// <response code="201">New user registered and logged in.</response>
    /// <response code="400">Invalid callback data or state mismatch.</response>
    /// <response code="401">OAuth authentication failed.</response>
    [HttpPost("external/callback")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ProcessOAuthCallback(
        [FromBody] OAuthCallbackRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryParseProvider(request.Provider, out var provider))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid provider",
                Detail = $"OAuth provider '{request.Provider}' is not supported."
            });
        }

        // Note: In a real implementation, expectedState should come from session/cookie
        // For this API-first approach, the client is responsible for state management
        var result = await oauthService.ProcessCallbackAsync(
            provider,
            request.Code,
            request.State,
            request.State, // Client manages state verification
            request.RedirectUri,
            cancellationToken);

        if (!result.Succeeded)
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = result.Error ?? "Authentication failed",
                Detail = result.ErrorDescription ?? "OAuth authentication was not successful."
            });
        }

        var userInfo = result.UserInfo!;

        // Check if user already exists with this external login
        var existingUserId = await externalLoginStore.FindUserByLoginAsync(
            userInfo.Provider,
            userInfo.ProviderSubject,
            cancellationToken);

        bool isNewUser = false;
        User? user;
        UserSession userSession;

        if (existingUserId.HasValue)
        {
            // Existing user - create session
            user = await identityService.GetByIdAsync(existingUserId.Value, cancellationToken)
                ?? throw new InvalidOperationException("User not found for external login.");

            userSession = await identityService.CreateSessionForUserAsync(existingUserId.Value, cancellationToken);
        }
        else
        {
            // New user - register with external login
            isNewUser = true;

            // Generate a unique username from provider info
            var baseUsername = GenerateUsernameFromOAuth(userInfo);

            var registrationRequest = new ExternalUserRegistrationRequest(
                Username: baseUsername,
                Provider: userInfo.Provider.ToString(),
                ProviderSubject: userInfo.ProviderSubject,
                ProviderEmail: userInfo.Email,
                ProviderDisplayName: userInfo.Name,
                Email: userInfo.EmailVerified ? userInfo.Email : null,
                AutoActivate: true);

            user = await identityService.RegisterExternalAsync(registrationRequest, cancellationToken);

            // Link the external login (in case RegisterExternalAsync didn't do it via UserLoginInfo)
            await externalLoginStore.AddLoginAsync(
                user.Id,
                userInfo.Provider,
                userInfo.ProviderSubject,
                userInfo.Name,
                userInfo.Email,
                cancellationToken);

            // Create session for new user
            userSession = await identityService.CreateSessionForUserAsync(user.Id, cancellationToken);
        }

        // Create session and tokens
        var (accessToken, refreshToken, loginSession) = await CreateSessionAndTokensAsync(userSession, cancellationToken);

        var response = new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresIn = AccessTokenExpirationMinutes * 60,
            User = new UserInfo
            {
                Id = userSession.UserId,
                Username = userSession.Username,
                Email = user?.Email,
                Roles = userSession.RoleCodes,
                Permissions = userSession.PermissionIdentifiers
            }
        };

        return isNewUser ? CreatedAtAction(nameof(GetMe), response) : Ok(response);
    }

    /// <summary>
    /// Gets the user's linked external logins.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of linked external logins.</returns>
    /// <response code="200">Returns linked external logins.</response>
    /// <response code="401">User not authenticated.</response>
    [HttpGet("users/{userId:guid}/identity/external")]
    [RequiredPermission(PermissionIds.Api.Auth.Identity.External.List.Identifier)]
    [ProducesResponseType<ExternalLoginsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetExternalLogins(
        [FromRoute, Required, PermissionParameter(PermissionIds.Api.Auth.UserIdParameter)] Guid userId,
        CancellationToken cancellationToken)
    {
        var logins = await externalLoginStore.GetLoginsAsync(userId, cancellationToken);

        return Ok(new ExternalLoginsResponse
        {
            Logins = logins.Select(l => new ExternalLoginResponse
            {
                Provider = l.Provider,
                DisplayName = l.DisplayName,
                Email = l.Email,
                LinkedAt = l.LinkedAt
            }).ToList()
        });
    }

    private static bool TryParseProvider(string provider, out ExternalLoginProvider result)
    {
        return Enum.TryParse(provider, ignoreCase: true, out result);
    }

    private static string GenerateUsernameFromOAuth(OAuthUserInfo userInfo)
    {
        // Try to create a username from the provider info
        var baseUsername = userInfo.Name?.Replace(" ", "").ToLowerInvariant()
            ?? userInfo.Email?.Split('@')[0].ToLowerInvariant()
            ?? $"user_{userInfo.ProviderSubject[..Math.Min(8, userInfo.ProviderSubject.Length)]}";

        // Ensure uniqueness by adding random suffix
        var suffix = Guid.NewGuid().ToString("N")[..6];
        return $"{baseUsername}_{suffix}";
    }

    #endregion
}
