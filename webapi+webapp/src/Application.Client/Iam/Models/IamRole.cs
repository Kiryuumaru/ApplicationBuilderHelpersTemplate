namespace Application.Client.Iam.Models;

/// <summary>
/// Represents role information from IAM.
/// </summary>
public class IamRole
{
    /// <summary>
    /// Gets or sets the role ID.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the role code (unique identifier).
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the role name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the role description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets whether this is a system role (cannot be modified/deleted).
    /// </summary>
    public bool IsSystemRole { get; set; }

    /// <summary>
    /// Gets or sets the scope templates defining role permissions.
    /// </summary>
    public List<ScopeTemplateInfo> ScopeTemplates { get; set; } = new();
}

/// <summary>
/// Represents a scope template within a role.
/// </summary>
public class ScopeTemplateInfo
{
    /// <summary>
    /// Gets or sets the permission identifier.
    /// </summary>
    public string PermissionIdentifier { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether this is an allow (true) or deny (false) grant.
    /// </summary>
    public bool IsAllow { get; set; }

    /// <summary>
    /// Gets or sets the parameter template values.
    /// </summary>
    public Dictionary<string, string> ParameterTemplates { get; set; } = new();
}

/// <summary>
/// Request to create a new role.
/// </summary>
public class CreateRoleRequest
{
    /// <summary>
    /// Gets or sets the role code (unique identifier).
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the role name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the role description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the scope templates defining role permissions.
    /// </summary>
    public List<ScopeTemplateRequest> ScopeTemplates { get; set; } = new();
}

/// <summary>
/// Request to update a role.
/// </summary>
public class UpdateRoleRequest
{
    /// <summary>
    /// Gets or sets the role name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the role description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the scope templates (if provided, replaces existing).
    /// </summary>
    public List<ScopeTemplateRequest>? ScopeTemplates { get; set; }
}

/// <summary>
/// Request to define a scope template.
/// </summary>
public class ScopeTemplateRequest
{
    /// <summary>
    /// Gets or sets the permission identifier.
    /// </summary>
    public string PermissionIdentifier { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether this is an allow (true) or deny (false) grant.
    /// </summary>
    public bool IsAllow { get; set; } = true;

    /// <summary>
    /// Gets or sets the parameter template values.
    /// </summary>
    public Dictionary<string, string> ParameterTemplates { get; set; } = new();
}

/// <summary>
/// Request to assign a role to a user.
/// </summary>
public class AssignRoleRequest
{
    /// <summary>
    /// Gets or sets the user ID.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets the role code to assign.
    /// </summary>
    public string RoleCode { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets parameter values for scope template placeholders.
    /// </summary>
    public Dictionary<string, string>? ParameterValues { get; set; }
}

/// <summary>
/// Request to unassign a role from a user.
/// </summary>
public class UnassignRoleRequest
{
    /// <summary>
    /// Gets or sets the user ID.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets the role ID to unassign.
    /// </summary>
    public Guid RoleId { get; set; }
}
