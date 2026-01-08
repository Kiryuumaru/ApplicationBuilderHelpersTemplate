using Application.Identity.Interfaces;
using Asp.Versioning;
using Domain.Authorization.Constants;
using Domain.Identity.Exceptions;
using Domain.Shared.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Presentation.WebApi.Attributes;
using Presentation.WebApi.Controllers.V1.Auth.TwoFactorController.Requests;
using Presentation.WebApi.Controllers.V1.Auth.TwoFactorController.Responses;
using SharedResponses = Presentation.WebApi.Controllers.V1.Auth.Shared.Responses;
using System.ComponentModel.DataAnnotations;

namespace Presentation.WebApi.Controllers.V1.Auth.TwoFactorController;

/// <summary>
/// Controller for two-factor authentication (2FA) endpoints.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{v:apiVersion}/auth")]
[Produces("application/json")]
[Tags("Authentication")]
public sealed class AuthTwoFactorController(
    ITwoFactorService twoFactorService,
    IUserProfileService userProfileService,
    IAuthenticationService authenticationService,
    IUserAuthorizationService userAuthorizationService) : ControllerBase
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
        var recoveryCodes = await twoFactorService.Enable2faAsync(userId, request.VerificationCode, cancellationToken);

        return Ok(new EnableTwoFactorResponse
        {
            RecoveryCodes = recoveryCodes
        });
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
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DisableTwoFactor(
        [FromRoute, Required, PermissionParameter(PermissionIds.Api.Auth.UserIdParameter)] Guid userId,
        [FromBody] DisableTwoFactorRequest request,
        CancellationToken cancellationToken)
    {
        var user = await userProfileService.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            throw new EntityNotFoundException("User", userId.ToString());
        }

        if (user.Username is null)
        {
            throw new Domain.Shared.Exceptions.ValidationException(
                propertyName: "Username",
                message: "The user must have a username to disable 2FA with password verification.");
        }

        var validationResult = await authenticationService.ValidateCredentialsAsync(user.Username, request.Password, cancellationToken);
        if (!validationResult.Succeeded && !validationResult.RequiresTwoFactor)
        {
            throw new InvalidPasswordException("The password is incorrect.");
        }

        await twoFactorService.Disable2faAsync(userId, cancellationToken);
        return NoContent();
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
    [ProducesResponseType<SharedResponses.AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> TwoFactorLogin([FromBody] TwoFactorLoginRequest request, CancellationToken cancellationToken)
    {
        var userSession = await authenticationService.Complete2faAuthenticationAsync(request.UserId, request.Code, cancellationToken);

        var userInfo = await CreateUserInfoAsync(userSession.UserId, cancellationToken);

        return Ok(new SharedResponses.AuthResponse
        {
            AccessToken = userSession.AccessToken,
            RefreshToken = userSession.RefreshToken,
            ExpiresIn = (int)(userSession.ExpiresAt - userSession.IssuedAt).TotalSeconds,
            User = userInfo
        });
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
        var recoveryCodes = await twoFactorService.GenerateRecoveryCodesAsync(userId, cancellationToken);
        return Ok(new RecoveryCodesResponse(recoveryCodes));
    }

    private async Task<SharedResponses.UserInfo> CreateUserInfoAsync(Guid userId, CancellationToken cancellationToken)
    {
        var authData = await userAuthorizationService.GetAuthorizationDataAsync(userId, cancellationToken);

        return new SharedResponses.UserInfo
        {
            Id = authData.UserId,
            Username = authData.Username,
            Email = authData.Email,
            Roles = authData.FormattedRoles,
            Permissions = authData.EffectivePermissions,
            IsAnonymous = authData.IsAnonymous
        };
    }
}
