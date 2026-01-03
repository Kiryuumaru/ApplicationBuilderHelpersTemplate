using Domain.Authorization.Constants;
using Domain.Authorization.ValueObjects;
using Domain.Identity.Models;
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
    private async Task<(string AccessToken, string RefreshToken, LoginSession Session)> CreateSessionAndTokensAsync(
        UserSession userSession,
        CancellationToken cancellationToken)
    {
        // Generate a temporary refresh token to get its hash
        var sessionId = Guid.NewGuid();
        var refreshToken = await GenerateRefreshTokenAsync(userSession.UserId, userSession.Username, sessionId, cancellationToken);
        var tokenHash = HashToken(refreshToken);

        // Extract device info from request headers
        var userAgent = Request.Headers.UserAgent.ToString();
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var deviceName = ParseDeviceName(userAgent);

        // Create the login session
        var loginSession = LoginSession.Create(
            userSession.UserId,
            tokenHash,
            DateTimeOffset.UtcNow.AddDays(RefreshTokenExpirationDays),
            deviceName,
            userAgent,
            ipAddress);

        // Override the generated session ID to match
        // We need to reconstruct with the known session ID
        loginSession = LoginSession.Reconstruct(
            sessionId,
            loginSession.UserId,
            loginSession.RefreshTokenHash,
            loginSession.DeviceName,
            loginSession.UserAgent,
            loginSession.IpAddress,
            loginSession.CreatedAt,
            loginSession.LastUsedAt,
            loginSession.ExpiresAt,
            loginSession.IsRevoked,
            loginSession.RevokedAt);

        await sessionStore.CreateAsync(loginSession, cancellationToken);

        var accessToken = await GenerateAccessTokenAsync(userSession, sessionId, cancellationToken);

        return (accessToken, refreshToken, loginSession);
    }

    /// <summary>
    /// Generates an access token for the given session with all user permissions.
    /// Access tokens explicitly DENY the refresh permission - they cannot be used to refresh.
    /// </summary>
    private async Task<string> GenerateAccessTokenAsync(UserSession session, Guid loginSessionId, CancellationToken cancellationToken)
    {
        var additionalClaims = new List<Claim>
        {
            new(SessionIdClaimType, loginSessionId.ToString())
        };

        // Add role claims
        foreach (var role in session.RoleCodes)
        {
            additionalClaims.Add(new Claim(ClaimTypes.Role, role));
        }

        // Build scopes with explicit deny for refresh permission
        var scopes = new List<ScopeDirective>();
        
        // Use new scope-based token generation if session has scope directives
        if (session.Scope.Count > 0)
        {
            scopes.AddRange(session.Scope);
        }
        else
        {
            // Fall back to legacy permission identifiers - convert to ScopeDirective
            foreach (var permissionId in session.PermissionIdentifiers)
            {
                scopes.Add(ScopeDirective.Allow(permissionId));
            }
        }
        
        // SECURITY: Explicitly deny refresh permission on access tokens
        // This ensures access tokens cannot be used to call the refresh endpoint
        scopes.Add(ScopeDirective.Parse(PermissionIds.Api.Auth.Refresh.Deny()));

        return await permissionService.GenerateTokenWithScopeAsync(
            session.UserId.ToString(),
            session.Username ?? string.Empty,
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

        // Build scopes with explicit deny for refresh permission - convert permission strings to Allow directives
        var scopes = permissions.Select(p => ScopeDirective.Allow(p)).ToList();
        
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
