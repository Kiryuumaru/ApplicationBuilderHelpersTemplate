using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Application.Authorization.Interfaces;
using Application.Authorization.Interfaces.Infrastructure;
using Domain.Authorization.Models;
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

    public async Task<IReadOnlyCollection<string>> ResolveRoleCodesAsync(User user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        cancellationToken.ThrowIfCancellationRequested();

        if (user.RoleAssignments.Count == 0)
        {
            return [];
        }

        var roleIds = user.RoleAssignments.Select(ra => ra.RoleId).Distinct();
        var roles = await _roleRepository.GetByIdsAsync(roleIds, cancellationToken).ConfigureAwait(false);

        return roles.Select(r => r.Code).ToArray();
    }

    public async Task<IReadOnlyCollection<string>> ResolveFormattedRoleClaimsAsync(User user, CancellationToken cancellationToken)
    {
        var roleResolutions = await ResolveRolesAsync(user, cancellationToken).ConfigureAwait(false);

        // Format roles with inline parameters: "USER;roleUserId=abc123"
        return roleResolutions
            .Select(r => Role.FormatRoleClaim(r.Role.Code, r.ParameterValues))
            .OrderBy(r => r, StringComparer.Ordinal)
            .ToArray();
    }
}
