namespace Application.Authorization.Roles.Models;

public sealed record RoleDescriptor(
    string Code,
    string Name,
    string? Description,
    bool IsSystemRole,
    IReadOnlyCollection<RolePermissionTemplateDescriptor>? PermissionTemplates = null);
