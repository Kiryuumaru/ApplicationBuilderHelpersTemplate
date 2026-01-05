using Domain.Authorization.Constants;
using Domain.Identity.Exceptions;
using Domain.Shared.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Presentation.WebApi.Attributes;
using Presentation.WebApi.Models.Requests;
using Presentation.WebApi.Models.Responses;
using System.ComponentModel.DataAnnotations;
using AuthenticationException = Domain.Identity.Exceptions.AuthenticationException;

namespace Presentation.WebApi.Controllers.V1;

public partial class AuthController
{
    /// <summary>
    /// Gets the 2FA setup information for the user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The shared key and authenticator URI for QR code generation.</returns>
    /// <response code="200">Returns 2FA setup information.</response>
    /// <response code="401">Not authenticated.</response>
    [HttpGet("users/{userId:guid}/2fa/setup")]
    [RequiredPermission(PermissionIds.Api.Auth._2Fa.Setup.Identifier)]
    [ProducesResponseType<TwoFactorSetupResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetTwoFactorSetup(
        [FromRoute, Required, PermissionParameter(PermissionIds.Api.Auth.UserIdParameter)] Guid userId,
        CancellationToken cancellationToken)
    {
        var setupInfo = await twoFactorService.Setup2faAsync(userId, cancellationToken);

        return Ok(new TwoFactorSetupResponse
        {
            SharedKey = setupInfo.SharedKey,
            FormattedSharedKey = setupInfo.FormattedSharedKey,
            AuthenticatorUri = setupInfo.AuthenticatorUri
        });
    }

    /// <summary>
    /// Enables two-factor authentication for the user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="request">The verification code from the authenticator app.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Recovery codes for account recovery.</returns>
    /// <response code="200">2FA enabled successfully, returns recovery codes.</response>
    /// <response code="400">Invalid verification code.</response>
    /// <response code="401">Not authenticated.</response>
    [HttpPost("users/{userId:guid}/2fa/enable")]
    [RequiredPermission(PermissionIds.Api.Auth._2Fa.Enable.Identifier)]
    [ProducesResponseType<EnableTwoFactorResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> EnableTwoFactor(
        [FromRoute, Required, PermissionParameter(PermissionIds.Api.Auth.UserIdParameter)] Guid userId,
        [FromBody] EnableTwoFactorRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var recoveryCodes = await twoFactorService.Enable2faAsync(userId, request.VerificationCode, cancellationToken);

            return Ok(new EnableTwoFactorResponse
            {
                RecoveryCodes = recoveryCodes
            });
        }
        catch (TwoFactorException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid verification code",
                Detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Disables two-factor authentication for the user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="request">The user's password to confirm the action.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Success status.</returns>
    /// <response code="204">2FA disabled successfully.</response>
    /// <response code="401">Not authenticated or invalid password.</response>
    [HttpPost("users/{userId:guid}/2fa/disable")]
    [RequiredPermission(PermissionIds.Api.Auth._2Fa.Disable.Identifier)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DisableTwoFactor(
        [FromRoute, Required, PermissionParameter(PermissionIds.Api.Auth.UserIdParameter)] Guid userId,
        [FromBody] DisableTwoFactorRequest request,
        CancellationToken cancellationToken)
    {
        // Get the user to verify password
        var user = await userProfileService.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "User not found",
                Detail = "The specified user does not exist."
            });
        }

        if (user.Username is null)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Username required",
                Detail = "The user must have a username to disable 2FA with password verification."
            });
        }

        try
        {
            // Authenticate to verify password (may throw AuthenticationException for bad password or 2FA required)
            await authenticationService.AuthenticateAsync(user.Username, request.Password, cancellationToken);
        }
        catch (AuthenticationException)
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Invalid password",
                Detail = "The password is incorrect."
            });
        }
        catch (TwoFactorRequiredException)
        {
            // 2FA required exception means the password was correct - proceed
        }

        try
        {
            await twoFactorService.Disable2faAsync(userId, cancellationToken);
            return NoContent();
        }
        catch (TwoFactorException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Cannot disable 2FA",
                Detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Completes login with a two-factor authentication code.
    /// </summary>
    /// <param name="request">The user ID and 2FA code.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>JWT tokens for the authenticated user.</returns>
    /// <response code="200">Returns JWT tokens.</response>
    /// <response code="401">Invalid 2FA code.</response>
    [HttpPost("login/2fa")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> TwoFactorLogin([FromBody] TwoFactorLoginRequest request, CancellationToken cancellationToken)
    {
        try
        {
            // Verify 2FA code first
            var isValidCode = await twoFactorService.Verify2faCodeAsync(request.UserId, request.Code, cancellationToken);
            if (!isValidCode)
            {
                return Unauthorized(new ProblemDetails
                {
                    Status = StatusCodes.Status401Unauthorized,
                    Title = "Invalid code",
                    Detail = "The 2FA code is invalid or has expired."
                });
            }

            // Get user info for session
            var userResult = await authenticationService.GetUserForSessionAsync(request.UserId, cancellationToken);
            if (!userResult.Succeeded)
            {
                return Unauthorized(new ProblemDetails
                {
                    Status = StatusCodes.Status401Unauthorized,
                    Title = "Invalid request",
                    Detail = "The user ID is invalid."
                });
            }

            var (accessToken, refreshToken, sessionId) = await CreateSessionAndTokensAsync(
                userResult.UserId!.Value,
                userResult.Username,
                userResult.Roles,
                cancellationToken);

            var permissions = await userAuthorizationService.GetEffectivePermissionsAsync(userResult.UserId!.Value, cancellationToken);

            return Ok(new AuthResponse
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresIn = AccessTokenExpirationMinutes * 60,
                User = new UserInfo
                {
                    Id = userResult.UserId!.Value,
                    Username = userResult.Username,
                    Email = null, // Session doesn't include email currently
                    Roles = userResult.Roles.ToArray(),
                    Permissions = permissions.ToArray()
                }
            });
        }
        catch (EntityNotFoundException)
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Invalid request",
                Detail = "The user ID is invalid or the 2FA session has expired."
            });
        }
    }

    /// <summary>
    /// Generates new recovery codes for two-factor authentication.
    /// </summary>
    /// <remarks>
    /// This will invalidate any previously generated recovery codes.
    /// Requires 2FA to be enabled on the account.
    /// </remarks>
    /// <param name="userId">The user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of 10 recovery codes.</returns>
    /// <response code="200">Returns the newly generated recovery codes.</response>
    /// <response code="400">2FA is not enabled on this account.</response>
    /// <response code="401">Not authenticated.</response>
    [HttpPost("users/{userId:guid}/2fa/recovery-codes")]
    [RequiredPermission(PermissionIds.Api.Auth._2Fa.RegenerateCodes.Identifier)]
    [ProducesResponseType<RecoveryCodesResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GenerateRecoveryCodes(
        [FromRoute, Required, PermissionParameter(PermissionIds.Api.Auth.UserIdParameter)] Guid userId,
        CancellationToken cancellationToken)
    {
        try
        {
            var recoveryCodes = await twoFactorService.GenerateRecoveryCodesAsync(userId, cancellationToken);
            return Ok(new RecoveryCodesResponse(recoveryCodes));
        }
        catch (TwoFactorException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "2FA not enabled",
                Detail = ex.Message
            });
        }
    }
}
