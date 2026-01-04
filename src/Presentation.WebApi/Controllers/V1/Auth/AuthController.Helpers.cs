using Domain.Authorization.Constants;
using Domain.Authorization.ValueObjects;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Security.Cryptography;

namespace Presentation.WebApi.Controllers.V1;

public partial class AuthController
{
    #region Helper Methods

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
        if (userIdClaim is not null && Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return userId;
        }
        return null;
    }

    private string? GetCurrentUsername()
    {
        return User.FindFirst(ClaimTypes.Name)?.Value;
    }

    private static ProblemDetails CreateUnauthorizedProblem()
    {
        return new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = "Authentication required",
            Detail = "You must be logged in to perform this action."
        };
    }

    private Guid? GetCurrentSessionId()
    {
        var sessionIdClaim = User.FindFirst(SessionIdClaimType);
        if (sessionIdClaim is not null && Guid.TryParse(sessionIdClaim.Value, out var sessionId))
        {
            return sessionId;
        }
        return null;
    }

    #endregion

    #region Token Generation Helpers

    /// <summary>
    /// Creates a new login session and generates access and refresh tokens.
    /// </summary>
    /// <param name="userId">The user's ID.</param>
    /// <param name="username">The user's username (null for anonymous users).</param>
    /// <param name="roles">The user's role codes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Access token, refresh token, and session ID.</returns>
    private async Task<(string AccessToken, string RefreshToken, Guid SessionId)> CreateSessionAndTokensAsync(
        Guid userId,
        string? username,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken)
    {
        // Generate a temporary refresh token to get its hash
        var sessionId = Guid.NewGuid();
        var refreshToken = await GenerateRefreshTokenAsync(userId, username, sessionId, cancellationToken);
        var tokenHash = HashToken(refreshToken);

        // Extract device info from request headers
        var userAgent = Request.Headers.UserAgent.ToString();
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var deviceName = ParseDeviceName(userAgent);

        // Create the login session via service
        await sessionService.CreateSessionAsync(
            userId,
            tokenHash,
            DateTimeOffset.UtcNow.AddDays(RefreshTokenExpirationDays),
            deviceName,
            userAgent,
            ipAddress,
            sessionId,
            cancellationToken);

        var accessToken = await GenerateAccessTokenForSessionAsync(userId, username, roles, sessionId, cancellationToken);

        return (accessToken, refreshToken, sessionId);
    }

    /// <summary>
    /// Generates an access token for a new session with all user permissions.
    /// Access tokens explicitly DENY the refresh permission - they cannot be used to refresh.
    /// </summary>
    private async Task<string> GenerateAccessTokenForSessionAsync(
        Guid userId,
        string? username,
        IReadOnlyCollection<string> roles,
        Guid loginSessionId,
        CancellationToken cancellationToken)
    {
        var additionalClaims = new List<Claim>
        {
            new(SessionIdClaimType, loginSessionId.ToString())
        };

        // Add role claims
        foreach (var role in roles)
        {
            additionalClaims.Add(new Claim(ClaimTypes.Role, role));
        }

        // Get permissions for the user from role service
        // GetEffectivePermissionsAsync returns scope directive strings (e.g., "allow;_read;userId=xxx")
        var permissions = await userAuthorizationService.GetEffectivePermissionsAsync(userId, cancellationToken);
        
        // Parse the scope directive strings - they're already in directive format
        var scopes = permissions.Select(ScopeDirective.Parse).ToList();
        
        // SECURITY: Explicitly deny refresh permission on access tokens
        // This ensures access tokens cannot be used to call the refresh endpoint
        scopes.Add(ScopeDirective.Parse(PermissionIds.Api.Auth.Refresh.Deny()));

        return await permissionService.GenerateTokenWithScopeAsync(
            userId.ToString(),
            username ?? string.Empty,
            scopes,
            additionalClaims,
            DateTimeOffset.UtcNow.AddMinutes(AccessTokenExpirationMinutes),
            cancellationToken);
    }

    /// <summary>
    /// Generates an access token for the given user with specified permissions.
    /// Access tokens explicitly DENY the refresh permission - they cannot be used to refresh.
    /// </summary>
    private async Task<string> GenerateAccessTokenForUserAsync(Guid userId, string username, IEnumerable<string> permissions, Guid loginSessionId, CancellationToken cancellationToken)
    {
        var additionalClaims = new List<Claim>
        {
            new(SessionIdClaimType, loginSessionId.ToString())
        };

        // Parse scope directive strings - they're already in directive format (e.g., "allow;_read;userId=xxx")
        var scopes = permissions.Select(ScopeDirective.Parse).ToList();
        
        // SECURITY: Explicitly deny refresh permission on access tokens
        scopes.Add(ScopeDirective.Parse(PermissionIds.Api.Auth.Refresh.Deny()));

        return await permissionService.GenerateTokenWithScopeAsync(
            userId.ToString(),
            username,
            scopes,
            additionalClaims,
            DateTimeOffset.UtcNow.AddMinutes(AccessTokenExpirationMinutes),
            cancellationToken);
    }

    /// <summary>
    /// Generates a refresh token for the user with session ID.
    /// Refresh tokens ONLY have the api:auth:refresh permission - they cannot access any other endpoint.
    /// </summary>
    private async Task<string> GenerateRefreshTokenAsync(Guid userId, string? username, Guid sessionId, CancellationToken cancellationToken)
    {
        var refreshClaims = new List<Claim>
        {
            new(SessionIdClaimType, sessionId.ToString())
        };

        // Refresh token only has permission to refresh - nothing else
        // Use ScopeDirective.Parse to convert the scope directive string
        var refreshScopes = new[]
        {
            ScopeDirective.Parse(PermissionIds.Api.Auth.Refresh.WithUserId(userId.ToString()).Allow())
        };
        
        return await permissionService.GenerateTokenWithScopeAsync(
            userId.ToString(),
            username ?? string.Empty,
            refreshScopes,
            additionalClaims: refreshClaims,
            DateTimeOffset.UtcNow.AddDays(RefreshTokenExpirationDays),
            cancellationToken);
    }

    /// <summary>
    /// Hashes a token for secure storage.
    /// </summary>
    private static string HashToken(string token)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(token);
        var hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }

    /// <summary>
    /// Parses a user-friendly device name from the User-Agent string.
    /// </summary>
    private static string? ParseDeviceName(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            return null;
        }

        // Simple parsing - in production, use a proper UA parser library
        if (userAgent.Contains("Windows", StringComparison.OrdinalIgnoreCase))
        {
            if (userAgent.Contains("Chrome", StringComparison.OrdinalIgnoreCase))
                return "Chrome on Windows";
            if (userAgent.Contains("Firefox", StringComparison.OrdinalIgnoreCase))
                return "Firefox on Windows";
            if (userAgent.Contains("Edge", StringComparison.OrdinalIgnoreCase))
                return "Edge on Windows";
            return "Windows";
        }
        if (userAgent.Contains("Mac", StringComparison.OrdinalIgnoreCase))
        {
            if (userAgent.Contains("Chrome", StringComparison.OrdinalIgnoreCase))
                return "Chrome on Mac";
            if (userAgent.Contains("Safari", StringComparison.OrdinalIgnoreCase))
                return "Safari on Mac";
            if (userAgent.Contains("Firefox", StringComparison.OrdinalIgnoreCase))
                return "Firefox on Mac";
            return "Mac";
        }
        if (userAgent.Contains("Linux", StringComparison.OrdinalIgnoreCase))
        {
            if (userAgent.Contains("Chrome", StringComparison.OrdinalIgnoreCase))
                return "Chrome on Linux";
            if (userAgent.Contains("Firefox", StringComparison.OrdinalIgnoreCase))
                return "Firefox on Linux";
            return "Linux";
        }
        if (userAgent.Contains("iPhone", StringComparison.OrdinalIgnoreCase))
            return "iPhone";
        if (userAgent.Contains("iPad", StringComparison.OrdinalIgnoreCase))
            return "iPad";
        if (userAgent.Contains("Android", StringComparison.OrdinalIgnoreCase))
            return "Android";

        return "Unknown Device";
    }

    #endregion
}
