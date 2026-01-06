using Domain.Authorization.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Presentation.WebApi.Attributes;
using Presentation.WebApi.Models.Responses;
using JwtClaimTypes = Domain.Identity.Constants.ClaimTypes;

namespace Presentation.WebApi.Controllers.V1;

public partial class AuthController
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
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "User not found",
                Detail = "The user associated with this token no longer exists."
            });
        }

        var meUserInfo = await CreateUserInfoAsync(
            userId,
            cancellationToken);

        return Ok(meUserInfo);
    }
}
