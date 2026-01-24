using Application.Server.Identity.Interfaces;
using Application.Server.Identity.Interfaces.Infrastructure;
using Application.Server.Identity.Models;
using Microsoft.Extensions.Logging;

namespace Application.Server.Identity.Services;

/// <summary>
/// Service for managing user sessions.
/// </summary>
public sealed class SessionService(
    ISessionRepository sessionRepository,
    ILogger<SessionService> logger) : ISessionService
{
    /// <inheritdoc />
    public async Task<SessionDto?> GetByIdAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var session = await sessionRepository.GetByIdAsync(sessionId, cancellationToken);
        return session is null ? null : MapToDto(session);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<SessionDto>> GetActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        var sessions = await sessionRepository.GetActiveByUserIdAsync(userId, cancellationToken);
        logger.LogInformation("GetActiveByUserIdAsync for user {UserId} returned {Count} sessions", userId, sessions.Count);
        return sessions.Select(MapToDto).ToList();
    }

    /// <inheritdoc />
    public async Task<bool> RevokeAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        logger.LogInformation("RevokeAsync called for session {SessionId}", sessionId);
        var result = await sessionRepository.RevokeAsync(sessionId, cancellationToken);
        logger.LogInformation("RevokeAsync for session {SessionId} returned {Result}", sessionId, result);
        return result;
    }

    /// <inheritdoc />
    public async Task<bool> RevokeForUserAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken)
    {
        var session = await sessionRepository.GetByIdAsync(sessionId, cancellationToken);
        if (session is null || session.UserId != userId)
        {
            return false;
        }
        return await sessionRepository.RevokeAsync(sessionId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<int> RevokeAllForUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await sessionRepository.RevokeAllForUserAsync(userId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<int> RevokeAllExceptAsync(Guid userId, Guid exceptSessionId, CancellationToken cancellationToken)
    {
        return await sessionRepository.RevokeAllExceptAsync(userId, exceptSessionId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<int> DeleteExpiredAsync(DateTimeOffset olderThan, CancellationToken cancellationToken)
    {
        return await sessionRepository.DeleteExpiredAsync(olderThan, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SessionDto?> ValidateSessionAsync(Guid sessionId, string? refreshTokenHash, CancellationToken cancellationToken)
    {
        var session = await sessionRepository.GetByIdAsync(sessionId, cancellationToken);
        if (session is null || !session.IsValid)
        {
            return null;
        }

        // If refresh token hash is provided, validate it
        if (refreshTokenHash is not null && !string.Equals(session.RefreshTokenHash, refreshTokenHash, StringComparison.Ordinal))
        {
            // Token mismatch - potential theft, revoke the session
            await sessionRepository.RevokeAsync(sessionId, cancellationToken);
            return null;
        }

        return MapToDto(session);
    }

    /// <inheritdoc />
    public Task<SessionDto?> ValidateSessionWithTokenAsync(Guid sessionId, string refreshToken, CancellationToken cancellationToken)
    {
        var tokenHash = Shared.Services.TokenHasher.Hash(refreshToken);
        return ValidateSessionAsync(sessionId, tokenHash, cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateRefreshTokenAsync(Guid sessionId, string newRefreshTokenHash, DateTimeOffset newExpiresAt, CancellationToken cancellationToken)
    {
        var session = await sessionRepository.GetByIdAsync(sessionId, cancellationToken);
        if (session is null)
        {
            throw new KeyNotFoundException($"Session with ID '{sessionId}' not found.");
        }

        session.RotateRefreshToken(newRefreshTokenHash, newExpiresAt);
        await sessionRepository.UpdateAsync(session, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Guid> CreateSessionAsync(
        Guid userId,
        string refreshTokenHash,
        DateTimeOffset expiresAt,
        string? deviceName,
        string? userAgent,
        string? ipAddress,
        Guid? sessionId,
        CancellationToken cancellationToken)
    {
        var loginSession = Domain.Identity.Models.LoginSession.Create(
            userId,
            refreshTokenHash,
            expiresAt,
            deviceName,
            userAgent,
            ipAddress);

        // If a session ID was provided, reconstruct with that ID
        if (sessionId.HasValue)
        {
            loginSession = Domain.Identity.Models.LoginSession.Reconstruct(
                sessionId.Value,
                loginSession.UserId,
                loginSession.RefreshTokenHash,
                loginSession.DeviceName,
                loginSession.UserAgent,
                loginSession.IpAddress,
                loginSession.CreatedAt,
                loginSession.LastUsedAt,
                loginSession.ExpiresAt,
                loginSession.IsRevoked,
                loginSession.RevokedAt);
        }

        await sessionRepository.CreateAsync(loginSession, cancellationToken);
        return loginSession.Id;
    }

    private static SessionDto MapToDto(Domain.Identity.Models.LoginSession session)
    {
        return new SessionDto
        {
            Id = session.Id,
            UserId = session.UserId,
            DeviceName = session.DeviceName,
            UserAgent = session.UserAgent,
            IpAddress = session.IpAddress,
            CreatedAt = session.CreatedAt,
            LastUsedAt = session.LastUsedAt,
            IsValid = session.IsValid,
            RevokedAt = session.RevokedAt
        };
    }
}
