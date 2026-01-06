using System.Security.Claims;
using Application.Authorization.Interfaces;
using Application.Identity.Interfaces;
using Application.Identity.Models;
using Domain.Authorization.Constants;
using Domain.Authorization.ValueObjects;
using Domain.Identity.Constants;
using Domain.Identity.Enums;
using JwtClaimTypes = Domain.Identity.Constants.JwtClaimTypes;

namespace Application.Identity.Services;

/// <summary>
/// Service for user authentication token generation and rotation.
/// Coordinates token generation with session management atomically.
/// </summary>
public sealed class UserTokenService(
    IUserAuthorizationService userAuthorizationService,
    ISessionService sessionService,
    IPermissionService permissionService) : IUserTokenService
{

    /// <inheritdoc />
    public async Task<UserTokenResult> CreateSessionWithTokensAsync(
        Guid userId,
        SessionDeviceInfo? deviceInfo,
        CancellationToken cancellationToken)
    {
        // Get user authorization data (includes username)
        var authData = await userAuthorizationService.GetAuthorizationDataAsync(userId, cancellationToken);

        // Generate session ID
        var sessionId = Guid.NewGuid();

        // Generate refresh token and hash
        var refreshToken = await GenerateRefreshTokenAsync(userId, authData.Username, sessionId, cancellationToken);
        var tokenHash = Common.Services.TokenHasher.Hash(refreshToken);

        // Create session
        await sessionService.CreateSessionAsync(
            userId,
            tokenHash,
            DateTimeOffset.UtcNow.Add(TokenExpirations.RefreshToken),
            deviceInfo?.DeviceName,
            deviceInfo?.UserAgent,
            deviceInfo?.IpAddress,
            sessionId,
            cancellationToken);

        // Generate access token
        var accessToken = await GenerateAccessTokenAsync(authData, sessionId, cancellationToken);

        return new UserTokenResult(accessToken, refreshToken, sessionId, (int)TokenExpirations.AccessToken.TotalSeconds);
    }

    /// <inheritdoc />
    public async Task<UserTokenResult> RotateTokensAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        // Get session to find user
        var session = await sessionService.GetByIdAsync(sessionId, cancellationToken)
            ?? throw new InvalidOperationException($"Session {sessionId} not found");

        // Get user authorization data
        var authData = await userAuthorizationService.GetAuthorizationDataAsync(session.UserId, cancellationToken);

        // Generate new refresh token and hash
        var refreshToken = await GenerateRefreshTokenAsync(session.UserId, authData.Username, sessionId, cancellationToken);
        var tokenHash = Common.Services.TokenHasher.Hash(refreshToken);

        // Update session with new token hash
        await sessionService.UpdateRefreshTokenAsync(
            sessionId,
            tokenHash,
            DateTimeOffset.UtcNow.Add(TokenExpirations.RefreshToken),
            cancellationToken);

        // Generate new access token
        var accessToken = await GenerateAccessTokenAsync(authData, sessionId, cancellationToken);

        return new UserTokenResult(accessToken, refreshToken, sessionId, (int)TokenExpirations.AccessToken.TotalSeconds);
    }

    /// <summary>
    /// Generates an access token for the user session.
    /// Role claims use inline parameter format (e.g., "USER;roleUserId=abc123").
    /// Only direct permission grants are included as scopes - role-derived scopes are resolved at runtime.
    /// </summary>
    private async Task<string> GenerateAccessTokenAsync(
        UserAuthorizationData authData,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var additionalClaims = new List<Claim>
        {
            new(JwtClaimTypes.SessionId, sessionId.ToString())
        };

        // RFC 9068 Section 2.2.3.1 / RFC 7643 Section 4.1.2 specify "roles" (plural)
        foreach (var role in authData.FormattedRoles)
        {
            additionalClaims.Add(new Claim(JwtClaimTypes.Roles, role));
        }

        // Get ONLY direct permission grants - NOT role-derived scopes
        // Role scopes (including deny;api:auth:refresh) are resolved at runtime from the database
        var scopes = authData.DirectPermissionScopes.Select(ScopeDirective.Parse).ToList();

        return await permissionService.GenerateTokenWithScopeAsync(
            authData.UserId.ToString(),
            authData.Username ?? string.Empty,
            scopes,
            additionalClaims,
            DateTimeOffset.UtcNow.Add(TokenExpirations.AccessToken),
            TokenType.Access,
            cancellationToken);
    }

    /// <summary>
    /// Generates a refresh token for the user session.
    /// Refresh tokens ONLY have the api:auth:refresh permission - they cannot access any other endpoint.
    /// </summary>
    private async Task<string> GenerateRefreshTokenAsync(
        Guid userId,
        string? username,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var refreshClaims = new List<Claim>
        {
            new(JwtClaimTypes.SessionId, sessionId.ToString())
        };

        // Refresh token only has permission to refresh - nothing else
        var refreshScopes = new[]
        {
            ScopeDirective.Parse(PermissionIds.Api.Auth.Refresh.WithUserId(userId.ToString()).Allow())
        };

        return await permissionService.GenerateTokenWithScopeAsync(
            userId.ToString(),
            username ?? string.Empty,
            refreshScopes,
            additionalClaims: refreshClaims,
            DateTimeOffset.UtcNow.Add(TokenExpirations.RefreshToken),
            TokenType.Refresh,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<TokenRefreshResult> RefreshTokensAsync(
        string refreshToken,
        CancellationToken cancellationToken)
    {
        // Validate token and get principal
        var principal = await permissionService.ValidateTokenAsync(refreshToken, cancellationToken);
        if (principal is null)
        {
            return TokenRefreshResult.Failure("invalid_token", "The refresh token is invalid or has expired.");
        }

        // Extract user ID using short claim types
        var userIdClaim = principal.FindFirst(JwtClaimTypes.Subject);
        if (userIdClaim is null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return TokenRefreshResult.Failure("invalid_token", "The refresh token does not contain valid user information.");
        }

        // Verify this is a refresh token (has refresh permission)
        var refreshPermission = PermissionIds.Api.Auth.Refresh.Permission.WithUserId(userId.ToString());
        if (!await permissionService.HasPermissionAsync(principal, refreshPermission, cancellationToken))
        {
            return TokenRefreshResult.Failure("invalid_token_type", "The provided token does not have refresh permission.");
        }

        // Extract session ID
        var sessionIdClaim = principal.FindFirst(JwtClaimTypes.SessionId);
        if (sessionIdClaim is null || !Guid.TryParse(sessionIdClaim.Value, out var sessionId))
        {
            return TokenRefreshResult.Failure("invalid_token", "The refresh token does not contain a valid session.");
        }

        // Validate session exists and token matches
        var loginSession = await sessionService.ValidateSessionWithTokenAsync(sessionId, refreshToken, cancellationToken);
        if (loginSession is null)
        {
            return TokenRefreshResult.Failure("session_revoked", "This session has been revoked or has expired.");
        }

        // Rotate tokens
        var tokens = await RotateTokensAsync(sessionId, cancellationToken);

        return TokenRefreshResult.Success(userId, tokens);
    }
}
