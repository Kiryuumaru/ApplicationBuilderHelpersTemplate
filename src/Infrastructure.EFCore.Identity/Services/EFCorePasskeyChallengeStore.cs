using Application.Identity.Interfaces;
using Domain.Identity.Enums;
using Domain.Identity.Models;
using Infrastructure.EFCore.Identity.Models;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.EFCore.Identity.Services;

/// <summary>
/// EF Core implementation of the passkey challenge store.
/// </summary>
public class EFCorePasskeyChallengeStore(EFCoreDbContext dbContext) : IPasskeyChallengeStore
{
    private readonly EFCoreDbContext _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    private DbSet<PasskeyChallengeEntity> Challenges => _dbContext.Set<PasskeyChallengeEntity>();

    public async Task SaveAsync(PasskeyChallenge challenge, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(challenge);

        var entity = MapToEntity(challenge);
        Challenges.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<PasskeyChallenge?> GetByIdAsync(Guid challengeId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var entity = await Challenges.FindAsync([challengeId], cancellationToken);
        return entity == null ? null : MapToDomain(entity);
    }

    public async Task DeleteAsync(Guid challengeId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var entity = await Challenges.FindAsync([challengeId], cancellationToken);
        if (entity != null)
        {
            Challenges.Remove(entity);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task DeleteExpiredAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var now = DateTimeOffset.UtcNow;
        var expired = await Challenges
            .Where(c => c.ExpiresAt <= now)
            .ToListAsync(cancellationToken);

        if (expired.Count > 0)
        {
            Challenges.RemoveRange(expired);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static PasskeyChallengeEntity MapToEntity(PasskeyChallenge challenge)
    {
        return new PasskeyChallengeEntity
        {
            Id = challenge.Id,
            Challenge = challenge.Challenge,
            UserId = challenge.UserId,
            Type = (int)challenge.Type,
            OptionsJson = challenge.OptionsJson,
            CreatedAt = challenge.CreatedAt,
            ExpiresAt = challenge.ExpiresAt
        };
    }

    private static PasskeyChallenge MapToDomain(PasskeyChallengeEntity entity)
    {
        return PasskeyChallenge.Reconstruct(
            entity.Id,
            entity.Challenge,
            entity.UserId,
            (PasskeyChallengeType)entity.Type,
            entity.OptionsJson,
            entity.CreatedAt,
            entity.ExpiresAt);
    }
}
