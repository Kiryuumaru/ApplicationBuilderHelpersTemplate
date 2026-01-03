using Application.Identity.Interfaces;
using Domain.Identity.Models;
using Infrastructure.EFCore.Identity.Models;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.EFCore.Identity.Services;

/// <summary>
/// EF Core implementation of the session store.
/// </summary>
public class EFCoreSessionStore(EFCoreDbContext dbContext) : ISessionStore
{
    private readonly EFCoreDbContext _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    private DbSet<LoginSessionEntity> Sessions => _dbContext.Set<LoginSessionEntity>();

    public async Task CreateAsync(LoginSession session, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(session);

        var entity = MapToEntity(session);
        Sessions.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<LoginSession?> GetByIdAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var entity = await Sessions.FindAsync([sessionId], cancellationToken);
        return entity == null ? null : MapToDomain(entity);
    }

    public async Task<IReadOnlyCollection<LoginSession>> GetActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Load user's non-revoked sessions, then filter and sort in memory
        // This is necessary because SQLite doesn't handle DateTimeOffset in WHERE or ORDER BY clauses
        var entities = await Sessions
            .Where(s => s.UserId == userId && !s.IsRevoked)
            .ToListAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        return entities
            .Where(s => s.ExpiresAt > now)
            .OrderByDescending(s => s.LastUsedAt)
            .Select(MapToDomain)
            .ToList();
    }

    public async Task UpdateAsync(LoginSession session, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(session);

        var entity = await Sessions.FindAsync([session.Id], cancellationToken);
        if (entity == null)
        {
            throw new InvalidOperationException($"Session with ID {session.Id} not found.");
        }

        // Update the entity with current session values
        entity.RefreshTokenHash = session.RefreshTokenHash;
        entity.LastUsedAt = session.LastUsedAt;
        entity.ExpiresAt = session.ExpiresAt;
        entity.IsRevoked = session.IsRevoked;
        entity.RevokedAt = session.RevokedAt;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> RevokeAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var entity = await Sessions.FindAsync([sessionId], cancellationToken);
        if (entity == null || entity.IsRevoked)
        {
            return false;
        }

        entity.IsRevoked = true;
        entity.RevokedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<int> RevokeAllForUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var now = DateTimeOffset.UtcNow;
        var sessions = await Sessions
            .Where(s => s.UserId == userId && !s.IsRevoked)
            .ToListAsync(cancellationToken);

        foreach (var session in sessions)
        {
            session.IsRevoked = true;
            session.RevokedAt = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return sessions.Count;
    }

    public async Task<int> RevokeAllExceptAsync(Guid userId, Guid exceptSessionId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var now = DateTimeOffset.UtcNow;
        var sessions = await Sessions
            .Where(s => s.UserId == userId && !s.IsRevoked && s.Id != exceptSessionId)
            .ToListAsync(cancellationToken);

        foreach (var session in sessions)
        {
            session.IsRevoked = true;
            session.RevokedAt = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return sessions.Count;
    }

    public async Task<int> DeleteExpiredAsync(DateTimeOffset olderThan, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Load all sessions, filter in memory due to SQLite DateTimeOffset limitations
        var allSessions = await Sessions.ToListAsync(cancellationToken);
        var sessions = allSessions
            .Where(s => s.ExpiresAt < olderThan || (s.IsRevoked && s.RevokedAt < olderThan))
            .ToList();

        if (sessions.Count > 0)
        {
            Sessions.RemoveRange(sessions);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return sessions.Count;
    }

    private static LoginSessionEntity MapToEntity(LoginSession session)
    {
        return new LoginSessionEntity
        {
            Id = session.Id,
            UserId = session.UserId,
            RefreshTokenHash = session.RefreshTokenHash,
            DeviceName = session.DeviceName,
            UserAgent = session.UserAgent,
            IpAddress = session.IpAddress,
            CreatedAt = session.CreatedAt,
            LastUsedAt = session.LastUsedAt,
            ExpiresAt = session.ExpiresAt,
            IsRevoked = session.IsRevoked,
            RevokedAt = session.RevokedAt
        };
    }

    private static LoginSession MapToDomain(LoginSessionEntity entity)
    {
        return LoginSession.Reconstruct(
            entity.Id,
            entity.UserId,
            entity.RefreshTokenHash,
            entity.DeviceName,
            entity.UserAgent,
            entity.IpAddress,
            entity.CreatedAt,
            entity.LastUsedAt,
            entity.ExpiresAt,
            entity.IsRevoked,
            entity.RevokedAt);
    }
}
