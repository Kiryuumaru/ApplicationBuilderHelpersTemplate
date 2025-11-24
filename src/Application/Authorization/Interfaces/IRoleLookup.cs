using Domain.Authorization.Models;

namespace Application.Authorization.Roles.Interfaces;

public interface IRoleLookup
{
    Role? FindById(Guid id);

    IReadOnlyCollection<Role> GetByIds(IEnumerable<Guid> ids);
}
