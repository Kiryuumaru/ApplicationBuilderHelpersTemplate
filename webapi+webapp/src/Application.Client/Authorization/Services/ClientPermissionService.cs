using Application.Client.Authentication.Interfaces;
using Application.Client.Authorization.Interfaces;
using Domain.Authorization.Services;
using Domain.Authorization.ValueObjects;

namespace Application.Client.Authorization.Services;

/// <summary>
/// Client-side permission service that evaluates permissions from the current auth state.
/// Uses the Domain layer's ScopeEvaluator for permission evaluation.
/// </summary>
public sealed class ClientPermissionService(IAuthStateProvider authStateProvider) : IClientPermissionService
{
    /// <inheritdoc />
    public bool HasPermission(string permissionPath)
    {
        var authState = authStateProvider.CurrentState;

        if (!authState.IsAuthenticated)
        {
            return false;
        }

        var directives = ParseDirectives(authState.Permissions);
        return ScopeEvaluator.HasPermission(directives, permissionPath);
    }

    /// <inheritdoc />
    public bool HasAnyPermission(params string[] permissionPaths)
    {
        if (permissionPaths is null || permissionPaths.Length == 0)
        {
            return false;
        }

        var authState = authStateProvider.CurrentState;

        if (!authState.IsAuthenticated)
        {
            return false;
        }

        var directives = ParseDirectives(authState.Permissions);
        return ScopeEvaluator.HasAnyPermission(directives, permissionPaths);
    }

    /// <inheritdoc />
    public bool HasAllPermissions(params string[] permissionPaths)
    {
        if (permissionPaths is null || permissionPaths.Length == 0)
        {
            return false;
        }

        var authState = authStateProvider.CurrentState;

        if (!authState.IsAuthenticated)
        {
            return false;
        }

        var directives = ParseDirectives(authState.Permissions);
        return ScopeEvaluator.HasAllPermissions(directives, permissionPaths);
    }

    private static List<ScopeDirective> ParseDirectives(IReadOnlyList<string>? permissions)
    {
        if (permissions is null || permissions.Count == 0)
        {
            return [];
        }

        var directives = new List<ScopeDirective>(permissions.Count);

        foreach (var permission in permissions)
        {
            if (ScopeDirective.TryParse(permission, out var directive) && directive is not null)
            {
                directives.Add(directive);
            }
        }

        return directives;
    }
}
