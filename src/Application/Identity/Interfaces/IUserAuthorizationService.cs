using Application.Identity.Models;

namespace Application.Identity.Interfaces;

/// <summary>
/// Service for user authorization management operations (roles and permissions).
/// Split from IIdentityService to follow single responsibility principle.
/// </summary>
public interface IUserAuthorizationService
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

    /// <summary>
    /// Grants a direct permission to a user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="permissionIdentifier">The permission identifier to grant.</param>
    /// <param name="description">Optional description for the grant.</param>
    /// <param name="grantedBy">Optional identifier of who granted the permission.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task GrantPermissionAsync(Guid userId, string permissionIdentifier, string? description, string? grantedBy, CancellationToken cancellationToken);

    /// <summary>
    /// Revokes a direct permission from a user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="permissionIdentifier">The permission identifier to revoke.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the permission was revoked, false if it was not found.</returns>
    Task<bool> RevokePermissionAsync(Guid userId, string permissionIdentifier, CancellationToken cancellationToken);
}
