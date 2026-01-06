using Domain.Authorization.Constants;
using Domain.Identity.Enums;
using Domain.Identity.Exceptions;
using Domain.Shared.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Presentation.WebApi.Attributes;
using Presentation.WebApi.Models.Requests;
using Presentation.WebApi.Models.Responses;
using System.ComponentModel.DataAnnotations;

namespace Presentation.WebApi.Controllers.V1;

public partial class AuthController
{
    /// <summary>
    /// Gets the user's linked identities.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Information about the user's linked identities.</returns>
    /// <response code="200">Returns the user's linked identities.</response>
    /// <response code="401">Not authenticated.</response>
    [HttpGet("users/{userId:guid}/identity")]
    [RequiredPermission(PermissionIds.Api.Auth.Identity.Read.Identifier)]
    [ProducesResponseType<IdentitiesResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetIdentities(
        [FromRoute, Required, PermissionParameter(PermissionIds.Api.Auth.UserIdParameter)] Guid userId,
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

        // Get linked OAuth providers
        var linkedProviders = user.ExternalLogins
            .Select(link => new LinkedProviderInfo
            {
                Provider = link.Provider,
                DisplayName = link.DisplayName,
                Email = link.Email
            })
            .ToArray();

        // Get linked passkeys
        var passkeys = await passkeyService.ListPasskeysAsync(userId, cancellationToken);
        var linkedPasskeys = passkeys
            .Select(p => new LinkedPasskeyInfo
            {
                Id = p.Id,
                Name = p.Name,
                RegisteredAt = p.RegisteredAt
            })
            .ToArray();

        return Ok(new IdentitiesResponse
        {
            IsAnonymous = user.IsAnonymous,
            LinkedAt = user.LinkedAt,
            HasPassword = user.HasPassword,
            Email = user.Email,
            EmailConfirmed = user.EmailConfirmed,
            LinkedProviders = linkedProviders,
            LinkedPasskeys = linkedPasskeys
        });
    }

    /// <summary>
    /// Links a password to the user's account.
    /// For anonymous users, this upgrades them to a full account.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="request">The password linking request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated user information.</returns>
    /// <response code="200">Password linked successfully.</response>
    /// <response code="400">Invalid request or password already linked.</response>
    /// <response code="401">Not authenticated.</response>
    /// <response code="409">Username or email already exists.</response>
    [HttpPost("users/{userId:guid}/identity/password")]
    [RequiredPermission(PermissionIds.Api.Auth.Identity.Password.Link.Identifier)]
    [ProducesResponseType<UserInfo>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> LinkPassword(
        [FromRoute, Required, PermissionParameter(PermissionIds.Api.Auth.UserIdParameter)] Guid userId,
        [FromBody] LinkPasswordRequest request,
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

        // Check if password is already linked
        if (user.HasPassword)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Password already linked",
                Detail = "This account already has a password. Use change-password to update it."
            });
        }

        // Check if username is already taken (only if username is changing)
        if (!string.Equals(user.Username, request.Username, StringComparison.OrdinalIgnoreCase))
        {
            var existingUser = await userProfileService.GetByUsernameAsync(request.Username, cancellationToken);
            if (existingUser is not null)
            {
                return Conflict(new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "Username already exists",
                    Detail = $"The username '{request.Username}' is already taken."
                });
            }
        }

        // Check if email is already taken
        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            var existingByEmail = await userProfileService.GetByEmailAsync(request.Email, cancellationToken);
            if (existingByEmail is not null && existingByEmail.Id != user.Id)
            {
                return Conflict(new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "Email already exists",
                    Detail = $"The email '{request.Email}' is already registered."
                });
            }
        }

        try
        {
            await passwordService.LinkPasswordAsync(
                userId,
                request.Username,
                request.Password,
                request.Email,
                cancellationToken);

            // Get updated user info with inline role format
            var linkPwdUserInfo = await CreateUserInfoAsync(
                userId,
                cancellationToken);

            return Ok(linkPwdUserInfo);
        }
        catch (PasswordValidationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Link failed",
                Detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Links an email to the user's account.
    /// Email alone does not upgrade anonymous users - they need a password, OAuth, or passkey.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="request">The email linking request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated user information.</returns>
    /// <response code="200">Email linked successfully.</response>
    /// <response code="400">Invalid request.</response>
    /// <response code="401">Not authenticated.</response>
    /// <response code="409">Email already exists.</response>
    [HttpPost("users/{userId:guid}/identity/email")]
    [RequiredPermission(PermissionIds.Api.Auth.Identity.Email.Link.Identifier)]
    [ProducesResponseType<UserInfo>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> LinkEmail(
        [FromRoute, Required, PermissionParameter(PermissionIds.Api.Auth.UserIdParameter)] Guid userId,
        [FromBody] LinkEmailRequest request,
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

        // Check if email is already taken by another user
        var existingByEmail = await userProfileService.GetByEmailAsync(request.Email, cancellationToken);
        if (existingByEmail is not null && existingByEmail.Id != user.Id)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Email already exists",
                Detail = $"The email '{request.Email}' is already registered to another account."
            });
        }

        try
        {
            await userProfileService.LinkEmailAsync(userId, request.Email, cancellationToken);

            // Get updated user info with inline role format
            var linkEmailUserInfo = await CreateUserInfoAsync(
                userId,
                cancellationToken);

            return Ok(linkEmailUserInfo);
        }
        catch (DuplicateEntityException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Link failed",
                Detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Links a passkey to the user's account.
    /// For anonymous users, this upgrades them to a full account (passkeys are passwordless).
    /// </summary>
    /// <remarks>
    /// This endpoint wraps passkey registration with anonymous user upgrade logic.
    /// Call this endpoint after navigator.credentials.create() returns, passing the
    /// challengeId from the options endpoint and the JSON-serialized attestation response.
    /// </remarks>
    /// <param name="userId">The user ID.</param>
    /// <param name="request">The passkey registration request with challenge ID and attestation response.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated user information.</returns>
    /// <response code="200">Passkey linked successfully.</response>
    /// <response code="400">Invalid attestation response or expired challenge.</response>
    /// <response code="401">Not authenticated.</response>
    [HttpPost("users/{userId:guid}/identity/passkeys/link")]
    [RequiredPermission(PermissionIds.Api.Auth.Identity.Passkeys.Register.Identifier)]
    [ProducesResponseType<UserInfo>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> LinkPasskey(
        [FromRoute, Required, PermissionParameter(PermissionIds.Api.Auth.UserIdParameter)] Guid userId,
        [FromBody] PasskeyRegistrationRequest request,
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

        try
        {
            // Register the passkey
            var result = await passkeyService.VerifyRegistrationAsync(request.ChallengeId, request.AttestationResponseJson, cancellationToken);

            // If user was anonymous, upgrade them
            if (user.IsAnonymous)
            {
                await userRegistrationService.UpgradeAnonymousWithPasskeyAsync(userId, cancellationToken);
            }

            // Get updated user info with inline role format
            var linkPasskeyUserInfo = await CreateUserInfoAsync(
                userId,
                cancellationToken);

            return Ok(linkPasskeyUserInfo);
        }
        catch (Domain.Shared.Exceptions.ValidationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Passkey registration failed",
                Detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Unlinks an OAuth provider from the user's account.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="provider">The provider to unlink (e.g., "google", "github").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>No content on success.</returns>
    /// <response code="204">Provider unlinked successfully.</response>
    /// <response code="400">Cannot unlink last authentication method.</response>
    /// <response code="401">Not authenticated.</response>
    /// <response code="404">Provider not linked.</response>
    [HttpDelete("users/{userId:guid}/identity/external/{provider}")]
    [RequiredPermission(PermissionIds.Api.Auth.Identity.External.Unlink.Identifier)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UnlinkProvider(
        [FromRoute, Required, PermissionParameter(PermissionIds.Api.Auth.UserIdParameter)] Guid userId,
        string provider,
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

        // Check if provider is linked
        var link = user.ExternalLogins.FirstOrDefault(l => string.Equals(l.Provider.ToString(), provider, StringComparison.OrdinalIgnoreCase));
        if (link is null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Provider not linked",
                Detail = $"The provider '{provider}' is not linked to this account."
            });
        }

        // Check if this is the last auth method
        if (!await authMethodGuardService.CanUnlinkProviderAsync(userId, provider, cancellationToken))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Cannot unlink last authentication method",
                Detail = "You must have at least one authentication method linked to your account."
            });
        }

        // Parse provider string to enum
        if (!Enum.TryParse<ExternalLoginProvider>(provider, ignoreCase: true, out var providerEnum))
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Unknown provider",
                Detail = $"The provider '{provider}' is not a recognized OAuth provider."
            });
        }

        try
        {
            await userProfileService.UnlinkExternalLoginAsync(userId, providerEnum, cancellationToken);
            return NoContent();
        }
        catch (EntityNotFoundException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Unlink failed",
                Detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Changes the user's username.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="request">The username change request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated user information.</returns>
    /// <response code="200">Username changed successfully.</response>
    /// <response code="400">Invalid request or anonymous user.</response>
    /// <response code="401">Not authenticated.</response>
    /// <response code="409">Username already exists.</response>
    [HttpPut("users/{userId:guid}/identity/username")]
    [RequiredPermission(PermissionIds.Api.Auth.Identity.Username.Change.Identifier)]
    [ProducesResponseType<UserInfo>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ChangeUsername(
        [FromRoute, Required, PermissionParameter(PermissionIds.Api.Auth.UserIdParameter)] Guid userId,
        [FromBody] ChangeUsernameRequest request,
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

        // Anonymous users cannot change username
        if (user.IsAnonymous)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Cannot change username",
                Detail = "Anonymous users cannot change username. Link a password or OAuth first."
            });
        }

        // Check if username is already taken
        if (!string.Equals(user.Username, request.Username, StringComparison.OrdinalIgnoreCase))
        {
            var existingUser = await userProfileService.GetByUsernameAsync(request.Username, cancellationToken);
            if (existingUser is not null)
            {
                return Conflict(new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "Username already exists",
                    Detail = $"The username '{request.Username}' is already taken."
                });
            }
        }

        try
        {
            await userProfileService.ChangeUsernameAsync(userId, request.Username, cancellationToken);

            // Get updated user info with inline role format
            var changeUsernameUserInfo = await CreateUserInfoAsync(
                userId,
                cancellationToken);

            return Ok(changeUsernameUserInfo);
        }
        catch (DuplicateEntityException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Change failed",
                Detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Changes the user's email address.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="request">The email change request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated user information.</returns>
    /// <response code="200">Email changed successfully.</response>
    /// <response code="400">Invalid request.</response>
    /// <response code="401">Not authenticated.</response>
    /// <response code="409">Email already exists.</response>
    [HttpPut("users/{userId:guid}/identity/email")]
    [RequiredPermission(PermissionIds.Api.Auth.Identity.Email.Change.Identifier)]
    [ProducesResponseType<UserInfo>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ChangeEmail(
        [FromRoute, Required, PermissionParameter(PermissionIds.Api.Auth.UserIdParameter)] Guid userId,
        [FromBody] ChangeEmailRequest request,
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

        // Check if email is already taken by another user
        var existingByEmail = await userProfileService.GetByEmailAsync(request.Email, cancellationToken);
        if (existingByEmail is not null && existingByEmail.Id != user.Id)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Email already exists",
                Detail = $"The email '{request.Email}' is already registered to another account."
            });
        }

        try
        {
            await userProfileService.ChangeEmailAsync(userId, request.Email, cancellationToken);

            // Get updated user info with inline role format
            var changeEmailUserInfo = await CreateUserInfoAsync(
                userId,
                cancellationToken);

            return Ok(changeEmailUserInfo);
        }
        catch (DuplicateEntityException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Change failed",
                Detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Unlinks the email from the user's account.
    /// Email can only be unlinked if the user has a username to login with.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>No content on success.</returns>
    /// <response code="204">Email unlinked successfully.</response>
    /// <response code="400">No email is linked, or email is required for login.</response>
    /// <response code="401">Not authenticated.</response>
    [HttpDelete("users/{userId:guid}/identity/email")]
    [RequiredPermission(PermissionIds.Api.Auth.Identity.Email.Unlink.Identifier)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UnlinkEmail(
        [FromRoute, Required, PermissionParameter(PermissionIds.Api.Auth.UserIdParameter)] Guid userId,
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

        // Check if user has an email to unlink
        if (string.IsNullOrWhiteSpace(user.Email))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "No email linked",
                Detail = "No email is linked to this account."
            });
        }

        // Check if email is required for login (no username set)
        var hasUsername = !string.IsNullOrWhiteSpace(user.Username);
        
        if (!hasUsername)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Cannot unlink email",
                Detail = "Email is required for login because you have no username. Set a username first before unlinking email."
            });
        }

        try
        {
            await userProfileService.UnlinkEmailAsync(userId, cancellationToken);
            return NoContent();
        }
        catch (EntityNotFoundException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Unlink failed",
                Detail = ex.Message
            });
        }
    }
}
