using Domain.Authorization.Models;

namespace Application.Authorization.Interfaces;

public interface IRoleLookup
{
    Task<Role?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<Role>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken);
}
