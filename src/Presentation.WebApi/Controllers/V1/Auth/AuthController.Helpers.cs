using Domain.Authorization.Constants;
using Domain.Authorization.ValueObjects;
using Application.Identity.Models;
using Microsoft.AspNetCore.Mvc;
using Presentation.WebApi.Models.Responses;
using System.Security.Claims;

namespace Presentation.WebApi.Controllers.V1;

public partial class AuthController
{
    #region Helper Methods

    private Guid? GetCurrentUserId()
    {
        // Support both short ("nameid") and verbose (ClaimTypes.NameIdentifier) claim types
        var userIdClaim = User.FindFirst("nameid") ?? User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
        if (userIdClaim is not null && Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return userId;
        }
        return null;
    }

    private string? GetCurrentUsername()
    {
        // Support both short ("name") and verbose (ClaimTypes.Name) claim types
        return User.FindFirst("name")?.Value ?? User.FindFirst(ClaimTypes.Name)?.Value;
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
    /// Extracts device info from HTTP context and delegates to IUserTokenService.
    /// </summary>
    /// <param name="userId">The user's ID.</param>
    /// <param name="username">The user's username (null for anonymous users). Not used - kept for signature compatibility.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Access token, refresh token, session ID, and expiration in seconds.</returns>
    private async Task<(string AccessToken, string RefreshToken, Guid SessionId, int ExpiresInSeconds)> CreateSessionAndTokensAsync(
        Guid userId,
        string? username,
        CancellationToken cancellationToken)
    {
        // Extract device info from request headers
        var userAgent = Request.Headers.UserAgent.ToString();
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var deviceName = ParseDeviceName(userAgent);

        var deviceInfo = new SessionDeviceInfo(deviceName, userAgent, ipAddress);

        var result = await userTokenService.CreateSessionWithTokensAsync(userId, deviceInfo, cancellationToken);

        return (result.AccessToken, result.RefreshToken, result.SessionId, result.ExpiresInSeconds);
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

    /// <summary>
    /// Creates a UserInfo response model with inline role format.
    /// Roles are returned in the format "USER;roleUserId=abc123" for consistency with JWT claims.
    /// Uses authorization data from the combined GetAuthorizationDataAsync call for efficiency.
    /// </summary>
    private async Task<UserInfo> CreateUserInfoAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        // Single DB call to get all authorization data
        var authData = await userAuthorizationService.GetAuthorizationDataAsync(userId, cancellationToken);

        return new UserInfo
        {
            Id = authData.UserId,
            Username = authData.Username,
            Email = authData.Email,
            Roles = authData.FormattedRoles,
            Permissions = authData.EffectivePermissions,
            IsAnonymous = authData.IsAnonymous
        };
    }

    #endregion
}
