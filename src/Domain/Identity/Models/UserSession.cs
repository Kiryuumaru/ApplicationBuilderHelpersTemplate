using Domain.Shared.Models;

namespace Domain.Identity.Models;

public sealed class UserSession : ValueObject
{
    public Guid UserId { get; }
    public string Username { get; }
    public IReadOnlyCollection<string> PermissionIdentifiers { get; }
    public IReadOnlyCollection<string> RoleCodes { get; }
    public DateTimeOffset IssuedAt { get; }
    public DateTimeOffset ExpiresAt { get; }

    private UserSession(
        Guid userId,
        string username,
        IReadOnlyCollection<string> permissionIdentifiers,
        IReadOnlyCollection<string> roleCodes,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt)
    {
        UserId = userId;
        Username = username;
        PermissionIdentifiers = permissionIdentifiers;
        RoleCodes = roleCodes;
        IssuedAt = issuedAt;
        ExpiresAt = expiresAt;
    }

    public static UserSession Create(
        Guid userId,
        string username,
        IEnumerable<string> permissionIdentifiers,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt,
        IEnumerable<string>? roleCodes = null)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User identifier cannot be empty.", nameof(userId));
        }

        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException("Username cannot be empty.", nameof(username));
        }

        if (expiresAt <= issuedAt)
        {
            throw new ArgumentException("Session expiration must be greater than issuance time.", nameof(expiresAt));
        }

        var permissions = permissionIdentifiers?
            .Where(static p => !string.IsNullOrWhiteSpace(p))
            .Select(static p => p.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static p => p, StringComparer.Ordinal)
            .ToArray() ?? Array.Empty<string>();
        var roles = roleCodes?
            .Where(static code => !string.IsNullOrWhiteSpace(code))
            .Select(static code => code.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static code => code, StringComparer.Ordinal)
            .ToArray() ?? Array.Empty<string>();
        return new UserSession(userId, username.Trim(), permissions, roles, issuedAt, expiresAt);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return UserId;
        yield return Username;
        yield return IssuedAt;
        yield return ExpiresAt;
    }
}
