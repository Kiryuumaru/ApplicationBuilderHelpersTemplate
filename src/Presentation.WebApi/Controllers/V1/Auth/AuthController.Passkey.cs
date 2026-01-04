using Domain.Authorization.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Presentation.WebApi.Attributes;
using Presentation.WebApi.Models.Requests;
using Presentation.WebApi.Models.Responses;
using System.ComponentModel.DataAnnotations;
using System.Security.Authentication;

namespace Presentation.WebApi.Controllers.V1;

public partial class AuthController
{
    #region Passkey (WebAuthn) Endpoints

    /// <summary>
    /// Gets the options needed to create a new passkey for the user.
    /// </summary>
    /// <remarks>
    /// Call this endpoint first to get WebAuthn options, then pass the options to
    /// navigator.credentials.create() in the browser. Use the returned challengeId
    /// when calling the registration endpoint.
    /// </remarks>
    /// <param name="userId">The user ID.</param>
    /// <param name="request">The passkey registration options request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Challenge ID and WebAuthn options JSON.</returns>
    /// <response code="200">Returns the challenge ID and options for creating a passkey.</response>
    /// <response code="401">Not authenticated.</response>
    [HttpPost("users/{userId:guid}/identity/passkeys/options")]
    [RequiredPermission(PermissionIds.Api.Auth.Identity.Passkeys.Register.Identifier)]
    [ProducesResponseType<PasskeyRegistrationOptionsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetPasskeyCreationOptions(
        [FromRoute, Required, PermissionParameter(PermissionIds.Api.Auth.UserIdParameter)] Guid userId,
        [FromBody] PasskeyRegistrationOptionsRequest request,
        CancellationToken cancellationToken)
    {
        var options = await passkeyService.GetRegistrationOptionsAsync(userId, request.CredentialName, cancellationToken);

        return Ok(new PasskeyRegistrationOptionsResponse(options.ChallengeId, options.OptionsJson));
    }

    /// <summary>
    /// Registers a new passkey for the user.
    /// </summary>
    /// <remarks>
    /// Call this endpoint after navigator.credentials.create() returns, passing the
    /// challengeId from the options endpoint and the JSON-serialized attestation response.
    /// </remarks>
    /// <param name="userId">The user ID.</param>
    /// <param name="request">The passkey registration request with challenge ID and attestation response.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The registered passkey information.</returns>
    /// <response code="201">Passkey registered successfully.</response>
    /// <response code="400">Invalid attestation response or expired challenge.</response>
    /// <response code="401">Not authenticated.</response>
    [HttpPost("users/{userId:guid}/identity/passkeys")]
    [RequiredPermission(PermissionIds.Api.Auth.Identity.Passkeys.Register.Identifier)]
    [ProducesResponseType<PasskeyRegistrationResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RegisterPasskey(
        [FromRoute, Required, PermissionParameter(PermissionIds.Api.Auth.UserIdParameter)] Guid userId,
        [FromBody] PasskeyRegistrationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await passkeyService.VerifyRegistrationAsync(request.ChallengeId, request.AttestationResponseJson, cancellationToken);

            return StatusCode(StatusCodes.Status201Created, new PasskeyRegistrationResponse(result.CredentialId, result.CredentialName));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid passkey registration",
                Detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Gets the options needed to authenticate with a passkey.
    /// </summary>
    /// <remarks>
    /// Call this endpoint first to get WebAuthn options, then pass the options to
    /// navigator.credentials.get() in the browser. Use the returned challengeId
    /// when calling the login endpoint.
    /// </remarks>
    /// <param name="request">Optional username for user-specific credential filtering.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Challenge ID and WebAuthn options JSON.</returns>
    /// <response code="200">Returns the challenge ID and options for passkey authentication.</response>
    [HttpPost("login/passkey/options")]
    [AllowAnonymous]
    [ProducesResponseType<PasskeyLoginOptionsResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPasskeyRequestOptions([FromBody] PasskeyLoginOptionsRequest? request, CancellationToken cancellationToken)
    {
        var options = await passkeyService.GetLoginOptionsAsync(request?.Username, cancellationToken);

        return Ok(new PasskeyLoginOptionsResponse(options.ChallengeId, options.OptionsJson));
    }

    /// <summary>
    /// Authenticates with a passkey and returns JWT tokens.
    /// </summary>
    /// <remarks>
    /// Call this endpoint after navigator.credentials.get() returns, passing the
    /// challengeId from the options endpoint and the JSON-serialized assertion response.
    /// </remarks>
    /// <param name="request">The passkey login request with challenge ID and assertion response.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>JWT access and refresh tokens along with user information.</returns>
    /// <response code="200">Returns the JWT tokens and user information.</response>
    /// <response code="400">Invalid assertion response or expired challenge.</response>
    /// <response code="401">Authentication failed.</response>
    [HttpPost("login/passkey")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> PasskeyLogin([FromBody] PasskeyLoginRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await passkeyService.VerifyLoginAsync(request.ChallengeId, request.AssertionResponseJson, cancellationToken);

            var userSession = result.Session;
            var (accessToken, refreshToken, sessionId) = await CreateSessionAndTokensAsync(
                userSession.UserId,
                userSession.Username,
                userSession.Roles,
                cancellationToken);

            var permissions = await userAuthorizationService.GetEffectivePermissionsAsync(userSession.UserId, cancellationToken);

            return Ok(new AuthResponse
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresIn = AccessTokenExpirationMinutes * 60,
                User = new UserInfo
                {
                    Id = userSession.UserId,
                    Username = userSession.Username,
                    Email = null, // Session doesn't include email currently
                    Roles = userSession.Roles.ToArray(),
                    Permissions = permissions.ToArray()
                }
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid passkey authentication",
                Detail = ex.Message
            });
        }
        catch (AuthenticationException)
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Authentication failed",
                Detail = "Passkey authentication failed."
            });
        }
    }

    /// <summary>
    /// Lists all passkeys for the user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of registered passkeys.</returns>
    /// <response code="200">Returns the list of passkeys.</response>
    /// <response code="401">Not authenticated.</response>
    [HttpGet("users/{userId:guid}/identity/passkeys")]
    [RequiredPermission(PermissionIds.Api.Auth.Identity.Passkeys.List.Identifier)]
    [ProducesResponseType<PasskeyListResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ListPasskeys(
        [FromRoute, Required, PermissionParameter(PermissionIds.Api.Auth.UserIdParameter)] Guid userId,
        CancellationToken cancellationToken)
    {
        var passkeys = await passkeyService.ListPasskeysAsync(userId, cancellationToken);

        var response = new PasskeyListResponse(
            passkeys.Select(p => new PasskeyInfoResponse(p.Id, p.Name, p.RegisteredAt, p.LastUsedAt)).ToList()
        );

        return Ok(response);
    }

    /// <summary>
    /// Renames a passkey.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="credentialId">The passkey credential ID.</param>
    /// <param name="request">The rename request with new name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>No content on success.</returns>
    /// <response code="204">Passkey renamed successfully.</response>
    /// <response code="401">Not authenticated.</response>
    /// <response code="404">Passkey not found.</response>
    [HttpPut("users/{userId:guid}/identity/passkeys/{credentialId:guid}")]
    [RequiredPermission(PermissionIds.Api.Auth.Identity.Passkeys.Rename.Identifier)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RenamePasskey(
        [FromRoute, Required, PermissionParameter(PermissionIds.Api.Auth.UserIdParameter)] Guid userId,
        Guid credentialId,
        [FromBody] PasskeyRenameRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await passkeyService.RenamePasskeyAsync(userId, credentialId, request.Name, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Passkey not found",
                Detail = "The specified passkey was not found or does not belong to the specified user."
            });
        }
    }

    /// <summary>
    /// Revokes (deletes) a passkey.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="credentialId">The passkey credential ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>No content on success.</returns>
    /// <response code="204">Passkey revoked successfully.</response>
    /// <response code="400">Cannot unlink last authentication method.</response>
    /// <response code="401">Not authenticated.</response>
    /// <response code="404">Passkey not found.</response>
    [HttpDelete("users/{userId:guid}/identity/passkeys/{credentialId:guid}")]
    [RequiredPermission(PermissionIds.Api.Auth.Identity.Passkeys.Delete.Identifier)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokePasskey(
        [FromRoute, Required, PermissionParameter(PermissionIds.Api.Auth.UserIdParameter)] Guid userId,
        Guid credentialId,
        CancellationToken cancellationToken)
    {
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

        // Check if this is the last auth method
        var oauthProviders = user.ExternalLogins.Count;
        var passkeys = await passkeyService.ListPasskeysAsync(userId, cancellationToken);

        // If this passkey is the only auth method (no password, no oauth, only this passkey), prevent deletion
        if (!user.HasPassword && oauthProviders == 0 && passkeys.Count <= 1)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Cannot unlink last authentication method",
                Detail = "You must have at least one authentication method linked to your account."
            });
        }

        try
        {
            await passkeyService.RevokePasskeyAsync(userId, credentialId, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Passkey not found",
                Detail = "The specified passkey was not found or does not belong to the specified user."
            });
        }
    }

    #endregion
}
