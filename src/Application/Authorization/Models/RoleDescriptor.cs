namespace Application.Authorization.Models;

public sealed record RoleDescriptor(
    string Code,
    string Name,
    string? Description,
    bool IsSystemRole,
    IReadOnlyCollection<RolePermissionTemplateDescriptor>? PermissionTemplates = null);
