using Domain.Identity.Models;

namespace Domain.Identity.Interfaces;

public interface IUserRoleResolver
{
    IReadOnlyCollection<UserRoleResolution> ResolveRoles(User user);
}
