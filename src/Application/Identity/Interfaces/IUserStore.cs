using Domain.Identity.Models;

namespace Application.Identity.Interfaces;

public interface IUserStore
{
    Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<User?> FindByUsernameAsync(string username, CancellationToken cancellationToken);

    Task<User?> FindByExternalIdentityAsync(string provider, string providerSubject, CancellationToken cancellationToken);

    Task SaveAsync(User user, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<User>> ListAsync(CancellationToken cancellationToken);
}
