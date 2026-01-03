using Application.Identity.Models;
using Domain.Identity.Enums;

namespace Application.Identity.Interfaces;

/// <summary>
/// Store for managing external login links between users and OAuth providers.
/// </summary>
public interface IExternalLoginStore
{
    /// <summary>
    /// Finds a user by their external login.
    /// </summary>
    /// <param name="provider">The OAuth provider.</param>
    /// <param name="providerKey">The user's unique key at the provider.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The user ID if found, null otherwise.</returns>
    Task<Guid?> FindUserByLoginAsync(
        ExternalLoginProvider provider,
        string providerKey,
        CancellationToken cancellationToken);

    /// <summary>
    /// Adds an external login link for a user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="provider">The OAuth provider.</param>
    /// <param name="providerKey">The user's unique key at the provider.</param>
    /// <param name="displayName">Display name for the linked account.</param>
    /// <param name="email">Email from the provider.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AddLoginAsync(
        Guid userId,
        ExternalLoginProvider provider,
        string providerKey,
        string? displayName,
        string? email,
        CancellationToken cancellationToken);

    /// <summary>
    /// Removes an external login link from a user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="provider">The OAuth provider.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the link was removed.</returns>
    Task<bool> RemoveLoginAsync(
        Guid userId,
        ExternalLoginProvider provider,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets all external logins for a user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of external login information.</returns>
    Task<IReadOnlyCollection<ExternalLoginInfo>> GetLoginsAsync(
        Guid userId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Checks if a user has any external login linked.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the user has at least one external login.</returns>
    Task<bool> HasAnyLoginAsync(Guid userId, CancellationToken cancellationToken);
}
