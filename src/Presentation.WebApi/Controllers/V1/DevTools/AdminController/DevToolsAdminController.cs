#if DEBUG
using Application.Authorization.Interfaces;
using Application.Identity.Interfaces;
using Application.Identity.Models;
using Asp.Versioning;
using Domain.Authorization.Constants;
using Domain.Shared.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Presentation.WebApi.Controllers.V1.DevTools.AdminController.Requests;
using Presentation.WebApi.Controllers.V1.DevTools.AdminController.Responses;

namespace Presentation.WebApi.Controllers.V1.DevTools.AdminController;

/// <summary>
/// DevTools-only controller for testing purposes.
/// This controller is only available in DEBUG builds.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{v:apiVersion}/devtools")]
[Produces("application/json")]
[Tags("DevTools")]
public sealed class DevToolsAdminController(
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
            throw new EntityNotFoundException("User", userId.ToString());
        }

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
    /// <returns>The created user's ID.</returns>
    [HttpPost("create-admin")]
    [AllowAnonymous]
    [ProducesResponseType<CreateAdminResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateAdmin(
        [FromBody] CreateAdminRequest request,
        CancellationToken cancellationToken)
    {
        var user = await userRegistrationService.RegisterUserAsync(
            new UserRegistrationRequest(
                request.Username,
                request.Password,
                ConfirmPassword: null,
                Email: request.Email),
            cancellationToken);

        await userAuthorizationService.AssignRoleAsync(
            user.Id,
            new Application.Identity.Models.RoleAssignmentRequest(Roles.Admin.Code, null),
            cancellationToken);

        return Created(string.Empty, new CreateAdminResponse { UserId = user.Id });
    }
}
#endif
