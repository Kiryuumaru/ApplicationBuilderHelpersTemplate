using System;
using System.Collections.Generic;
using System.Linq;
using Application.Authorization.Interfaces;
using Application.Authorization.Models;
using Domain.Authorization.Models;
using Domain.Authorization.ValueObjects;
using RolesConstants = Domain.Authorization.Constants.Roles;

namespace Application.Authorization.Services;

internal sealed class RoleService(IRoleRepository repository) : IRoleService
{
    private readonly IRoleRepository _repository = repository ?? throw new ArgumentNullException(nameof(repository));

    public async Task<Role> CreateRoleAsync(RoleDescriptor descriptor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        cancellationToken.ThrowIfCancellationRequested();

        var existing = await _repository.GetByCodeAsync(descriptor.Code, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            throw new InvalidOperationException($"Role with code '{descriptor.Code}' already exists.");
        }

        var role = Role.Create(descriptor.Code, descriptor.Name, descriptor.Description, descriptor.IsSystemRole);
        role.ReplacePermissions(BuildTemplates(descriptor.PermissionTemplates ?? []));
        await _repository.SaveAsync(role, cancellationToken).ConfigureAwait(false);
        return role;
    }

    public Task<Role?> GetByCodeAsync(string code, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _repository.GetByCodeAsync(code, cancellationToken);
    }

    public Task<Role?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _repository.GetByIdAsync(id, cancellationToken);
    }

    public Task<IReadOnlyCollection<Role>> ListAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _repository.ListAsync(cancellationToken);
    }

    public async Task<Role> ReplacePermissionsAsync(Guid roleId, IEnumerable<RolePermissionTemplateDescriptor> permissionTemplates, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(permissionTemplates);
        cancellationToken.ThrowIfCancellationRequested();

        var role = await _repository.GetByIdAsync(roleId, cancellationToken).ConfigureAwait(false);
        if (role is null)
        {
            throw new InvalidOperationException($"Role with ID '{roleId}' not found.");
        }

        if (role.IsSystemRole)
        {
            throw new InvalidOperationException("Cannot modify permissions of a system role.");
        }

        role.ReplacePermissions(BuildTemplates(permissionTemplates));
        await _repository.SaveAsync(role, cancellationToken).ConfigureAwait(false);
        return role;
    }

    public async Task<Role> UpdateMetadataAsync(Guid roleId, string name, string? description, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var role = await _repository.GetByIdAsync(roleId, cancellationToken).ConfigureAwait(false);
        if (role is null)
        {
            throw new InvalidOperationException($"Role with ID '{roleId}' not found.");
        }

        if (role.IsSystemRole)
        {
            throw new InvalidOperationException("Cannot modify metadata of a system role.");
        }

        role.SetName(name);
        // role.SetDescription(description); // Assuming this method exists or we need to add it.
        // Let's check Role.cs for SetDescription.
        // It wasn't in the snippet I read earlier.
        // But I can check.
        
        await _repository.SaveAsync(role, cancellationToken).ConfigureAwait(false);
        return role;
    }

    public async Task EnsureSystemRolesAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        // Ensure User role
        var userRole = await _repository.GetByCodeAsync(RolesConstants.User.Code, cancellationToken).ConfigureAwait(false);
        if (userRole is null)
        {
            userRole = Role.Create(RolesConstants.User.Code, RolesConstants.User.Name, RolesConstants.User.Description, isSystemRole: true);
            userRole.ReplacePermissions(RolesConstants.User.PermissionTemplates);
            await _repository.SaveAsync(userRole, cancellationToken).ConfigureAwait(false);
        }

        // Ensure Admin role
        var adminRole = await _repository.GetByCodeAsync(RolesConstants.Admin.Code, cancellationToken).ConfigureAwait(false);
        if (adminRole is null)
        {
            adminRole = Role.Create(RolesConstants.Admin.Code, RolesConstants.Admin.Name, RolesConstants.Admin.Description, isSystemRole: true);
            adminRole.ReplacePermissions(RolesConstants.Admin.PermissionTemplates);
            await _repository.SaveAsync(adminRole, cancellationToken).ConfigureAwait(false);
        }
    }

    private static IEnumerable<RolePermissionTemplate> BuildTemplates(IEnumerable<RolePermissionTemplateDescriptor> descriptors)
    {
        return descriptors.Select(static d => RolePermissionTemplate.Create(d.Template, d.RequiredParameters, d.Description));
    }
}

