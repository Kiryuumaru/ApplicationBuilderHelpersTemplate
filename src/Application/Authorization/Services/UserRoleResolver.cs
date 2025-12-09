using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Application.Authorization.Interfaces;
using Domain.Identity.Models;

namespace Application.Authorization.Services;

internal sealed class UserRoleResolver(IRoleLookup roleLookup) : IUserRoleResolver
{
    private readonly IRoleLookup _roleLookup = roleLookup ?? throw new ArgumentNullException(nameof(roleLookup));

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

        var roles = await _roleLookup.GetByIdsAsync(distinctIds, cancellationToken).ConfigureAwait(false);
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
