using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Domain.Identity.Exceptions;
using Presentation.WebApi.Models.Requests;
using Presentation.WebApi.Models.Responses;

namespace Presentation.WebApi.Controllers.V1;

public partial class AuthController
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
}
