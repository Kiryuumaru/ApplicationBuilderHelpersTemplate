using System.Security.Claims;
using System.Security.Cryptography;
using Application.Authorization.Interfaces;
using Application.Identity.Interfaces;
using Application.Identity.Models;
using Domain.Authorization.Constants;
using Domain.Authorization.ValueObjects;

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
    private const string SessionIdClaimType = "sid";
    private static readonly TimeSpan AccessTokenExpiration = TimeSpan.FromMinutes(60);
    private static readonly TimeSpan RefreshTokenExpiration = TimeSpan.FromDays(7);

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
        var tokenHash = HashToken(refreshToken);

        // Create session
        await sessionService.CreateSessionAsync(
            userId,
            tokenHash,
            DateTimeOffset.UtcNow.Add(RefreshTokenExpiration),
            deviceInfo?.DeviceName,
            deviceInfo?.UserAgent,
            deviceInfo?.IpAddress,
            sessionId,
            cancellationToken);

        // Generate access token
        var accessToken = await GenerateAccessTokenAsync(authData, sessionId, cancellationToken);

        return new UserTokenResult(accessToken, refreshToken, sessionId, (int)AccessTokenExpiration.TotalSeconds);
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
        var tokenHash = HashToken(refreshToken);

        // Update session with new token hash
        await sessionService.UpdateRefreshTokenAsync(
            sessionId,
            tokenHash,
            DateTimeOffset.UtcNow.Add(RefreshTokenExpiration),
            cancellationToken);

        // Generate new access token
        var accessToken = await GenerateAccessTokenAsync(authData, sessionId, cancellationToken);

        return new UserTokenResult(accessToken, refreshToken, sessionId, (int)AccessTokenExpiration.TotalSeconds);
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
            new(SessionIdClaimType, sessionId.ToString())
        };

        // Add role claims using short claim type name (not verbose MS schema)
        foreach (var role in authData.FormattedRoles)
        {
            additionalClaims.Add(new Claim("role", role));
        }

        // Get ONLY direct permission grants - NOT role-derived scopes
        // Role scopes (including deny;api:auth:refresh) are resolved at runtime from the database
        var scopes = authData.DirectPermissionScopes.Select(ScopeDirective.Parse).ToList();

        return await permissionService.GenerateTokenWithScopeAsync(
            authData.UserId.ToString(),
            authData.Username ?? string.Empty,
            scopes,
            additionalClaims,
            DateTimeOffset.UtcNow.Add(AccessTokenExpiration),
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
            new(SessionIdClaimType, sessionId.ToString())
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
            DateTimeOffset.UtcNow.Add(RefreshTokenExpiration),
            cancellationToken);
    }

    /// <summary>
    /// Hashes a token for secure storage.
    /// </summary>
    private static string HashToken(string token)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(token);
        var hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }
}
