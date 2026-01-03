using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Domain.Authorization.Models;

namespace Domain.Identity.Models;

public sealed class UserRoleResolution(Role role, IReadOnlyDictionary<string, string?>? parameterValues = null)
{
    private static readonly IReadOnlyDictionary<string, string?> EmptyParameters =
        new ReadOnlyDictionary<string, string?>(new Dictionary<string, string?>(StringComparer.Ordinal));

    public Role Role { get; } = role ?? throw new ArgumentNullException(nameof(role));
    public IReadOnlyDictionary<string, string?> ParameterValues { get; } = NormalizeParameters(parameterValues);

    private static IReadOnlyDictionary<string, string?> NormalizeParameters(IReadOnlyDictionary<string, string?>? parameterValues)
    {
        if (parameterValues is null || parameterValues.Count == 0)
        {
            return EmptyParameters;
        }

        if (parameterValues is ReadOnlyDictionary<string, string?>)
        {
            return parameterValues;
        }

        var dictionary = new Dictionary<string, string?>(parameterValues.Count, StringComparer.Ordinal);
        foreach (var pair in parameterValues)
        {
            dictionary[pair.Key] = pair.Value;
        }

        return new ReadOnlyDictionary<string, string?>(dictionary);
    }
}
