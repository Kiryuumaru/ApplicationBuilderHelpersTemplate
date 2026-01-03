using System.Collections.Generic;

namespace Application.Identity.Models;

public sealed record ExternalUserRegistrationRequest(
    string Username,
    string Provider,
    string ProviderSubject,
    string? ProviderEmail = null,
    string? ProviderDisplayName = null,
    string? Email = null,
    IReadOnlyCollection<string>? PermissionIdentifiers = null,
    IReadOnlyCollection<RoleAssignmentRequest>? RoleAssignments = null,
    bool AutoActivate = true);
