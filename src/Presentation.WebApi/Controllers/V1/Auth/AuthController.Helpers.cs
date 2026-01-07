using Domain.Authorization.Constants;
using Domain.Authorization.ValueObjects;
using Application.Identity.Models;
using Application.Common.Services;
using Presentation.WebApi.Models.Responses;
using JwtClaimTypes = Domain.Identity.Constants.JwtClaimTypes;

namespace Presentation.WebApi.Controllers.V1;

public partial class AuthController
{
    #region Helper Methods

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(JwtClaimTypes.Subject);
        if (userIdClaim is not null && Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return userId;
        }
        return null;
    }

    private string? GetCurrentUsername()
    {
        return User.FindFirst(JwtClaimTypes.Name)?.Value;
    }

    private Guid? GetCurrentSessionId()
    {
        var sessionIdClaim = User.FindFirst(JwtClaimTypes.SessionId);
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
        var deviceName = DeviceInfoParser.ParseDeviceName(userAgent);

        var deviceInfo = new SessionDeviceInfo(deviceName, userAgent, ipAddress);

        var result = await userTokenService.CreateSessionWithTokensAsync(userId, deviceInfo, cancellationToken);

        return (result.AccessToken, result.RefreshToken, result.SessionId, result.ExpiresInSeconds);
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
