namespace Application.Identity.Models;

public sealed record UserRegistrationRequest(
    string Username,
    string Password,
    string? Email = null,
    IReadOnlyCollection<string>? PermissionIdentifiers = null,
    IReadOnlyCollection<RoleAssignmentRequest>? RoleAssignments = null,
    bool AutoActivate = true);
