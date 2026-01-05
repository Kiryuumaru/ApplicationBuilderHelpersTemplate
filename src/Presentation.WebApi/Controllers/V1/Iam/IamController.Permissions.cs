using Domain.Authorization.Constants;
using Domain.Authorization.Models;
using Microsoft.AspNetCore.Mvc;
using Presentation.WebApi.Attributes;
using Presentation.WebApi.Models.Requests;
using Presentation.WebApi.Models.Responses;

namespace Presentation.WebApi.Controllers.V1.Iam;

public partial class IamController
{
    /// <summary>
    /// Lists all available permissions in the system.
    /// </summary>
    /// <returns>The permission tree.</returns>
    [HttpGet("permissions")]
    [ProducesResponseType<PermissionListResponse>(StatusCodes.Status200OK)]
    public IActionResult ListPermissions()
    {
        var permissions = Permissions.PermissionTreeRoots
            .Select(MapPermissionToResponse)
            .ToList();

        return Ok(new PermissionListResponse { Permissions = permissions });
    }

    private static PermissionInfoResponse MapPermissionToResponse(Permission permission)
    {
        return new PermissionInfoResponse
        {
            Path = permission.Path,
            Identifier = permission.Identifier,
            Description = permission.Description,
            Parameters = permission.Parameters,
            IsRead = permission.IsRead,
            IsWrite = permission.IsWrite,
            Children = permission.HasChildren
                ? permission.Permissions.Select(MapPermissionToResponse).ToList()
                : null
        };
    }

    /// <summary>
    /// Grants a direct permission to a user (admin only).
    /// </summary>
    /// <param name="request">The permission grant request containing user ID and permission identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>No content.</returns>
    [HttpPost("permissions/grant")]
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
    [HttpPost("permissions/revoke")]
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
