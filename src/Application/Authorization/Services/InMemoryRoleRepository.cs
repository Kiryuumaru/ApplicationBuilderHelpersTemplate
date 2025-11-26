using System.Collections.Concurrent;
using Application.Authorization.Roles.Interfaces;
using Domain.Authorization.Models;
using Microsoft.AspNetCore.Identity;

namespace Application.Authorization.Roles.Services;

internal sealed class InMemoryRoleRepository : IRoleRepository, IRoleLookup, IRoleStore<Role>
{
    private readonly ConcurrentDictionary<Guid, Role> _roles = new();
    private readonly ConcurrentDictionary<string, Guid> _codeIndex = new(StringComparer.OrdinalIgnoreCase);

    // IRoleRepository implementation
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

    // IRoleLookup implementation
    public Role? FindById(Guid id)
        => _roles.TryGetValue(id, out var role) ? role : null;

    public IReadOnlyCollection<Role> GetByIds(IEnumerable<Guid> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        var list = new List<Role>();
        foreach (var id in ids)
        {
            if (_roles.TryGetValue(id, out var role))
            {
                list.Add(role);
            }
        }

        return list;
    }

    // IRoleStore<Role> implementation
    public Task<IdentityResult> CreateAsync(Role role, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(role);
        
        if (!_roles.TryAdd(role.Id, role))
        {
             return Task.FromResult(IdentityResult.Failed(new IdentityError { Description = "Role already exists." }));
        }
        _codeIndex[role.Code] = role.Id;
        return Task.FromResult(IdentityResult.Success);
    }

    public Task<IdentityResult> UpdateAsync(Role role, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(role);

        _roles[role.Id] = role;
        _codeIndex[role.Code] = role.Id;
        return Task.FromResult(IdentityResult.Success);
    }

    public Task<IdentityResult> DeleteAsync(Role role, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(role);
        
        return DeleteAsync(role.Id, cancellationToken).ContinueWith(t => t.Result ? IdentityResult.Success : IdentityResult.Failed(new IdentityError { Description = "Failed to delete role." }));
    }

    public Task<Role?> FindByIdAsync(string roleId, CancellationToken cancellationToken)
    {
        if (Guid.TryParse(roleId, out var guid))
        {
            return GetByIdAsync(guid, cancellationToken);
        }
        return Task.FromResult<Role?>(null);
    }

    public Task<Role?> FindByNameAsync(string normalizedRoleName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var role = _roles.Values.FirstOrDefault(r => r.NormalizedName == normalizedRoleName);
        return Task.FromResult(role);
    }

    public Task<string?> GetNormalizedRoleNameAsync(Role role, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(role);
        return Task.FromResult<string?>(role.NormalizedName);
    }

    public Task<string> GetRoleIdAsync(Role role, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(role);
        return Task.FromResult(role.Id.ToString());
    }

    public Task<string?> GetRoleNameAsync(Role role, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(role);
        return Task.FromResult<string?>(role.Name);
    }

    public Task SetNormalizedRoleNameAsync(Role role, string? normalizedName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(role);
        if (normalizedName != null)
        {
            role.SetNormalizedName(normalizedName);
        }
        return Task.CompletedTask;
    }

    public Task SetRoleNameAsync(Role role, string? roleName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(role);
        if (roleName != null)
        {
            role.SetName(roleName);
        }
        return Task.CompletedTask;
    }

    public void Dispose()
    {
    }
}
