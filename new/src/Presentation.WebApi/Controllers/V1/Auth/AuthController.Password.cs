using Application.Identity.Interfaces;
using Domain.Authorization.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Presentation.WebApi.Attributes;
using Presentation.WebApi.Models.Requests;
using System.ComponentModel.DataAnnotations;
using System.Security.Authentication;
using System.Security.Claims;

namespace Presentation.WebApi.Controllers.V1;

public partial class AuthController
{
    /// <summary>
    /// Changes the user's password.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="request">The password change details including current and new password.</param>
    /// <param name="identityService">The identity service.</param>
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
        [FromServices] IIdentityService identityService,
        CancellationToken cancellationToken)
    {
        try
        {
            await identityService.ChangePasswordAsync(userId, request.CurrentPassword, request.NewPassword, cancellationToken);
            return NoContent();
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Invalid password",
                Detail = "The current password is incorrect."
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Password change failed",
                Detail = ex.Message
            });
        }
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
        var token = await identityService.GeneratePasswordResetTokenAsync(request.Email, cancellationToken);
        
        if (token is not null)
        {
            // URL-encode the token for safe transmission
            var encodedToken = System.Text.Encodings.Web.UrlEncoder.Default.Encode(token);
            
            // In production, use a proper base URL from configuration
            var resetLink = $"{Request.Scheme}://{Request.Host}/reset-password?email={Uri.EscapeDataString(request.Email)}&token={encodedToken}";
            
            await emailService.SendPasswordResetLinkAsync(request.Email, resetLink, cancellationToken);
        }
        
        // Always return success for security (prevents email enumeration)
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
        var success = await identityService.ResetPasswordWithTokenAsync(
            request.Email, 
            request.Token, 
            request.NewPassword, 
            cancellationToken);
        
        if (!success)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Password reset failed",
                Detail = "The password reset token is invalid or has expired."
            });
        }
        
        return NoContent();
    }
}
