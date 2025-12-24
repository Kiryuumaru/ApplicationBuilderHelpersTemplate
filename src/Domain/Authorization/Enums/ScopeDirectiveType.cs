namespace Domain.Authorization.Enums;

/// <summary>
/// Defines the type of a scope directive.
/// </summary>
public enum ScopeDirectiveType
{
    /// <summary>
    /// Grants access to a permission path.
    /// </summary>
    Allow = 0,

    /// <summary>
    /// Revokes access to a permission path.
    /// </summary>
    Deny = 1
}
