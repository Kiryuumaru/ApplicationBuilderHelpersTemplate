using Application.Authorization.Interfaces;
using Application.Authorization.Interfaces.Infrastructure;
using Application.Identity.Interfaces;
using Application.Identity.Interfaces.Infrastructure;
using Application.Identity.Models;
using Domain.Authorization.Models;
using Domain.Identity.Models;

namespace Application.Identity.Services;

/// <summary>
/// Implementation of IUserRoleService using repositories directly.
/// </summary>
internal sealed class UserRoleService(
    IUserRepository userRepository,
    IRoleRepository roleRepository,
    IUserRoleResolver userRoleResolver) : IUserRoleService
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
}
