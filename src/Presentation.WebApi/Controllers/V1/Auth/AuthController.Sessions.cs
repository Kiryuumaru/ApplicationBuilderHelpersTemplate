using Domain.Authorization.Constants;
using Domain.Shared.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Presentation.WebApi.Attributes;
using Presentation.WebApi.Models.Responses;
using System.ComponentModel.DataAnnotations;

namespace Presentation.WebApi.Controllers.V1;

public partial class AuthController
{
    /// <summary>
    /// Lists all active sessions for the user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of active sessions.</returns>
    /// <response code="200">Returns the list of sessions.</response>
    /// <response code="401">Not authenticated.</response>
    [HttpGet("users/{userId:guid}/sessions")]
    [RequiredPermission(PermissionIds.Api.Auth.Sessions.List.Identifier)]
    [ProducesResponseType<SessionListResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ListSessions(
        [FromRoute, Required, PermissionParameter(PermissionIds.Api.Auth.UserIdParameter)] Guid userId,
        CancellationToken cancellationToken)
    {
        var currentSessionId = GetCurrentSessionId();
        var sessions = await sessionService.GetActiveByUserIdAsync(userId, cancellationToken);

        var sessionInfos = sessions.Select(s => new SessionInfoResponse
        {
            Id = s.Id,
            DeviceName = s.DeviceName,
            UserAgent = s.UserAgent,
            IpAddress = s.IpAddress,
            CreatedAt = s.CreatedAt,
            LastUsedAt = s.LastUsedAt,
            IsCurrent = s.Id == currentSessionId
        }).ToList();

        return Ok(new SessionListResponse { Sessions = sessionInfos });
    }

    /// <summary>
    /// Revokes a specific session (logout that device).
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="id">The session ID to revoke.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Success status.</returns>
    /// <response code="204">Session revoked successfully.</response>
    /// <response code="401">Not authenticated.</response>
    /// <response code="404">Session not found.</response>
    [HttpDelete("users/{userId:guid}/sessions/{id:guid}")]
    [RequiredPermission(PermissionIds.Api.Auth.Sessions.Revoke.Identifier)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeSession(
        [FromRoute, Required, PermissionParameter(PermissionIds.Api.Auth.UserIdParameter)] Guid userId,
        Guid id,
        CancellationToken cancellationToken)
    {
        // Service validates ownership and revokes
        var success = await sessionService.RevokeForUserAsync(userId, id, cancellationToken);
        if (!success)
        {
            throw new EntityNotFoundException("Session", id.ToString());
        }

        return NoContent();
    }

    /// <summary>
    /// Revokes all sessions for the user (logout everywhere).
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Success status with count of revoked sessions.</returns>
    /// <response code="200">Sessions revoked successfully.</response>
    /// <response code="401">Not authenticated.</response>
    [HttpDelete("users/{userId:guid}/sessions")]
    [RequiredPermission(PermissionIds.Api.Auth.Sessions.RevokeAll.Identifier)]
    [ProducesResponseType<SessionRevokeAllResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RevokeAllSessions(
        [FromRoute, Required, PermissionParameter(PermissionIds.Api.Auth.UserIdParameter)] Guid userId,
        CancellationToken cancellationToken)
    {
        var revokedCount = await sessionService.RevokeAllForUserAsync(userId, cancellationToken);
        return Ok(new SessionRevokeAllResponse { RevokedCount = revokedCount });
    }
}
