using Application.Identity.Models;

namespace Application.Identity.Interfaces;

/// <summary>
/// Service for user role management operations.
/// Split from IIdentityService to follow single responsibility principle.
/// </summary>
public interface IUserRoleService
{
    /// <summary>
    /// Assigns a role to a user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="assignment">The role assignment request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AssignRoleAsync(Guid userId, RoleAssignmentRequest assignment, CancellationToken cancellationToken);

    /// <summary>
    /// Removes a role from a user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="roleId">The role ID to remove.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RemoveRoleAsync(Guid userId, Guid roleId, CancellationToken cancellationToken);

    /// <summary>
    /// Gets all effective permissions for a user (from all assigned roles).
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of permission identifiers.</returns>
    Task<IReadOnlyCollection<string>> GetEffectivePermissionsAsync(Guid userId, CancellationToken cancellationToken);
}
