using System.Collections.Generic;

namespace Application.Authorization.Models;

public sealed record RolePermissionTemplateDescriptor(
    string Template,
    IReadOnlyCollection<string>? RequiredParameters = null,
    string? Description = null);
