using System;
using System.Collections.Generic;
using System.Linq;
using Domain.Authorization.ValueObjects;
using Domain.Shared.Exceptions;
using Domain.Shared.Models;

namespace Domain.Authorization.Models;

public sealed class Role : AggregateRoot
{
    private static readonly IReadOnlyDictionary<string, string?> EmptyParameters = new Dictionary<string, string?>();
    private readonly HashSet<RolePermissionTemplate> _permissionGrants = new();

    public string Code { get; private set; }
    public string Name { get; private set; }
    public string NormalizedName { get; private set; }
    public string? Description { get; private set; }
    public bool IsSystemRole { get; private set; }

    public IReadOnlyCollection<RolePermissionTemplate> PermissionGrants => _permissionGrants.ToList().AsReadOnly();

    private Role(Guid id, string code, string name, string? description, bool isSystemRole) : base(id)
    {
        Code = NormalizeCode(code);
        Name = NormalizeName(name);
        NormalizedName = name.ToUpperInvariant();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        IsSystemRole = isSystemRole;
    }

    public static Role Create(string code, string name, string? description = null, bool isSystemRole = false)
        => new(Guid.NewGuid(), code, name, description, isSystemRole);

    public void SetName(string name)
    {
        Name = NormalizeName(name);
        MarkAsModified();
    }

    public void SetNormalizedName(string normalizedName)
    {
        NormalizedName = normalizedName;
        MarkAsModified();
    }

    public void UpdateMetadata(string name, string? description)
    {
        Name = NormalizeName(name);
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        MarkAsModified();
    }

    public void PromoteToSystemRole()
    {
        if (!IsSystemRole)
        {
            IsSystemRole = true;
            MarkAsModified();
        }
    }

    public bool AssignPermission(RolePermissionTemplate grant)
    {
        ArgumentNullException.ThrowIfNull(grant);
        if (_permissionGrants.Add(grant))
        {
            MarkAsModified();
            return true;
        }

        return false;
    }

    public bool RemovePermission(string permissionIdentifier)
    {
        ArgumentException.ThrowIfNullOrEmpty(permissionIdentifier);
        var canonical = Permission.ParseIdentifier(permissionIdentifier).Canonical;
        var removed = _permissionGrants.RemoveWhere(grant =>
        {
            if (grant.RequiresParameters)
            {
                return false;
            }

            return string.Equals(grant.Expand(EmptyParameters), canonical, StringComparison.Ordinal);
        }) > 0;
        if (removed)
        {
            MarkAsModified();
        }

        return removed;
    }

    public void ReplacePermissions(IEnumerable<RolePermissionTemplate> grants)
    {
        if (grants is null)
        {
            throw new ArgumentNullException(nameof(grants));
        }

        var updated = new HashSet<RolePermissionTemplate>(grants);
        _permissionGrants.Clear();
        foreach (var grant in updated)
        {
            _permissionGrants.Add(grant);
        }
        MarkAsModified();
    }

    public IReadOnlyCollection<string> GetPermissionIdentifiers()
    {
        var identifiers = new List<string>(_permissionGrants.Count);
        foreach (var grant in _permissionGrants)
        {
            if (grant.RequiresParameters)
            {
                throw new DomainException($"Permission template '{grant.IdentifierTemplate}' requires parameters.");
            }

            identifiers.Add(grant.Expand(EmptyParameters));
        }

        return [.. identifiers
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static identifier => identifier, StringComparer.Ordinal)];
    }

    public IReadOnlyCollection<string> ExpandPermissions(IReadOnlyDictionary<string, string?> parameterValues)
    {
        ArgumentNullException.ThrowIfNull(parameterValues);

        return [.. _permissionGrants
            .Select(template => template.Expand(parameterValues))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static identifier => identifier, StringComparer.Ordinal)];
    }

    private static string NormalizeCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new DomainException("Role code cannot be null or empty.");
        }

        return code.Trim().ToUpperInvariant();
    }

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Role name cannot be null or empty.");
        }

        return name.Trim();
    }
}
