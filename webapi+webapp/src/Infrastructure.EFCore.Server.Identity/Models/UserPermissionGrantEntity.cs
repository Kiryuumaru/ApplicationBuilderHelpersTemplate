using Domain.Authorization.Enums;

namespace Infrastructure.EFCore.Server.Identity.Models;

/// <summary>
/// EF Core entity for storing direct permission grants for users.
/// </summary>
public class UserPermissionGrantEntity
{
    /// <summary>
    /// The user ID this permission grant belongs to.
    /// </summary>
    public required Guid UserId { get; set; }

    /// <summary>
    /// The type of grant (Allow or Deny).
    /// </summary>
    public required ScopeDirectiveType Type { get; set; }

    /// <summary>
    /// The permission identifier (e.g., "api:users:read").
    /// </summary>
    public required string PermissionIdentifier { get; set; }

    /// <summary>
    /// Optional description of why this permission was granted.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// When this permission was granted.
    /// </summary>
    public required DateTimeOffset GrantedAt { get; set; }

    /// <summary>
    /// Who granted this permission (user ID or system identifier).
    /// </summary>
    public string? GrantedBy { get; set; }
}
