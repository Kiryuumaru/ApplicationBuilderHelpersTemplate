using Application.Identity.Interfaces;
using Asp.Versioning;
using Domain.Authorization.Constants;
using Microsoft.AspNetCore.Mvc;
using Presentation.WebApi.Attributes;
using Presentation.WebApi.Controllers.V1.Auth.Shared.Responses;
using System.Security.Authentication;
using JwtClaimTypes = Domain.Identity.Constants.JwtClaimTypes;

namespace Presentation.WebApi.Controllers.V1.Auth.MeController;

/// <summary>
/// Controller for authenticated profile operations.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{v:apiVersion}/auth")]
[Produces("application/json")]
[Tags("Authentication")]
public sealed class AuthMeController(
    IUserProfileService userProfileService,
    IUserAuthorizationService userAuthorizationService) : ControllerBase
{
    /// <summary>
    /// Gets the current user's information.
    /// </summary>
    /// <param name="userId">The user ID from JWT claims.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The current user's information.</returns>
    /// <response code="200">Returns the current user's information.</response>
    /// <response code="401">Not authenticated.</response>
    [HttpGet("me")]
    [RequiredPermission(PermissionIds.Api.Auth.Me.Identifier)]
    [ProducesResponseType<UserInfo>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMe(
        [FromJwt(JwtClaimTypes.Subject), PermissionParameter(PermissionIds.Api.Auth.Me.UserIdParameter)] Guid userId,
        CancellationToken cancellationToken)
    {
        var user = await userProfileService.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            throw new AuthenticationException("The user associated with this token no longer exists.");
        }

        var meUserInfo = await CreateUserInfoAsync(userId, cancellationToken);
        return Ok(meUserInfo);
    }

    private async Task<UserInfo> CreateUserInfoAsync(Guid userId, CancellationToken cancellationToken)
    {
        var authData = await userAuthorizationService.GetAuthorizationDataAsync(userId, cancellationToken);

        return new UserInfo
        {
            Id = authData.UserId,
            Username = authData.Username,
            Email = authData.Email,
            Roles = authData.FormattedRoles,
            Permissions = authData.EffectivePermissions,
            IsAnonymous = authData.IsAnonymous
        };
    }
}
