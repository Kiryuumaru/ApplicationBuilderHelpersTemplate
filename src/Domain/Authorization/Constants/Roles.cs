using System;
using System.Collections.Generic;
using System.Linq;
using Domain.Authorization.Models;
using Domain.Authorization.ValueObjects;

namespace Domain.Authorization.Constants;

public static class Roles
{
    public static RoleDefinition Admin { get; }
    public static RoleDefinition User { get; }
    public static IReadOnlyList<RoleDefinition> All { get; }

    static Roles()
    {
        Admin = new RoleDefinition(
            Code: "admin",
            Name: "Administrator",
            Description: "Full access to all platform capabilities.",
            IsSystemRole: true,
            PermissionTemplates:
            [
                RolePermissionTemplate.Create(Permissions.RootReadIdentifier),
                RolePermissionTemplate.Create(Permissions.RootWriteIdentifier)
            ]);

        User = new RoleDefinition(
            Code: "user",
            Name: "User",
            Description: "Default role for authenticated users accessing their own data.",
            IsSystemRole: true,
            TemplateParametersOverride: ["roleUserId"],
            PermissionTemplates:
            [
                RolePermissionTemplate.Create("api:[userId={roleUserId}]:_read"),
                RolePermissionTemplate.Create("api:[userId={roleUserId}]:_write")
            ]);

        All = [Admin, User];
    }

    public sealed record RoleDefinition(
        string Code,
        string Name,
        string? Description,
        bool IsSystemRole,
        IReadOnlyList<RolePermissionTemplate> PermissionTemplates,
        IReadOnlyList<string>? TemplateParametersOverride = null)
    {
        public IReadOnlyList<string> TemplateParameters { get; init; } = TemplateParametersOverride ?? Array.Empty<string>();

        public Role Instantiate()
        {
            var role = Models.Role.Create(Code, Name, Description, IsSystemRole);
            role.ReplacePermissions(PermissionTemplates);
            return role;
        }
    }
}
