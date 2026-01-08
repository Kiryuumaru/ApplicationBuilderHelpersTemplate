using Application.Identity.Interfaces;
using Asp.Versioning;
using Domain.Authorization.Constants;
using Domain.Authorization.Models;
using Domain.Shared.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Presentation.WebApi.Attributes;
using Presentation.WebApi.Controllers.V1.Iam.PermissionsController.Requests;
using Presentation.WebApi.Controllers.V1.Iam.PermissionsController.Responses;
using AuthorizationPermissions = Domain.Authorization.Constants.Permissions;

namespace Presentation.WebApi.Controllers.V1.Iam.PermissionsController;

/// <summary>
/// IAM permission operations.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{v:apiVersion}/iam")]
[Produces("application/json")]
[Authorize]
[Tags("IAM")]
public sealed class IamPermissionsController(
    IUserAuthorizationService userAuthorizationService) : ControllerBase
{
    /// <summary>
    /// Lists all available permissions in the system.
    /// </summary>
    /// <returns>The permission tree.</returns>
    [HttpGet("permissions")]
    [ProducesResponseType<PermissionListResponse>(StatusCodes.Status200OK)]
    public IActionResult ListPermissions()
    {
        var permissions = AuthorizationPermissions.PermissionTreeRoots
            .Select(MapToResponse)
            .ToList();

        return Ok(new PermissionListResponse { Permissions = permissions });
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
        var grantedBy = User.Identity?.Name;
        await userAuthorizationService.GrantPermissionAsync(
            request.UserId,
            request.PermissionIdentifier,
            request.IsAllow,
            request.Description,
            grantedBy,
            cancellationToken);
        return NoContent();
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
        var revoked = await userAuthorizationService.RevokePermissionAsync(
            request.UserId,
            request.PermissionIdentifier,
            cancellationToken);

        if (!revoked)
        {
            throw new EntityNotFoundException("Permission", request.PermissionIdentifier);
        }

        return NoContent();
    }

    private static PermissionInfoResponse MapToResponse(Permission permission)
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
                ? permission.Permissions.Select(MapToResponse).ToList()
                : null
        };
    }
}
