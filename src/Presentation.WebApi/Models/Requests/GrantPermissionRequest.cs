using System.ComponentModel.DataAnnotations;

namespace Presentation.WebApi.Models.Requests;

/// <summary>
/// Request to grant a direct permission to a user.
/// </summary>
public sealed class GrantPermissionRequest
{
    /// <summary>
    /// Gets or sets the permission identifier to grant.
    /// </summary>
    [Required]
    public required string PermissionIdentifier { get; init; }

    /// <summary>
    /// Gets or sets an optional description for why the permission was granted.
    /// </summary>
    public string? Description { get; init; }
}
