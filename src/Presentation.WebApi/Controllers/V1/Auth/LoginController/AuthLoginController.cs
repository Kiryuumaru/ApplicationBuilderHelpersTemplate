using Application.Common.Services;
using Application.Identity.Interfaces;
using Application.Identity.Models;
using Asp.Versioning;
using Domain.Authorization.Constants;
using Domain.Shared.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Presentation.WebApi.Attributes;
using Presentation.WebApi.Controllers.V1.Auth.LoginController.Requests;
using Presentation.WebApi.Controllers.V1.Auth.MeController;
using Presentation.WebApi.Controllers.V1.Auth.Shared.Responses;
using Presentation.WebApi.Controllers.V1.Auth.TwoFactorController.Responses;
using Presentation.WebApi.Extensions;
using JwtClaimTypes = Domain.Identity.Constants.JwtClaimTypes;

namespace Presentation.WebApi.Controllers.V1.Auth.LoginController;

/// <summary>
/// Controller for anonymous authentication endpoints (login, register) and current-session logout.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{v:apiVersion}/auth")]
[Produces("application/json")]
[Tags("Authentication")]
public sealed class AuthLoginController(
    IUserRegistrationService userRegistrationService,
    IAuthenticationService authenticationService,
    ISessionService sessionService,
    IUserAuthorizationService userAuthorizationService,
    IUserTokenService userTokenService) : ControllerBase
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
        var result = await authenticationService.ValidateCredentialsAsync(request.Username, request.Password, cancellationToken);

        if (!result.Succeeded && !result.RequiresTwoFactor)
        {
            throw new Domain.Identity.Exceptions.AuthenticationException("Invalid credentials.");
        }

        if (result.RequiresTwoFactor)
        {
            return Accepted(new TwoFactorRequiredResponse
            {
                UserId = result.UserId!.Value
            });
        }

        var (accessToken, refreshToken, _, expiresIn) = await CreateSessionAndTokensAsync(
            result.UserId!.Value,
            cancellationToken);

        var userInfo = await CreateUserInfoAsync(result.UserId!.Value, cancellationToken);

        return Ok(new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresIn = expiresIn,
            User = userInfo
        });
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
        request ??= new RegisterRequest();

        if (!string.IsNullOrWhiteSpace(request.Password) && string.IsNullOrWhiteSpace(request.Username))
        {
            throw new ValidationException("Username", "Username is required when providing a password.");
        }

        if (!string.IsNullOrWhiteSpace(request.Email) && string.IsNullOrWhiteSpace(request.Username))
        {
            throw new ValidationException("Username", "Username is required when providing an email.");
        }

        if (request.IsAnonymous)
        {
            var anonymousUser = await userRegistrationService.RegisterUserAsync(null, cancellationToken);

            var (accessToken, refreshToken, _, expiresInAnon) = await CreateSessionAndTokensAsync(
                anonymousUser.Id,
                cancellationToken);

            var anonUserInfo = await CreateUserInfoAsync(anonymousUser.Id, cancellationToken);

            var response = new AuthResponse
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresIn = expiresInAnon,
                User = anonUserInfo
            };

            return CreatedAtMe(response);
        }

        var registrationRequest = new UserRegistrationRequest(
            request.Username!,
            request.Password!,
            request.ConfirmPassword,
            request.Email,
            AutoActivate: true);

        var user = await userRegistrationService.RegisterUserAsync(registrationRequest, cancellationToken);

        var (newAccessToken, newRefreshToken, _, newExpiresIn) = await CreateSessionAndTokensAsync(
            user.Id,
            cancellationToken);

        var newUserInfo = await CreateUserInfoAsync(user.Id, cancellationToken);

        var newResponse = new AuthResponse
        {
            AccessToken = newAccessToken,
            RefreshToken = newRefreshToken,
            ExpiresIn = newExpiresIn,
            User = newUserInfo
        };

        return CreatedAtMe(newResponse);
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
        _ = userId;

        var sessionId = User.GetSessionId();
        if (sessionId.HasValue)
        {
            await sessionService.RevokeAsync(sessionId.Value, cancellationToken);
        }
        return NoContent();
    }

    private IActionResult CreatedAtMe(AuthResponse response)
    {
        _ = RouteData.Values.TryGetValue("v", out var apiVersion);

        return CreatedAtAction(
            actionName: nameof(AuthMeController.GetMe),
            controllerName: "AuthMe",
            routeValues: apiVersion is null ? null : new { v = apiVersion },
            value: response);
    }

    private async Task<(string AccessToken, string RefreshToken, Guid SessionId, int ExpiresInSeconds)> CreateSessionAndTokensAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var userAgent = Request.Headers.UserAgent.ToString();
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var deviceName = DeviceInfoParser.ParseDeviceName(userAgent);

        var deviceInfo = new SessionDeviceInfo(deviceName, userAgent, ipAddress);
        var result = await userTokenService.CreateSessionWithTokensAsync(userId, deviceInfo, cancellationToken);

        return (result.AccessToken, result.RefreshToken, result.SessionId, result.ExpiresInSeconds);
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
