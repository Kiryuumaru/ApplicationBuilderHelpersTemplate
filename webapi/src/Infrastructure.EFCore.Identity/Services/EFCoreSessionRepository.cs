using Domain.Identity.Interfaces;
using Domain.Identity.Models;
using Infrastructure.EFCore.Extensions;
using Infrastructure.EFCore.Identity.Models;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.EFCore.Identity.Services;

/// <summary>
/// EF Core implementation of ISessionRepository using scoped DbContext.
/// Changes are tracked but not persisted until IIdentityUnitOfWork.CommitAsync() is called.
/// </summary>
internal sealed class EFCoreSessionRepository(EFCoreDbContext context) : ISessionRepository
{
    private readonly EFCoreDbContext _context = context;

    // Query methods

    public async Task<LoginSession?> GetByIdAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var entity = await _context.Set<LoginSessionEntity>().FindAsync([sessionId], cancellationToken);
        return entity is null ? null : MapToDomain(entity);
    }

    public async Task<IReadOnlyCollection<LoginSession>> GetActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        var entities = await _context.Set<LoginSessionEntity>()
            .Where(s => s.UserId == userId && !s.IsRevoked)
            .ToListAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        return entities
            .Where(s => s.ExpiresAt > now)
            .OrderByDescending(s => s.LastUsedAt)
            .Select(MapToDomain)
            .ToList();
    }

    // Change tracking methods - changes are persisted on UnitOfWork.CommitAsync()

    public void Add(LoginSession session)
    {
        var entity = MapToEntity(session);
        _context.Set<LoginSessionEntity>().Add(entity);
    }

    public void Update(LoginSession session)
    {
        var entity = _context.Set<LoginSessionEntity>().Local.FirstOrDefault(e => e.Id == session.Id);
        if (entity is not null)
        {
            entity.RefreshTokenHash = session.RefreshTokenHash;
            entity.LastUsedAt = session.LastUsedAt;
            entity.ExpiresAt = session.ExpiresAt;
            entity.IsRevoked = session.IsRevoked;
            entity.RevokedAt = session.RevokedAt;
        }
        else
        {
            entity = MapToEntity(session);
            _context.Set<LoginSessionEntity>().Attach(entity);
            _context.Entry(entity).State = EntityState.Modified;
        }
    }

    public void Remove(LoginSession session)
    {
        var entity = _context.Set<LoginSessionEntity>().Local.FirstOrDefault(e => e.Id == session.Id)
            ?? MapToEntity(session);
        _context.Set<LoginSessionEntity>().Remove(entity);
    }

    // Bulk operations - execute immediately for efficiency

    public async Task<int> RevokeAllForUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var sessions = await _context.Set<LoginSessionEntity>()
            .Where(s => s.UserId == userId && !s.IsRevoked)
            .ToListAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        foreach (var session in sessions)
        {
            session.IsRevoked = true;
            session.RevokedAt = now;
        }

        await _context.SaveChangesWithExceptionHandlingAsync(cancellationToken);
        return sessions.Count;
    }

    public async Task<int> RevokeAllExceptAsync(Guid userId, Guid exceptSessionId, CancellationToken cancellationToken)
    {
        var sessions = await _context.Set<LoginSessionEntity>()
            .Where(s => s.UserId == userId && !s.IsRevoked && s.Id != exceptSessionId)
            .ToListAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        foreach (var session in sessions)
        {
            session.IsRevoked = true;
            session.RevokedAt = now;
        }

        await _context.SaveChangesWithExceptionHandlingAsync(cancellationToken);
        return sessions.Count;
    }

    public async Task<int> DeleteExpiredAsync(DateTimeOffset olderThan, CancellationToken cancellationToken)
    {
        var expiredSessions = await _context.Set<LoginSessionEntity>()
            .ToListAsync(cancellationToken);

        var toDelete = expiredSessions
            .Where(s => (s.IsRevoked && s.RevokedAt < olderThan) || s.ExpiresAt < olderThan)
            .ToList();

        _context.Set<LoginSessionEntity>().RemoveRange(toDelete);
        await _context.SaveChangesWithExceptionHandlingAsync(cancellationToken);
        return toDelete.Count;
    }

    // Helper methods

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
            ExpiresAt = session.ExpiresAt,
            LastUsedAt = session.LastUsedAt,
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
