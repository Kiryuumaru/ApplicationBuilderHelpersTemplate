using Domain.Authorization.Models;

namespace Application.Authorization.Interfaces;

public interface IRoleRepository
{
    Task<Role?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Role?> GetByCodeAsync(string code, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<Role>> ListAsync(CancellationToken cancellationToken);

    Task SaveAsync(Role role, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken);
}
