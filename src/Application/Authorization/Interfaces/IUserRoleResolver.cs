using Domain.Identity.Models;

namespace Application.Authorization.Interfaces;

public interface IUserRoleResolver
{
    Task<IReadOnlyCollection<UserRoleResolution>> ResolveRolesAsync(User user, CancellationToken cancellationToken);
}
