using Application.Authorization.Interfaces;
using Application.Authorization.Interfaces.Infrastructure;
using Domain.Identity.Models;

namespace Application.Authorization.Services;

internal sealed class UserRoleResolver(IRoleRepository roleRepository) : IUserRoleResolver
{
    private readonly IRoleRepository _roleRepository = roleRepository ?? throw new ArgumentNullException(nameof(roleRepository));

    public async Task<IReadOnlyCollection<UserRoleResolution>> ResolveRolesAsync(User user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        cancellationToken.ThrowIfCancellationRequested();
        if (user.RoleAssignments.Count == 0)
        {
            return Array.Empty<UserRoleResolution>();
        }

        var distinctIds = user.RoleAssignments
            .Select(static assignment => assignment.RoleId)
            .Distinct()
            .ToArray();

        var roles = await _roleRepository.GetByIdsAsync(distinctIds, cancellationToken).ConfigureAwait(false);
        var roleIndex = roles.ToDictionary(static role => role.Id);
        var resolutions = new List<UserRoleResolution>(user.RoleAssignments.Count);
        foreach (var assignment in user.RoleAssignments)
        {
            if (!roleIndex.TryGetValue(assignment.RoleId, out var role))
            {
                continue;
            }

            resolutions.Add(new UserRoleResolution(role, assignment.ParameterValues));
        }

        return resolutions;
    }
}
