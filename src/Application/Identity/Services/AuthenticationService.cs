using Application.Authorization.Interfaces;
using Application.Authorization.Interfaces.Infrastructure;
using Application.Identity.Interfaces;
using Application.Identity.Interfaces.Infrastructure;
using Application.Identity.Models;
using Domain.Authorization.Models;
using Domain.Identity.Exceptions;
using Domain.Identity.Interfaces;
using Domain.Identity.Models;
using Domain.Shared.Exceptions;
using System.Security.Claims;

namespace Application.Identity.Services;

/// <summary>
/// Implementation of IAuthenticationService using repositories directly.
/// </summary>
internal sealed class AuthenticationService(
    IUserRepository userRepository,
    ISessionRepository sessionRepository,
    IRoleRepository roleRepository,
    IPasswordVerifier passwordVerifier,
    ITokenService tokenService,
    IUserRoleResolver userRoleResolver) : IAuthenticationService
{
    private const string RbacVersionClaimType = "rbac_version";
    private const string CurrentRbacVersion = "2";

    private readonly IUserRepository _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
    private readonly ISessionRepository _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
    private readonly IRoleRepository _roleRepository = roleRepository ?? throw new ArgumentNullException(nameof(roleRepository));
    private readonly IPasswordVerifier _passwordVerifier = passwordVerifier ?? throw new ArgumentNullException(nameof(passwordVerifier));
    private readonly ITokenService _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
    private readonly IUserRoleResolver _userRoleResolver = userRoleResolver ?? throw new ArgumentNullException(nameof(userRoleResolver));

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
        return CredentialValidationResult.Success(user.Id, user.UserName, user.Email, roleCodes, user.IsAnonymous);
    }

    public async Task<CredentialValidationResult> GetUserForSessionAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _userRepository.FindByIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return CredentialValidationResult.Failed($"User with ID {userId} not found.");
        }

        var roleCodes = await ResolveRoleCodesAsync(user, cancellationToken).ConfigureAwait(false);
        return CredentialValidationResult.Success(user.Id, user.UserName, user.Email, roleCodes, user.IsAnonymous);
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
        var user = await _userRepository.FindByIdAsync(userId, cancellationToken).ConfigureAwait(false)
            ?? throw new EntityNotFoundException("User", userId.ToString());

        // TODO: Verify 2FA code using ITwoFactorService
        // For now, we just create the session (actual 2FA verification happens via ITwoFactorService)

        return await CreateSessionForUserInternalAsync(user, cancellationToken).ConfigureAwait(false);
    }

    private async Task<UserSessionDto> CreateSessionForUserInternalAsync(User user, CancellationToken cancellationToken)
    {
        // Resolve roles with their parameter values
        var roleResolutions = await _userRoleResolver.ResolveRolesAsync(user, cancellationToken).ConfigureAwait(false);
        
        // Format role claims with inline parameters (e.g., "USER;roleUserId=abc123")
        var roleClaims = roleResolutions
            .Select(r => Role.FormatRoleClaim(r.Role.Code, r.ParameterValues))
            .ToArray();

        // Create login session
        var now = DateTimeOffset.UtcNow;
        var refreshTokenExpiry = now.AddDays(7);
        var accessTokenExpiry = now.AddHours(1);

        // Generate a refresh token hash for the session
        var refreshTokenValue = Guid.NewGuid().ToString("N");
        var refreshTokenHash = ComputeHash(refreshTokenValue);

        var loginSession = LoginSession.Create(user.Id, refreshTokenHash, refreshTokenExpiry);
        await _sessionRepository.CreateAsync(loginSession, cancellationToken).ConfigureAwait(false);

        // Build additional claims with RBAC version
        var additionalClaims = new List<Claim>
        {
            new(RbacVersionClaimType, CurrentRbacVersion)
        };

        // Generate access token with inline role claims (including parameters)
        // Permission resolution happens at request time using the role codes
        var accessToken = await _tokenService.GenerateAccessTokenAsync(
            user.Id,
            user.UserName,
            roleClaims,
            additionalClaims,
            cancellationToken).ConfigureAwait(false);

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
            Roles = roleClaims
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
