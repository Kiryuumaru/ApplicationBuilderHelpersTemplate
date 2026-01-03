using Domain.Identity.Models;

namespace Application.Identity.Interfaces;

/// <summary>
/// Store for managing passkey challenges during WebAuthn operations.
/// </summary>
public interface IPasskeyChallengeStore
{
    /// <summary>
    /// Saves a challenge for later verification.
    /// </summary>
    Task SaveAsync(PasskeyChallenge challenge, CancellationToken cancellationToken);

    /// <summary>
    /// Gets a challenge by its ID.
    /// </summary>
    Task<PasskeyChallenge?> GetByIdAsync(Guid challengeId, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes a challenge (typically after successful verification or expiration).
    /// </summary>
    Task DeleteAsync(Guid challengeId, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes all expired challenges (for cleanup).
    /// </summary>
    Task DeleteExpiredAsync(CancellationToken cancellationToken);
}
