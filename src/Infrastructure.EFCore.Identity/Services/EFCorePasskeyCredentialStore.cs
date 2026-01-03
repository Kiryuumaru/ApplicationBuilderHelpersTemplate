using Application.Identity.Interfaces;
using Domain.Identity.Models;
using Infrastructure.EFCore.Identity.Models;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.EFCore.Identity.Services;

/// <summary>
/// EF Core implementation of the passkey credential store.
/// </summary>
public class EFCorePasskeyCredentialStore(EFCoreDbContext dbContext) : IPasskeyCredentialStore
{
    private readonly EFCoreDbContext _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    private DbSet<PasskeyCredentialEntity> Credentials => _dbContext.Set<PasskeyCredentialEntity>();

    public async Task SaveAsync(PasskeyCredential credential, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(credential);

        var entity = MapToEntity(credential);
        Credentials.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<PasskeyCredential>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var entities = await Credentials
            .Where(c => c.UserId == userId)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<PasskeyCredential?> GetByIdAsync(Guid credentialId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var entity = await Credentials.FindAsync([credentialId], cancellationToken);
        return entity == null ? null : MapToDomain(entity);
    }

    public async Task<PasskeyCredential?> GetByCredentialIdAsync(byte[] credentialId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(credentialId);

        var entity = await Credentials
            .FirstOrDefaultAsync(c => c.CredentialId == credentialId, cancellationToken);

        return entity == null ? null : MapToDomain(entity);
    }

    public async Task<IReadOnlyCollection<PasskeyCredential>> GetByCredentialIdsAsync(IEnumerable<byte[]> credentialIds, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(credentialIds);

        var idList = credentialIds.ToList();
        if (idList.Count == 0)
            return [];

        // Note: EF Core may not support Contains for byte arrays directly
        // We'll fetch all and filter in memory for now
        var entities = await Credentials.ToListAsync(cancellationToken);
        var matching = entities
            .Where(e => idList.Any(id => id.SequenceEqual(e.CredentialId)))
            .Select(MapToDomain)
            .ToList();

        return matching;
    }

    public async Task<IReadOnlyCollection<PasskeyCredential>> GetByUserHandleAsync(byte[] userHandle, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(userHandle);

        var entities = await Credentials
            .Where(c => c.UserHandle == userHandle)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task UpdateAsync(PasskeyCredential credential, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(credential);

        var entity = await Credentials.FindAsync([credential.Id], cancellationToken);
        if (entity == null)
            throw new InvalidOperationException($"Passkey credential {credential.Id} not found");

        // Update mutable fields
        entity.Name = credential.Name;
        entity.SignCount = credential.SignCount;
        entity.LastUsedAt = credential.LastUsedAt;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid credentialId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var entity = await Credentials.FindAsync([credentialId], cancellationToken);
        if (entity != null)
        {
            Credentials.Remove(entity);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static PasskeyCredentialEntity MapToEntity(PasskeyCredential credential)
    {
        return new PasskeyCredentialEntity
        {
            Id = credential.Id,
            UserId = credential.UserId,
            Name = credential.Name,
            CredentialId = credential.CredentialId,
            PublicKey = credential.PublicKey,
            SignCount = credential.SignCount,
            AaGuid = credential.AaGuid,
            CredentialType = credential.CredentialType,
            RegisteredAt = credential.RegisteredAt,
            LastUsedAt = credential.LastUsedAt,
            UserHandle = credential.UserHandle,
            AttestationFormat = credential.AttestationFormat,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    private static PasskeyCredential MapToDomain(PasskeyCredentialEntity entity)
    {
        return PasskeyCredential.Reconstruct(
            entity.Id,
            entity.UserId,
            entity.Name,
            entity.CredentialId,
            entity.PublicKey,
            entity.SignCount,
            entity.AaGuid,
            entity.CredentialType,
            entity.UserHandle,
            entity.AttestationFormat,
            entity.RegisteredAt,
            entity.LastUsedAt);
    }
}
