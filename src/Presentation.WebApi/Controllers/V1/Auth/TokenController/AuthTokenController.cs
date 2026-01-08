using Application.Identity.Interfaces;
using Asp.Versioning;
using Domain.Identity.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Presentation.WebApi.Controllers.V1.Auth.TokenController.Requests;
using Presentation.WebApi.Controllers.V1.Auth.Shared.Responses;

namespace Presentation.WebApi.Controllers.V1.Auth.TokenController;

/// <summary>
/// Controller for token management endpoints.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{v:apiVersion}/auth")]
[Produces("application/json")]
[Tags("Authentication")]
public sealed class AuthTokenController(
    IUserAuthorizationService userAuthorizationService,
    IUserTokenService userTokenService) : ControllerBase
{
    /// <summary>
    /// Refreshes an expired access token using a valid refresh token.
    /// </summary>
    /// <param name="request">The refresh token.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>New JWT access and refresh tokens.</returns>
    /// <response code="200">Returns new JWT tokens.</response>
    /// <response code="401">Invalid or expired refresh token.</response>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var result = await userTokenService.RefreshTokensAsync(request.RefreshToken, cancellationToken);

        if (!result.Succeeded)
        {
            throw new RefreshTokenInvalidException(result.Error, result.ErrorDescription);
        }

        var refreshUserInfo = await CreateUserInfoAsync(result.UserId!.Value, cancellationToken);

        return Ok(new AuthResponse
        {
            AccessToken = result.Tokens!.AccessToken,
            RefreshToken = result.Tokens.RefreshToken,
            ExpiresIn = result.Tokens.ExpiresInSeconds,
            User = refreshUserInfo
        });
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
