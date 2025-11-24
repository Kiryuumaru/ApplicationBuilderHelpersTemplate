using System.Collections.Concurrent;
using System.Linq;
using Application.Authorization.Roles.Interfaces;
using Domain.Authorization.Models;

namespace Application.Authorization.Roles.Services;

internal sealed class InMemoryRoleRepository : IRoleRepository, IRoleLookup
{
    private readonly ConcurrentDictionary<Guid, Role> _roles = new();
    private readonly ConcurrentDictionary<string, Guid> _codeIndex = new(StringComparer.OrdinalIgnoreCase);

    public Task<Role?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(FindById(id));
    }

    public Task<Role?> GetByCodeAsync(string code, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(code))
        {
            return Task.FromResult<Role?>(null);
        }

        var normalized = code.Trim();
        if (_codeIndex.TryGetValue(normalized, out var roleId) && _roles.TryGetValue(roleId, out var role))
        {
            return Task.FromResult<Role?>(role);
        }

        return Task.FromResult<Role?>(null);
    }

    public Task<IReadOnlyCollection<Role>> ListAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var snapshot = _roles.Values
            .OrderBy(static role => role.Code, StringComparer.Ordinal)
            .ToArray();
        return Task.FromResult<IReadOnlyCollection<Role>>(snapshot);
    }

    public Task SaveAsync(Role role, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(role);

        _roles[role.Id] = role;
        _codeIndex[role.Code] = role.Id;
        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_roles.TryRemove(id, out var removed))
        {
            return Task.FromResult(false);
        }

        var code = removed.Code;
        if (!string.IsNullOrWhiteSpace(code))
        {
            _codeIndex.TryRemove(code, out _);
        }

        return Task.FromResult(true);
    }

    public Role? FindById(Guid id)
        => _roles.TryGetValue(id, out var role) ? role : null;

    public IReadOnlyCollection<Role> GetByIds(IEnumerable<Guid> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        var list = new List<Role>();
        foreach (var id in ids)
        {
            if (id == Guid.Empty)
            {
                continue;
            }

            if (_roles.TryGetValue(id, out var role))
            {
                list.Add(role);
            }
        }

        return list;
    }
}
