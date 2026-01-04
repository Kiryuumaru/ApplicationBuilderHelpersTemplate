using Application.Identity.Interfaces;
using Asp.Versioning;
using Domain.Authorization.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Presentation.WebApi.Attributes;
using Presentation.WebApi.Models.Requests;
using AppRoleAssignment = Application.Identity.Models.RoleAssignmentRequest;

namespace Presentation.WebApi.Controllers.V1.Iam;

/// <summary>
/// Controller for role management operations within IAM (Identity and Access Management).
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{v:apiVersion}/iam/roles")]
[Produces("application/json")]
[Tags("IAM - Roles")]
[Authorize]
public class RolesController(
    IUserAuthorizationService userAuthorizationService) : ControllerBase
{
    /// <summary>
    /// Assigns a role to a user (admin only).
    /// </summary>
    /// <param name="request">The role assignment request containing user ID and role code.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>No content.</returns>
    [HttpPost("assign")]
    [RequiredPermission(PermissionIds.Api.Iam.Roles.Assign.Identifier)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AssignRole(
        [FromBody] RoleAssignmentRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await userAuthorizationService.AssignRoleAsync(
                request.UserId,
                new AppRoleAssignment(request.RoleCode, request.ParameterValues),
                cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Not found",
                Detail = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid operation",
                Detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Removes a role from a user (admin only).
    /// </summary>
    /// <param name="request">The role removal request containing user ID and role ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>No content.</returns>
    [HttpPost("remove")]
    [RequiredPermission(PermissionIds.Api.Iam.Roles.Remove.Identifier)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RemoveRole(
        [FromBody] RoleRemovalRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await userAuthorizationService.RemoveRoleAsync(request.UserId, request.RoleId, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Not found",
                Detail = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid operation",
                Detail = ex.Message
            });
        }
    }
}
