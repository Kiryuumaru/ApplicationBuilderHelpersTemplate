using System;
using System.Collections.Generic;
using System.Linq;
using Application.Authorization.Roles.Interfaces;
using Application.Authorization.Roles.Models;
using Domain.Authorization.Models;
using Domain.Authorization.ValueObjects;
using RolesConstants = Domain.Authorization.Constants.Roles;

namespace Application.Authorization.Roles.Services;

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
        role.ReplacePermissions(BuildTemplates(descriptor.PermissionTemplates));
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

        var role = await RequireRoleAsync(roleId, cancellationToken).ConfigureAwait(false);
        role.ReplacePermissions(BuildTemplates(permissionTemplates));
        await _repository.SaveAsync(role, cancellationToken).ConfigureAwait(false);
        return role;
    }

    public async Task<Role> UpdateMetadataAsync(Guid roleId, string name, string? description, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var role = await RequireRoleAsync(roleId, cancellationToken).ConfigureAwait(false);
        role.UpdateMetadata(name, description);
        await _repository.SaveAsync(role, cancellationToken).ConfigureAwait(false);
        return role;
    }

    public async Task EnsureSystemRolesAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var definition in RolesConstants.All)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var existing = await _repository.GetByCodeAsync(definition.Code, cancellationToken).ConfigureAwait(false);
            if (existing is null)
            {
                await _repository.SaveAsync(definition.Instantiate(), cancellationToken).ConfigureAwait(false);
                continue;
            }

            existing.UpdateMetadata(definition.Name, definition.Description);
            existing.ReplacePermissions(definition.PermissionTemplates);
            if (definition.IsSystemRole)
            {
                existing.PromoteToSystemRole();
            }

            await _repository.SaveAsync(existing, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<Role> RequireRoleAsync(Guid roleId, CancellationToken cancellationToken)
    {
        var role = await _repository.GetByIdAsync(roleId, cancellationToken).ConfigureAwait(false);
        if (role is null)
        {
            throw new KeyNotFoundException($"Role '{roleId}' was not found.");
        }

        return role;
    }

    private static IReadOnlyCollection<RolePermissionTemplate> BuildTemplates(IEnumerable<RolePermissionTemplateDescriptor>? descriptors)
    {
        if (descriptors is null)
        {
            return Array.Empty<RolePermissionTemplate>();
        }

        var templates = new List<RolePermissionTemplate>();
        foreach (var descriptor in descriptors)
        {
            if (descriptor is null || string.IsNullOrWhiteSpace(descriptor.Template))
            {
                continue;
            }

            templates.Add(RolePermissionTemplate.Create(
                descriptor.Template,
                descriptor.RequiredParameters,
                descriptor.Description));
        }

        return templates.Count == 0
            ? Array.Empty<RolePermissionTemplate>()
            : templates.ToArray();
    }
}
