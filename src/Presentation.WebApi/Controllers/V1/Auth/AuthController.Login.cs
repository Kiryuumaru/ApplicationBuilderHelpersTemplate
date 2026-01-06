using Application.Identity.Models;
using Domain.Authorization.Constants;
using Domain.Identity.Exceptions;
using Domain.Shared.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Presentation.WebApi.Attributes;
using Presentation.WebApi.Models.Requests;
using Presentation.WebApi.Models.Responses;
using System.Security.Authentication;
using JwtClaimTypes = Domain.Identity.Constants.JwtClaimTypes;

namespace Presentation.WebApi.Controllers.V1;

public partial class AuthController
{
    /// <summary>
    /// Authenticates a user and returns JWT tokens.
    /// </summary>
    /// <param name="request">The login credentials.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>JWT access and refresh tokens along with user information, or a 2FA challenge.</returns>
    /// <response code="200">Returns the JWT tokens and user information.</response>
    /// <response code="202">Two-factor authentication is required. Returns user ID to complete 2FA.</response>
    /// <response code="401">Invalid credentials.</response>
    /// <response code="403">Account is locked.</response>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<TwoFactorRequiredResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await authenticationService.ValidateCredentialsAsync(request.Username, request.Password, cancellationToken);

            if (!result.Succeeded && !result.RequiresTwoFactor)
            {
                return Unauthorized(new ProblemDetails
                {
                    Status = StatusCodes.Status401Unauthorized,
                    Title = "Invalid credentials",
                    Detail = "The username or password is incorrect."
                });
            }

            if (result.RequiresTwoFactor)
            {
                return Accepted(new TwoFactorRequiredResponse
                {
                    UserId = result.UserId!.Value
                });
            }

            // Create session and get effective permissions
            var (accessToken, refreshToken, sessionId, expiresIn) = await CreateSessionAndTokensAsync(
                result.UserId!.Value,
                result.Username,
                cancellationToken);

            var userInfo = await CreateUserInfoAsync(
                result.UserId!.Value,
                cancellationToken);

            return Ok(new AuthResponse
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresIn = expiresIn,
                User = userInfo
            });
        }
        catch (Domain.Identity.Exceptions.AuthenticationException)
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Invalid credentials",
                Detail = "The username or password is incorrect."
            });
        }
        catch (Domain.Identity.Exceptions.AccountLockedException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Account locked",
                Detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Registers a new user account.
    /// With no body or empty body, creates an anonymous user.
    /// With username/password, creates a full account.
    /// </summary>
    /// <param name="request">The registration details. All fields optional for anonymous registration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>JWT tokens for the newly registered user.</returns>
    /// <response code="201">User created successfully, returns tokens.</response>
    /// <response code="400">Invalid registration data.</response>
    /// <response code="409">Username or email already exists.</response>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest? request, CancellationToken cancellationToken)
    {
        try
        {
            // Default to empty request for anonymous registration
            request ??= new RegisterRequest();

            // Validate username is required when providing password
            if (!string.IsNullOrWhiteSpace(request.Password) && string.IsNullOrWhiteSpace(request.Username))
            {
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Username required",
                    Detail = "Username is required when providing a password."
                });
            }

            // Validate username is provided if email is provided
            if (!string.IsNullOrWhiteSpace(request.Email) && string.IsNullOrWhiteSpace(request.Username))
            {
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Username required",
                    Detail = "Username is required when providing an email."
                });
            }

            // Check if this is anonymous registration
            if (request.IsAnonymous)
            {
                var anonymousUser = await userRegistrationService.RegisterUserAsync(null, cancellationToken);

                var (accessToken, refreshToken, _, expiresInAnon) = await CreateSessionAndTokensAsync(
                    anonymousUser.Id,
                    anonymousUser.Username,
                    cancellationToken);

                var anonUserInfo = await CreateUserInfoAsync(
                    anonymousUser.Id,
                    cancellationToken);

                return CreatedAtAction(nameof(GetMe), new AuthResponse
                {
                    AccessToken = accessToken,
                    RefreshToken = refreshToken,
                    ExpiresIn = expiresInAnon,
                    User = anonUserInfo
                });
            }

            // Full registration with username/password - service handles validation
            var registrationRequest = new UserRegistrationRequest(
                request.Username!,
                request.Password!,
                request.ConfirmPassword,
                request.Email,
                AutoActivate: true);

            var user = await userRegistrationService.RegisterUserAsync(registrationRequest, cancellationToken);

            // Create session for the newly registered user (no double session)
            var (newAccessToken, newRefreshToken, sessionId, newExpiresIn) = await CreateSessionAndTokensAsync(
                user.Id,
                user.Username,
                cancellationToken);

            var newUserInfo = await CreateUserInfoAsync(
                user.Id,
                cancellationToken);

            return CreatedAtAction(nameof(GetMe), new AuthResponse
            {
                AccessToken = newAccessToken,
                RefreshToken = newRefreshToken,
                ExpiresIn = newExpiresIn,
                User = newUserInfo
            });
        }
        catch (DuplicateEntityException ex)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Registration failed",
                Detail = ex.Message
            });
        }
        catch (PasswordValidationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid password",
                Detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Invalidates the current session (logout).
    /// </summary>
    /// <param name="userId">The user ID from JWT claims (used for permission check).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Success status.</returns>
    /// <remarks>
    /// Revokes the current session so the refresh token can no longer be used.
    /// </remarks>
    /// <response code="204">Successfully logged out.</response>
    /// <response code="401">Not authenticated.</response>
    /// <response code="403">Insufficient permissions.</response>
    [HttpPost("logout")]
    [RequiredPermission(PermissionIds.Api.Auth.Logout.Identifier)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Logout(
        [FromJwt(JwtClaimTypes.Subject), PermissionParameter(PermissionIds.Api.Auth.Logout.UserIdParameter)] Guid userId,
        CancellationToken cancellationToken)
    {
        var sessionId = GetCurrentSessionId();
        if (sessionId.HasValue)
        {
            await sessionService.RevokeAsync(sessionId.Value, cancellationToken);
        }
        return NoContent();
    }
}
