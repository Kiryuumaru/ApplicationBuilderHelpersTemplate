using Domain.Authorization.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Presentation.WebApi.Models.Requests;
using Presentation.WebApi.Models.Responses;
using System.Security.Claims;

namespace Presentation.WebApi.Controllers.V1;

public partial class AuthController
{
    /// <summary>
    /// Refreshes an expired access token using a valid refresh token.
    /// </summary>
    /// <param name="request">The refresh token.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>New JWT access and refresh tokens.</returns>
    /// <response code="200">Returns new JWT tokens.</response>
    /// <response code="401">Invalid or expired refresh token.</response>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var principal = await permissionService.ValidateTokenAsync(request.RefreshToken, cancellationToken);
        if (principal is null)
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Invalid token",
                Detail = "The refresh token is invalid or has expired."
            });
        }

        // Extract user ID first - needed for permission check
        var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier) ?? principal.FindFirst("sub");
        if (userIdClaim is null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Invalid token",
                Detail = "The refresh token does not contain valid user information."
            });
        }

        // SECURITY: Verify this token has the refresh permission
        // Refresh tokens are granted ONLY api:auth:refresh;userId={userId}
        // Access tokens have deny;api:auth:refresh so they will fail this check
        var refreshPermission = PermissionIds.Api.Auth.Refresh.Permission.WithUserId(userId.ToString());
        if (!permissionService.HasPermission(principal, refreshPermission))
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Invalid token type",
                Detail = "The provided token does not have refresh permission."
            });
        }

        // Extract session ID from the refresh token
        var sessionIdClaim = principal.FindFirst(SessionIdClaimType);
        if (sessionIdClaim is null || !Guid.TryParse(sessionIdClaim.Value, out var sessionId))
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Invalid token",
                Detail = "The refresh token does not contain a valid session."
            });
        }

        // Validate the session exists and is not revoked
        var tokenHash = HashToken(request.RefreshToken);
        var loginSession = await sessionService.ValidateSessionAsync(sessionId, tokenHash, cancellationToken);
        if (loginSession is null)
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Session revoked",
                Detail = "This session has been revoked or has expired."
            });
        }

        // Fetch fresh user info by userId (not username, since username can change)
        var user = await userProfileService.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "User not found",
                Detail = "The user associated with this token no longer exists."
            });
        }

        // Get fresh permissions by re-building effective permissions
        var effectivePermissions = await userAuthorizationService.GetEffectivePermissionsAsync(userId, cancellationToken);

        // Generate new tokens - refresh token rotation (use current username from DB)
        var currentUsername = user.Username ?? userId.ToString();
        var newRefreshToken = await GenerateRefreshTokenAsync(userId, currentUsername, sessionId, cancellationToken);
        var newTokenHash = HashToken(newRefreshToken);
        var newExpiresAt = DateTimeOffset.UtcNow.AddDays(RefreshTokenExpirationDays);
        
        // Rotate the refresh token in the session
        await sessionService.UpdateRefreshTokenAsync(sessionId, newTokenHash, newExpiresAt, cancellationToken);

        var accessToken = await GenerateAccessTokenForUserAsync(userId, currentUsername, effectivePermissions.ToList(), sessionId, cancellationToken);

        return Ok(new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = newRefreshToken,
            ExpiresIn = AccessTokenExpirationMinutes * 60,
            User = new UserInfo
            {
                Id = userId,
                Username = currentUsername,
                Email = user.Email,
                Roles = user.Roles.ToArray(),
                Permissions = effectivePermissions.ToArray()
            }
        });
    }
}
