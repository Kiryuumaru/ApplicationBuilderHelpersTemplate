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
        if (!await permissionService.HasPermissionAsync(principal, refreshPermission, cancellationToken))
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
        // Hash the token for comparison with stored hash
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

        // Rotate tokens via service - generates new tokens and updates session atomically
        var result = await userTokenService.RotateTokensAsync(sessionId, cancellationToken);

        var refreshUserInfo = await CreateUserInfoAsync(userId, cancellationToken);

        return Ok(new AuthResponse
        {
            AccessToken = result.AccessToken,
            RefreshToken = result.RefreshToken,
            ExpiresIn = result.ExpiresInSeconds,
            User = refreshUserInfo
        });
    }

    /// <summary>
    /// Hashes a token for secure storage comparison.
    /// </summary>
    private static string HashToken(string token)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(token);
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }
}
