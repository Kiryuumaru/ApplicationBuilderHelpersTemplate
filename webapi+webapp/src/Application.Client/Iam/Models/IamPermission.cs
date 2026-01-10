namespace Application.Client.Iam.Models;

/// <summary>
/// Represents permission information from IAM.
/// </summary>
public class IamPermission
{
    /// <summary>
    /// Gets or sets the permission path/identifier.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the permission identifier.
    /// </summary>
    public string Identifier { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the permission description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the parameter names for this permission.
    /// </summary>
    public List<string> Parameters { get; set; } = new();

    /// <summary>
    /// Gets or sets child permissions in the hierarchy.
    /// </summary>
    public List<IamPermission> Children { get; set; } = new();
}

/// <summary>
/// Request to grant a direct permission to a user.
/// </summary>
public class GrantPermissionRequest
{
    /// <summary>
    /// Gets or sets the user ID.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets the permission identifier.
    /// </summary>
    public string PermissionIdentifier { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether this is an allow (true) or deny (false) grant.
    /// </summary>
    public bool IsAllow { get; set; } = true;

    /// <summary>
    /// Gets or sets a description for the grant.
    /// </summary>
    public string? Description { get; set; }
}

/// <summary>
/// Request to revoke a direct permission from a user.
/// </summary>
public class RevokePermissionRequest
{
    /// <summary>
    /// Gets or sets the user ID.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets the permission identifier.
    /// </summary>
    public string PermissionIdentifier { get; set; } = string.Empty;
}
