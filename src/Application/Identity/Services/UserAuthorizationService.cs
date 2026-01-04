using Application.Authorization.Interfaces;
using Application.Authorization.Interfaces.Infrastructure;
using Application.Identity.Interfaces;
using Application.Identity.Interfaces.Infrastructure;
using Application.Identity.Models;
using Domain.Authorization.Models;
using Domain.Identity.Models;
using Domain.Identity.ValueObjects;

namespace Application.Identity.Services;

/// <summary>
/// Implementation of IUserAuthorizationService using repositories directly.
/// </summary>
internal sealed class UserAuthorizationService(
    IUserRepository userRepository,
    IRoleRepository roleRepository,
    IUserRoleResolver userRoleResolver) : IUserAuthorizationService
{
    private readonly IUserRepository _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
    private readonly IRoleRepository _roleRepository = roleRepository ?? throw new ArgumentNullException(nameof(roleRepository));
    private readonly IUserRoleResolver _userRoleResolver = userRoleResolver ?? throw new ArgumentNullException(nameof(userRoleResolver));

    public async Task AssignRoleAsync(Guid userId, RoleAssignmentRequest assignment, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(assignment);

        var user = await _userRepository.FindByIdAsync(userId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"User with ID {userId} not found.");

        var role = await _roleRepository.GetByCodeAsync(assignment.RoleCode, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Role with code '{assignment.RoleCode}' not found.");

        // Validate required parameters
        ValidateRoleParameters(role, assignment.ParameterValues);

        user.AssignRole(role.Id, assignment.ParameterValues);
        await _userRepository.SaveAsync(user, cancellationToken).ConfigureAwait(false);
    }

    public async Task RemoveRoleAsync(Guid userId, Guid roleId, CancellationToken cancellationToken)
    {
        var user = await _userRepository.FindByIdAsync(userId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"User with ID {userId} not found.");

        user.RemoveRole(roleId);
        await _userRepository.SaveAsync(user, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyCollection<string>> GetEffectivePermissionsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _userRepository.FindByIdAsync(userId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"User with ID {userId} not found.");

        var roleResolutions = await _userRoleResolver.ResolveRolesAsync(user, cancellationToken).ConfigureAwait(false);

        var permissions = new HashSet<string>(StringComparer.Ordinal);

        // Add direct permission grants from the user
        foreach (var grant in user.PermissionGrants)
        {
            // Direct grants are added as "allow" directives
            permissions.Add($"allow;{grant.Identifier}");
        }

        // Add permissions from roles
        foreach (var resolution in roleResolutions)
        {
            var scope = resolution.Role.ExpandScope(resolution.ParameterValues);
            foreach (var directive in scope)
            {
                permissions.Add(directive.ToString());
            }
        }

        return [.. permissions.OrderBy(p => p, StringComparer.Ordinal)];
    }

    public async Task GrantPermissionAsync(Guid userId, string permissionIdentifier, string? description, string? grantedBy, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(permissionIdentifier);

        var user = await _userRepository.FindByIdAsync(userId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"User with ID {userId} not found.");

        var grant = UserPermissionGrant.Create(permissionIdentifier, description, grantedBy);
        user.GrantPermission(grant);
        await _userRepository.SaveAsync(user, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> RevokePermissionAsync(Guid userId, string permissionIdentifier, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(permissionIdentifier);

        var user = await _userRepository.FindByIdAsync(userId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"User with ID {userId} not found.");

        var revoked = user.RevokePermission(permissionIdentifier);
        if (revoked)
        {
            await _userRepository.SaveAsync(user, cancellationToken).ConfigureAwait(false);
        }

        return revoked;
    }

    private static void ValidateRoleParameters(Domain.Authorization.Models.Role role, IReadOnlyDictionary<string, string?>? providedParameters)
    {
        // Collect all required parameters from the role's scope templates
        var requiredParameters = new HashSet<string>(StringComparer.Ordinal);
        foreach (var scopeTemplate in role.ScopeTemplates)
        {
            foreach (var requiredParam in scopeTemplate.RequiredParameters)
            {
                requiredParameters.Add(requiredParam);
            }
        }

        if (requiredParameters.Count == 0)
        {
            return; // No parameters required
        }

        // Check that all required parameters are provided
        var providedKeys = providedParameters?.Keys ?? [];
        var missingParameters = requiredParameters.Where(p => !providedKeys.Contains(p)).ToList();

        if (missingParameters.Count > 0)
        {
            throw new InvalidOperationException(
                $"Role '{role.Code}' requires parameters [{string.Join(", ", missingParameters)}] but they were not provided.");
        }
    }
}
