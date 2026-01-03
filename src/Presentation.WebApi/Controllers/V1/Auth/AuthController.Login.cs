using Application.Identity.Models;
using Domain.Authorization.Constants;
using Domain.Identity.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Presentation.WebApi.Attributes;
using Presentation.WebApi.Models.Requests;
using Presentation.WebApi.Models.Responses;
using System.Security.Authentication;
using System.Security.Claims;

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
            var result = await identityService.AuthenticateWithResultAsync(request.Username, request.Password, cancellationToken);

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
                    UserId = result.TwoFactorUserId!.Value
                });
            }

            var userSession = result.Session!;
            var (accessToken, refreshToken, loginSession) = await CreateSessionAndTokensAsync(userSession, cancellationToken);

            return Ok(new AuthResponse
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresIn = AccessTokenExpirationMinutes * 60,
                User = new UserInfo
                {
                    Id = userSession.UserId,
                    Username = userSession.Username,
                    Email = null, // Session doesn't include email currently
                    Roles = userSession.RoleCodes,
                    Permissions = userSession.PermissionIdentifiers
                }
            });
        }
        catch (AuthenticationException)
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

            // Validate password confirmation if password is provided
            if (!string.IsNullOrWhiteSpace(request.Password))
            {
                if (string.IsNullOrWhiteSpace(request.Username))
                {
                    return BadRequest(new ProblemDetails
                    {
                        Status = StatusCodes.Status400BadRequest,
                        Title = "Username required",
                        Detail = "Username is required when providing a password."
                    });
                }

                if (request.Password != request.ConfirmPassword)
                {
                    return BadRequest(new ProblemDetails
                    {
                        Status = StatusCodes.Status400BadRequest,
                        Title = "Passwords do not match",
                        Detail = "Password and ConfirmPassword must match."
                    });
                }
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
                var anonymousUser = await identityService.RegisterUserAsync(null, cancellationToken);
                var anonymousSession = UserSession.Create(
                    anonymousUser.Id,
                    anonymousUser.UserName,
                    [], // No roles for anonymous
                    [], // No permissions for anonymous
                    anonymousUser.IsAnonymous);

                var (accessToken, refreshToken, _) = await CreateSessionAndTokensAsync(anonymousSession, cancellationToken);

                return CreatedAtAction(nameof(GetMe), new AuthResponse
                {
                    AccessToken = accessToken,
                    RefreshToken = refreshToken,
                    ExpiresIn = AccessTokenExpirationMinutes * 60,
                    User = new UserInfo
                    {
                        Id = anonymousUser.Id,
                        Username = anonymousUser.UserName,
                        Email = null,
                        Roles = [],
                        Permissions = [],
                        IsAnonymous = true
                    }
                });
            }

            // Full registration with username/password
            // Check if username already exists
            var existingUser = await identityService.GetByUsernameAsync(request.Username!, cancellationToken);
            if (existingUser is not null)
            {
                return Conflict(new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "Username already exists",
                    Detail = $"The username '{request.Username}' is already taken."
                });
            }

            // Check if email already exists (if provided)
            if (!string.IsNullOrWhiteSpace(request.Email))
            {
                var existingByEmail = await identityService.GetByEmailAsync(request.Email, cancellationToken);
                if (existingByEmail is not null)
                {
                    return Conflict(new ProblemDetails
                    {
                        Status = StatusCodes.Status409Conflict,
                        Title = "Email already exists",
                        Detail = $"The email '{request.Email}' is already registered."
                    });
                }
            }

            var registrationRequest = new UserRegistrationRequest(
                request.Username!,
                request.Password!,
                request.Email,
                AutoActivate: true);

            var user = await identityService.RegisterUserAsync(registrationRequest, cancellationToken);

            // Authenticate the newly registered user to get session info
            var userSession = await identityService.AuthenticateAsync(request.Username!, request.Password!, cancellationToken);

            var (newAccessToken, newRefreshToken, loginSession) = await CreateSessionAndTokensAsync(userSession, cancellationToken);

            return CreatedAtAction(nameof(GetMe), new AuthResponse
            {
                AccessToken = newAccessToken,
                RefreshToken = newRefreshToken,
                ExpiresIn = AccessTokenExpirationMinutes * 60,
                User = new UserInfo
                {
                    Id = userSession.UserId,
                    Username = userSession.Username,
                    Email = request.Email,
                    Roles = userSession.RoleCodes,
                    Permissions = userSession.PermissionIdentifiers,
                    IsAnonymous = false
                }
            });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Registration failed",
                Detail = ex.Message
            });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("password", StringComparison.OrdinalIgnoreCase))
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
        [FromJwt(ClaimTypes.NameIdentifier), PermissionParameter(PermissionIds.Api.Auth.Logout.UserIdParameter)] Guid userId,
        CancellationToken cancellationToken)
    {
        var sessionId = GetCurrentSessionId();
        if (sessionId.HasValue)
        {
            await sessionStore.RevokeAsync(sessionId.Value, cancellationToken);
        }
        return NoContent();
    }
}
