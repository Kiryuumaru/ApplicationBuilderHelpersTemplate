using Domain.Authorization.Models;
using Domain.Shared.Exceptions;
using Domain.Shared.Models;

namespace Domain.Identity.ValueObjects;

public sealed class UserPermissionGrant : ValueObject
{
    public string Identifier { get; }
    public string? Description { get; }
    public DateTimeOffset GrantedAt { get; }
    public string? GrantedBy { get; }

    private UserPermissionGrant(string identifier, string? description, DateTimeOffset grantedAt, string? grantedBy)
    {
        Identifier = Normalize(identifier);
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        GrantedAt = grantedAt;
        GrantedBy = string.IsNullOrWhiteSpace(grantedBy) ? null : grantedBy.Trim();
    }

    public static UserPermissionGrant Create(string identifier, string? description = null, string? grantedBy = null, DateTimeOffset? grantedAt = null)
    {
        var timestamp = grantedAt ?? DateTimeOffset.UtcNow;
        return new UserPermissionGrant(identifier, description, timestamp, grantedBy);
    }

    public static UserPermissionGrant FromPermission(Permission permission, IReadOnlyDictionary<string, string?>? parameters = null, string? description = null, string? grantedBy = null, DateTimeOffset? grantedAt = null)
    {
        if (permission is null)
        {
            throw new ArgumentNullException(nameof(permission));
        }

        var identifier = permission.BuildPath(parameters);
        return Create(identifier, description ?? permission.Description, grantedBy, grantedAt);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Identifier;
    }

    private static string Normalize(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            throw new DomainException("Permission identifier cannot be null or empty.");
        }

        var parsed = Permission.ParseIdentifier(identifier.Trim());
        var normalized = string.Join(':', parsed.Identifier
            .Split(':', StringSplitOptions.RemoveEmptyEntries)
            .Select(segment => segment.Trim())
            .Where(static segment => segment.Length > 0));

        if (normalized.Length == 0)
        {
            throw new DomainException("Permission identifier canonical form could not be determined.");
        }

        return normalized;
    }
}
