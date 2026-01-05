#if DEBUG
using Application.Authorization.Interfaces;
using Application.Identity.Interfaces;
using Application.Identity.Models;
using Asp.Versioning;
using Domain.Authorization.Constants;
using Domain.Identity.Exceptions;
using Domain.Shared.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Presentation.WebApi.Models.Requests;

namespace Presentation.WebApi.Controllers.V1;

/// <summary>
/// Debug-only controller for testing purposes.
/// This controller is only available in DEBUG builds.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{v:apiVersion}/debug")]
[Produces("application/json")]
[Tags("Debug")]
public class DebugController(
    IUserProfileService userProfileService,
    IUserAuthorizationService userAuthorizationService,
    IUserRegistrationService userRegistrationService) : ControllerBase
{
    /// <summary>
    /// Grants the ADMIN role to a user (DEBUG builds only).
    /// This endpoint exists solely for testing purposes.
    /// </summary>
    /// <param name="userId">The user ID to grant admin access to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>No content on success.</returns>
    [HttpPost("users/{userId:guid}/make-admin")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MakeAdmin(Guid userId, CancellationToken cancellationToken)
    {
        var user = await userProfileService.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "User not found",
                Detail = $"User with ID '{userId}' not found."
            });
        }

        // Assign ADMIN role (which grants full access)
        await userAuthorizationService.AssignRoleAsync(
            userId,
            new Application.Identity.Models.RoleAssignmentRequest(Roles.Admin.Code, null),
            cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// Creates a user and grants them ADMIN role in one step (DEBUG builds only).
    /// </summary>
    /// <param name="request">The registration request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created admin user's tokens.</returns>
    [HttpPost("create-admin")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateAdmin(
        [FromBody] CreateAdminRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Register the user
            var user = await userRegistrationService.RegisterUserAsync(
                new UserRegistrationRequest(
                    request.Username,
                    request.Password,
                    request.Email),
                cancellationToken);

            // Assign ADMIN role
            await userAuthorizationService.AssignRoleAsync(
                user.Id,
                new Application.Identity.Models.RoleAssignmentRequest(Roles.Admin.Code, null),
                cancellationToken);

            return Created(string.Empty, new { UserId = user.Id });
        }
        catch (DuplicateEntityException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Registration failed",
                Detail = ex.Message
            });
        }
        catch (PasswordValidationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Registration failed",
                Detail = ex.Message
            });
        }
    }
}
#endif
