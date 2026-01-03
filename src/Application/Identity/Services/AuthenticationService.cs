using Application.Authorization.Interfaces;
using Application.Authorization.Interfaces.Infrastructure;
using Application.Identity.Interfaces;
using Application.Identity.Interfaces.Infrastructure;
using Application.Identity.Models;
using Domain.Identity.Interfaces;
using Domain.Identity.Models;

namespace Application.Identity.Services;

/// <summary>
/// Implementation of IAuthenticationService using repositories directly.
/// </summary>
internal sealed class AuthenticationService(
    IUserRepository userRepository,
    ISessionRepository sessionRepository,
    IRoleRepository roleRepository,
    IPasswordVerifier passwordVerifier,
    ITokenService tokenService) : IAuthenticationService
{
    private readonly IUserRepository _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
    private readonly ISessionRepository _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
    private readonly IRoleRepository _roleRepository = roleRepository ?? throw new ArgumentNullException(nameof(roleRepository));
    private readonly IPasswordVerifier _passwordVerifier = passwordVerifier ?? throw new ArgumentNullException(nameof(passwordVerifier));
    private readonly ITokenService _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));

    public async Task<CredentialValidationResult> ValidateCredentialsAsync(string username, string password, CancellationToken cancellationToken)
    {
        var user = await _userRepository.FindByUsernameAsync(username, cancellationToken).ConfigureAwait(false);
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

        var roleCodes = await ResolveRoleCodesAsync(user, cancellationToken).ConfigureAwait(false);
        return CredentialValidationResult.Success(user.Id, user.UserName, roleCodes, user.IsAnonymous);
    }

    public async Task<CredentialValidationResult> GetUserForSessionAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _userRepository.FindByIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return CredentialValidationResult.Failed($"User with ID {userId} not found.");
        }

        var roleCodes = await ResolveRoleCodesAsync(user, cancellationToken).ConfigureAwait(false);
        return CredentialValidationResult.Success(user.Id, user.UserName, roleCodes, user.IsAnonymous);
    }

    public async Task<UserSessionDto> AuthenticateAsync(string username, string password, CancellationToken cancellationToken)
    {
        var result = await AuthenticateWithResultAsync(username, password, cancellationToken).ConfigureAwait(false);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException(result.ErrorMessage ?? "Authentication failed.");
        }

        if (result.RequiresTwoFactor)
        {
            throw new InvalidOperationException("Two-factor authentication is required.");
        }

        return result.Session ?? throw new InvalidOperationException("Session was not created.");
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
            ?? throw new InvalidOperationException($"User with ID {userId} not found.");

        return await CreateSessionForUserInternalAsync(user, cancellationToken).ConfigureAwait(false);
    }

    public async Task<UserSessionDto> Complete2faAuthenticationAsync(Guid userId, string code, CancellationToken cancellationToken)
    {
        var user = await _userRepository.FindByIdAsync(userId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"User with ID {userId} not found.");

        // TODO: Verify 2FA code using ITwoFactorService
        // For now, we just create the session (actual 2FA verification happens via ITwoFactorService)

        return await CreateSessionForUserInternalAsync(user, cancellationToken).ConfigureAwait(false);
    }

    private async Task<UserSessionDto> CreateSessionForUserInternalAsync(User user, CancellationToken cancellationToken)
    {
        var roleCodes = await ResolveRoleCodesAsync(user, cancellationToken).ConfigureAwait(false);

        // Create login session
        var now = DateTimeOffset.UtcNow;
        var refreshTokenExpiry = now.AddDays(7);
        var accessTokenExpiry = now.AddHours(1);

        // Generate a refresh token hash for the session
        var refreshTokenValue = Guid.NewGuid().ToString("N");
        var refreshTokenHash = ComputeHash(refreshTokenValue);

        var loginSession = LoginSession.Create(user.Id, refreshTokenHash, refreshTokenExpiry);
        await _sessionRepository.CreateAsync(loginSession, cancellationToken).ConfigureAwait(false);

        // Generate tokens
        var accessToken = await _tokenService.GenerateAccessTokenAsync(
            user.Id,
            user.UserName,
            roleCodes,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var refreshToken = await _tokenService.GenerateRefreshTokenAsync(
            loginSession.Id,
            cancellationToken).ConfigureAwait(false);

        return new UserSessionDto
        {
            SessionId = loginSession.Id,
            UserId = user.Id,
            Username = user.UserName,
            IsAnonymous = user.IsAnonymous,
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            IssuedAt = now,
            ExpiresAt = accessTokenExpiry,
            Roles = roleCodes
        };
    }

    private static string ComputeHash(string input)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(input));
        return Convert.ToBase64String(bytes);
    }

    private async Task<IReadOnlyCollection<string>> ResolveRoleCodesAsync(User user, CancellationToken cancellationToken)
    {
        if (user.RoleAssignments.Count == 0)
        {
            return [];
        }

        var roleIds = user.RoleAssignments.Select(ra => ra.RoleId).Distinct();
        var roles = await _roleRepository.GetByIdsAsync(roleIds, cancellationToken).ConfigureAwait(false);

        return roles.Select(r => r.Code).ToArray();
    }
}
