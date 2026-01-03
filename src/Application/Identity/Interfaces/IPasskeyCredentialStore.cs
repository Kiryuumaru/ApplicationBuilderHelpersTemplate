using Domain.Identity.Models;

namespace Application.Identity.Interfaces;

/// <summary>
/// Legacy store for managing registered passkey credentials.
/// Use IPasskeyRepository from Application.Identity.Interfaces.Infrastructure instead.
/// </summary>
[Obsolete("Use IPasskeyRepository from Application.Identity.Interfaces.Infrastructure instead. This interface will be removed in a future version.")]
public interface IPasskeyCredentialStore
{
    /// <summary>
    /// Saves a new passkey credential.
    /// </summary>
    Task SaveAsync(PasskeyCredential credential, CancellationToken cancellationToken);

    /// <summary>
    /// Gets all passkey credentials for a user.
    /// </summary>
    Task<IReadOnlyCollection<PasskeyCredential>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Gets a credential by its ID.
    /// </summary>
    Task<PasskeyCredential?> GetByIdAsync(Guid credentialId, CancellationToken cancellationToken);

    /// <summary>
    /// Gets a credential by its credential ID (the authenticator-assigned ID).
    /// </summary>
    Task<PasskeyCredential?> GetByCredentialIdAsync(byte[] credentialId, CancellationToken cancellationToken);

    /// <summary>
    /// Gets all credentials matching any of the given credential IDs.
    /// </summary>
    Task<IReadOnlyCollection<PasskeyCredential>> GetByCredentialIdsAsync(IEnumerable<byte[]> credentialIds, CancellationToken cancellationToken);

    /// <summary>
    /// Gets a credential by user handle (for discoverable credentials).
    /// </summary>
    Task<IReadOnlyCollection<PasskeyCredential>> GetByUserHandleAsync(byte[] userHandle, CancellationToken cancellationToken);

    /// <summary>
    /// Updates an existing credential (e.g., sign count, last used).
    /// </summary>
    Task UpdateAsync(PasskeyCredential credential, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes a passkey credential.
    /// </summary>
    Task DeleteAsync(Guid credentialId, CancellationToken cancellationToken);
}
