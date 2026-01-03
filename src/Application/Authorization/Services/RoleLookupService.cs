using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Authorization.Interfaces;
using Domain.Authorization.Constants;
using Domain.Authorization.Models;

namespace Application.Authorization.Services;

internal sealed class RoleLookupService(IRoleRepository? dynamicRepository = null) : IRoleLookup
{
    private readonly IRoleRepository? _dynamicRepository = dynamicRepository;

    public Task<Role?> FindByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Roles.TryGetById(id, out var staticRole))
        {
            return Task.FromResult<Role?>(staticRole);
        }

        return _dynamicRepository is not null
            ? _dynamicRepository.GetByIdAsync(id, cancellationToken)
            : Task.FromResult<Role?>(null);
    }

    public async Task<IReadOnlyCollection<Role>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ids);
        cancellationToken.ThrowIfCancellationRequested();

        var uniqueIds = new HashSet<Guid>(ids);
        var results = new Dictionary<Guid, Role>();
        var remaining = new List<Guid>();

        foreach (var id in uniqueIds)
        {
            if (Roles.TryGetById(id, out var staticRole))
            {
                results[id] = staticRole;
            }
            else
            {
                remaining.Add(id);
            }
        }

        if (remaining.Count > 0 && _dynamicRepository is not null)
        {
            var dynamicRoles = await _dynamicRepository.GetByIdsAsync(remaining, cancellationToken).ConfigureAwait(false);
            foreach (var role in dynamicRoles)
            {
                if (role is null)
                {
                    continue;
                }

                results[role.Id] = role;
            }
        }

        return results.Values.ToArray();
    }
}
