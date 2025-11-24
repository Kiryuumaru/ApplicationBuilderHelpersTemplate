using System;
using System.Collections.Generic;
using System.Linq;
using Application.Authorization.Roles.Interfaces;
using Domain.Authorization.Models;
using Domain.Identity.Interfaces;
using Domain.Identity.Models;

namespace Application.Authorization.Roles.Services;

internal sealed class UserRoleResolver(IRoleLookup roleLookup) : IUserRoleResolver
{
    private readonly IRoleLookup _roleLookup = roleLookup ?? throw new ArgumentNullException(nameof(roleLookup));

    public IReadOnlyCollection<UserRoleResolution> ResolveRoles(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (user.RoleAssignments.Count == 0)
        {
            return Array.Empty<UserRoleResolution>();
        }

        var distinctIds = user.RoleAssignments
            .Select(static assignment => assignment.RoleId)
            .Distinct()
            .ToArray();

        var roleIndex = _roleLookup.GetByIds(distinctIds).ToDictionary(static role => role.Id);
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
