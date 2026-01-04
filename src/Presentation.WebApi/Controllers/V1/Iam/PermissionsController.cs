using Application.Identity.Interfaces;
using Asp.Versioning;
using Domain.Authorization.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Presentation.WebApi.Attributes;
using Presentation.WebApi.Models.Requests;

namespace Presentation.WebApi.Controllers.V1.Iam;

/// <summary>
/// Controller for permission management operations within IAM (Identity and Access Management).
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{v:apiVersion}/iam/permissions")]
[Produces("application/json")]
[Tags("IAM - Permissions")]
[Authorize]
public class PermissionsController(
    IUserAuthorizationService userAuthorizationService) : ControllerBase
{
    /// <summary>
    /// Grants a direct permission to a user (admin only).
    /// </summary>
    /// <param name="request">The permission grant request containing user ID and permission identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>No content.</returns>
    [HttpPost("grant")]
    [RequiredPermission(PermissionIds.Api.Iam.Permissions.Grant.Identifier)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GrantPermission(
        [FromBody] PermissionGrantRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Use the current user's username as the granter
            var grantedBy = User.Identity?.Name;
            await userAuthorizationService.GrantPermissionAsync(
                request.UserId,
                request.PermissionIdentifier,
                request.Description,
                grantedBy,
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
    /// Revokes a direct permission from a user (admin only).
    /// </summary>
    /// <param name="request">The permission revocation request containing user ID and permission identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>No content.</returns>
    [HttpPost("revoke")]
    [RequiredPermission(PermissionIds.Api.Iam.Permissions.Revoke.Identifier)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RevokePermission(
        [FromBody] PermissionRevocationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var revoked = await userAuthorizationService.RevokePermissionAsync(
                request.UserId,
                request.PermissionIdentifier,
                cancellationToken);

            if (!revoked)
            {
                return NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Permission not found",
                    Detail = $"User does not have direct permission '{request.PermissionIdentifier}'."
                });
            }

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
