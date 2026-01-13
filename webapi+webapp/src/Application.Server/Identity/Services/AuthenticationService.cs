using Application.Server.Authorization.Interfaces;
using Application.Server.Authorization.Interfaces.Infrastructure;
using Application.Server.Identity.Interfaces;
using Application.Server.Identity.Interfaces.Infrastructure;
using Application.Server.Identity.Models;
using Domain.Authorization.Constants;
using Domain.Authorization.Models;
using Domain.Identity.Constants;
using Domain.Identity.Exceptions;
using Domain.Identity.Interfaces;
using Domain.Identity.Models;
using Domain.Shared.Exceptions;
using System.Security.Claims;

namespace Application.Server.Identity.Services;

/// <summary>
/// Implementation of IAuthenticationService using repositories directly.
/// Delegates token generation to IUserTokenService.
/// </summary>
public sealed class AuthenticationService(
    IUserRepository userRepository,
    IPasswordVerifier passwordVerifier,
    IUserRoleResolver userRoleResolver,
    ITwoFactorService twoFactorService,
    IUserTokenService userTokenService) : IAuthenticationService
{

    private readonly IUserRepository _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
    private readonly IPasswordVerifier _passwordVerifier = passwordVerifier ?? throw new ArgumentNullException(nameof(passwordVerifier));
    private readonly IUserRoleResolver _userRoleResolver = userRoleResolver ?? throw new ArgumentNullException(nameof(userRoleResolver));
    private readonly ITwoFactorService _twoFactorService = twoFactorService ?? throw new ArgumentNullException(nameof(twoFactorService));
    private readonly IUserTokenService _userTokenService = userTokenService ?? throw new ArgumentNullException(nameof(userTokenService));

    public async Task<CredentialValidationResult> ValidateCredentialsAsync(string usernameOrEmail, string password, CancellationToken cancellationToken)
    {
        // Try username first, then fall back to email lookup
        var user = await _userRepository.FindByUsernameAsync(usernameOrEmail, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            user = await _userRepository.FindByEmailAsync(usernameOrEmail, cancellationToken).ConfigureAwait(false);
        }

        if (user is null)
        {
            return CredentialValidationResult.Failed("Invalid username or password.");
        }

        if (string.IsNullOrEmpty(user.PasswordHash))
        {
            return CredentialValidationResult.Failed("Invalid username or password.");
        }

        if (!_passwordVerifier.Verify(user.PasswordHash, password))
        {
            return CredentialValidationResult.Failed("Invalid username or password.");
        }

        // Check for 2FA requirement
        if (user.TwoFactorEnabled)
        {
            return CredentialValidationResult.TwoFactorRequired(user.Id);
        }

        var roleResolutions = await _userRoleResolver.ResolveRolesAsync(user, cancellationToken).ConfigureAwait(false);
        var roleCodes = roleResolutions.Select(r => r.Code).ToArray();
        return CredentialValidationResult.Success(user.Id, user.UserName, user.Email, roleCodes, user.IsAnonymous);
    }

    public async Task<CredentialValidationResult> GetUserForSessionAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _userRepository.FindByIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return CredentialValidationResult.Failed($"User with ID {userId} not found.");
        }

        var roleResolutions2 = await _userRoleResolver.ResolveRolesAsync(user, cancellationToken).ConfigureAwait(false);
        var roleCodes2 = roleResolutions2.Select(r => r.Code).ToArray();
        return CredentialValidationResult.Success(user.Id, user.UserName, user.Email, roleCodes2, user.IsAnonymous);
    }

    public async Task<UserSessionDto> AuthenticateAsync(string username, string password, CancellationToken cancellationToken)
    {
        var result = await AuthenticateWithResultAsync(username, password, cancellationToken).ConfigureAwait(false);

        if (!result.Succeeded)
        {
            throw new AuthenticationException(result.ErrorMessage ?? "Authentication failed.");
        }

        if (result.RequiresTwoFactor)
        {
            throw new TwoFactorRequiredException(result.UserId!.Value);
        }

        return result.Session ?? throw new EntityNotFoundException("Session", "create");
    }

    public async Task<AuthenticationResultDto> AuthenticateWithResultAsync(string username, string password, CancellationToken cancellationToken)
    {
        var user = await _userRepository.FindByUsernameAsync(username, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return AuthenticationResultDto.Failed("Invalid username or password.");
        }

        if (string.IsNullOrEmpty(user.PasswordHash))
        {
            return AuthenticationResultDto.Failed("Invalid username or password.");
        }

        if (!_passwordVerifier.Verify(user.PasswordHash, password))
        {
            return AuthenticationResultDto.Failed("Invalid username or password.");
        }

        // Check for 2FA requirement
        if (user.TwoFactorEnabled)
        {
            return AuthenticationResultDto.TwoFactorRequired(user.Id);
        }

        var sessionDto = await CreateSessionForUserInternalAsync(user, cancellationToken).ConfigureAwait(false);
        return AuthenticationResultDto.Success(sessionDto);
    }

    public async Task<UserSessionDto> CreateSessionForUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _userRepository.FindByIdAsync(userId, cancellationToken).ConfigureAwait(false)
            ?? throw new EntityNotFoundException("User", userId.ToString());

        return await CreateSessionForUserInternalAsync(user, cancellationToken).ConfigureAwait(false);
    }

    public async Task<UserSessionDto> Complete2faAuthenticationAsync(Guid userId, string code, CancellationToken cancellationToken)
    {
        var user = await _userRepository.FindByIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            throw new TwoFactorSessionInvalidException();
        }

        // SECURITY: Verify 2FA code before creating session
        var isValid = await _twoFactorService.Verify2faCodeAsync(userId, code, cancellationToken).ConfigureAwait(false);
        if (!isValid)
        {
            throw new InvalidTwoFactorCodeException();
        }

        return await CreateSessionForUserInternalAsync(user, cancellationToken).ConfigureAwait(false);
    }

    private async Task<UserSessionDto> CreateSessionForUserInternalAsync(User user, CancellationToken cancellationToken)
    {
        // Delegate all token generation to IUserTokenService
        var tokenResult = await _userTokenService.CreateSessionWithTokensAsync(
            user.Id,
            deviceInfo: null, // Device info not available in this flow
            cancellationToken).ConfigureAwait(false);

        // Get formatted role claims for the session DTO
        var roleResolutions = await _userRoleResolver.ResolveRolesAsync(user, cancellationToken).ConfigureAwait(false);
        var roleClaims = roleResolutions.Select(r => r.ToFormattedClaim()).OrderBy(r => r, StringComparer.Ordinal).ToArray();

        return new UserSessionDto
        {
            SessionId = tokenResult.SessionId,
            UserId = user.Id,
            Username = user.UserName,
            IsAnonymous = user.IsAnonymous,
            AccessToken = tokenResult.AccessToken,
            RefreshToken = tokenResult.RefreshToken,
            IssuedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(tokenResult.ExpiresInSeconds),
            Roles = roleClaims
        };
    }
}
