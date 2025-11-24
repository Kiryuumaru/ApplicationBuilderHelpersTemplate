using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Domain.Shared.Exceptions;
using Domain.Shared.Models;

namespace Domain.Identity.ValueObjects;

public sealed class UserRoleAssignment : ValueObject
{
    private static readonly IReadOnlyDictionary<string, string?> EmptyParameters =
        new ReadOnlyDictionary<string, string?>(new Dictionary<string, string?>(StringComparer.Ordinal));

    public Guid RoleId { get; }
    public IReadOnlyDictionary<string, string?> ParameterValues { get; }

    private UserRoleAssignment(Guid roleId, IReadOnlyDictionary<string, string?> parameterValues)
    {
        if (roleId == Guid.Empty)
        {
            throw new DomainException("Role identifier cannot be empty.");
        }

        RoleId = roleId;
        ParameterValues = parameterValues;
    }

    public static UserRoleAssignment Create(Guid roleId, IReadOnlyDictionary<string, string?>? parameterValues = null)
        => new(roleId, NormalizeParameters(parameterValues));

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return RoleId;
        foreach (var kvp in ParameterValues.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            yield return kvp.Key;
            yield return kvp.Value ?? string.Empty;
        }
    }

    private static IReadOnlyDictionary<string, string?> NormalizeParameters(IReadOnlyDictionary<string, string?>? parameterValues)
    {
        if (parameterValues is null || parameterValues.Count == 0)
        {
            return EmptyParameters;
        }

        var normalized = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var kvp in parameterValues)
        {
            if (string.IsNullOrWhiteSpace(kvp.Key))
            {
                continue;
            }

            var key = kvp.Key.Trim();
            if (normalized.ContainsKey(key))
            {
                continue;
            }

            normalized[key] = kvp.Value?.Trim();
        }

        return normalized.Count == 0
            ? EmptyParameters
            : new ReadOnlyDictionary<string, string?>(normalized);
    }
}
