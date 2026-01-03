using Domain.Identity.Models;

namespace Application.Identity.Interfaces;

public interface IUserStore
{
    Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<User?> FindByUsernameAsync(string username, CancellationToken cancellationToken);

    Task<User?> FindByExternalIdentityAsync(string provider, string providerSubject, CancellationToken cancellationToken);

    Task SaveAsync(User user, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<User>> ListAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Deletes anonymous users who have not been active since the specified cutoff date.
    /// </summary>
    /// <param name="cutoffDate">Users inactive before this date will be deleted.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of users deleted.</returns>
    Task<int> DeleteAbandonedAnonymousUsersAsync(DateTimeOffset cutoffDate, CancellationToken cancellationToken);
}
