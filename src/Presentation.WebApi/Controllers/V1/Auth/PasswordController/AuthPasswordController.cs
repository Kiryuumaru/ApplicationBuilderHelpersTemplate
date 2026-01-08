using Application.Common.Interfaces;
using Application.Identity.Interfaces;
using Asp.Versioning;
using Domain.Authorization.Constants;
using Domain.Identity.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Presentation.WebApi.Attributes;
using Presentation.WebApi.Controllers.V1.Auth.PasswordController.Requests;
using System.ComponentModel.DataAnnotations;

namespace Presentation.WebApi.Controllers.V1.Auth.PasswordController;

/// <summary>
/// Controller for password-based identity operations.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{v:apiVersion}/auth")]
[Produces("application/json")]
[Tags("Authentication")]
public sealed class AuthPasswordController(
    IPasswordService passwordService,
    IEmailService emailService,
    IFrontendUrlBuilder frontendUrlBuilder)
    : ControllerBase
{
    /// <summary>
    /// Changes the user's password.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="request">The password change details including current and new password.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Success status.</returns>
    /// <response code="204">Password changed successfully.</response>
    /// <response code="400">New password doesn't meet requirements.</response>
    /// <response code="401">Not authenticated or current password is incorrect.</response>
    [HttpPut("users/{userId:guid}/identity/password")]
    [RequiredPermission(PermissionIds.Api.Auth.Identity.Password.Change.Identifier)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword(
        [FromRoute, Required, PermissionParameter(PermissionIds.Api.Auth.UserIdParameter)] Guid userId,
        [FromBody] ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        await passwordService.ChangePasswordAsync(userId, request.CurrentPassword, request.NewPassword, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Initiates a password reset by sending a reset email.
    /// </summary>
    /// <param name="request">The email address to send reset link to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Success status (always returns success for security).</returns>
    /// <remarks>
    /// For security reasons, this endpoint always returns success even if the email doesn't exist.
    /// </remarks>
    /// <response code="204">Password reset initiated (if email exists).</response>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        var token = await passwordService.GeneratePasswordResetTokenAsync(request.Email, cancellationToken);

        if (token is not null)
        {
            var resetLink = frontendUrlBuilder.BuildPasswordResetUrl(request.Email, token);
            await emailService.SendPasswordResetLinkAsync(request.Email, resetLink, cancellationToken);
        }

        return NoContent();
    }

    /// <summary>
    /// Resets the user's password using a reset token.
    /// </summary>
    /// <param name="request">The password reset details including token and new password.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Success status.</returns>
    /// <response code="204">Password reset successfully.</response>
    /// <response code="400">Invalid token or password requirements not met.</response>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var success = await passwordService.ResetPasswordWithTokenAsync(
            request.Email,
            request.Token,
            request.NewPassword,
            cancellationToken);

        if (!success)
        {
            throw new PasswordResetTokenInvalidException();
        }

        return NoContent();
    }
}
