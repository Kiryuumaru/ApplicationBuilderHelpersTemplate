using System;
using System.Collections.Generic;
using System.Linq;
using Domain.Authorization.ValueObjects;
using Domain.Shared.Exceptions;
using Domain.Shared.Models;

namespace Domain.Authorization.Models;

public sealed class Role : AggregateRoot
{
    private readonly List<ScopeTemplate> _scopeTemplates = new();

    public string Code { get; private set; }
    public string Name { get; private set; }
    public string NormalizedName { get; private set; }
    public string? Description { get; private set; }
    public bool IsSystemRole { get; private set; }

    public IReadOnlyCollection<ScopeTemplate> ScopeTemplates => _scopeTemplates.AsReadOnly();

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

    /// <summary>
    /// Factory method for hydrating a Role from persistence. AOT-compatible.
    /// </summary>
    public static Role Hydrate(Guid id, Guid? revId, string code, string name, string? description, bool isSystemRole)
    {
        var role = new Role(id, code, name, description, isSystemRole);
        if (revId.HasValue)
        {
            role.RevId = revId.Value;
        }
        return role;
    }

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

    /// <summary>
    /// Expands all scope templates into scope directives using the provided parameter values.
    /// </summary>
    public IReadOnlyCollection<ScopeDirective> ExpandScope(IReadOnlyDictionary<string, string?> parameterValues)
    {
        ArgumentNullException.ThrowIfNull(parameterValues);

        return [.. _scopeTemplates
            .Select(template => template.Expand(parameterValues))
            .Distinct()
            .OrderBy(static directive => directive.ToString(), StringComparer.Ordinal)];
    }

    /// <summary>
    /// Replaces all scope templates with the provided collection.
    /// </summary>
    public void ReplaceScopeTemplates(IEnumerable<ScopeTemplate> templates)
    {
        ArgumentNullException.ThrowIfNull(templates);

        _scopeTemplates.Clear();
        _scopeTemplates.AddRange(templates);
        MarkAsModified();
    }

    /// <summary>
    /// Adds a scope template to the role.
    /// </summary>
    public void AddScopeTemplate(ScopeTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);
        _scopeTemplates.Add(template);
        MarkAsModified();
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
