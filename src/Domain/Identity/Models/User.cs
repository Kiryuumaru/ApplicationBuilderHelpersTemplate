using System;
using System.Collections.Generic;
using System.Linq;
using Domain.Authorization.Models;
using Domain.Identity.Enums;
using Domain.Identity.ValueObjects;
using Domain.Shared.Exceptions;
using Domain.Shared.Models;

namespace Domain.Identity.Models;

public sealed class User : AggregateRoot
{
    private const int DefaultLockoutThreshold = 5;
    private static readonly TimeSpan DefaultLockoutDuration = TimeSpan.FromMinutes(15);

    private readonly HashSet<UserPermissionGrant> _permissionGrants = new();
    private readonly HashSet<UserRoleAssignment> _roleAssignments = new();
    private readonly Dictionary<string, UserIdentityLink> _identityLinks = new(StringComparer.Ordinal);

    public string Username { get; private set; }
    public string? Email { get; private set; }
    public bool IsEmailVerified { get; private set; }
    public UserStatus Status { get; private set; }
    public PasswordCredential? Credential { get; private set; }
    public bool RequiresPasswordReset { get; private set; }
    public DateTimeOffset? LockedUntil { get; private set; }
    public DateTimeOffset? LastLoginAt { get; private set; }
    public DateTimeOffset? LastFailedLoginAt { get; private set; }
    public int ConsecutiveFailedLoginCount { get; private set; }

    public IReadOnlyCollection<UserPermissionGrant> PermissionGrants => _permissionGrants.ToList().AsReadOnly();
    public IReadOnlyCollection<Guid> RoleIds => _roleAssignments
        .Select(static assignment => assignment.RoleId)
        .Distinct()
        .ToArray();
    public IReadOnlyCollection<UserRoleAssignment> RoleAssignments => _roleAssignments.ToList().AsReadOnly();
    public IReadOnlyCollection<UserIdentityLink> IdentityLinks => _identityLinks.Values.ToList().AsReadOnly();
    public bool HasPasswordCredential => Credential is not null;

    private User(string username, string? email, PasswordCredential? credential)
    {
        Username = NormalizeUsername(username);
        Email = string.IsNullOrWhiteSpace(email) ? null : NormalizeEmail(email);
        Credential = credential;
        Status = UserStatus.PendingActivation;
    }

    public static User Register(string username, string? email = null, PasswordCredential? credential = null)
        => new(username, email, credential);

    public static User RegisterExternal(
        string username,
        string provider,
        string providerSubject,
        string? providerEmail = null,
        string? displayName = null,
        string? email = null)
    {
        var user = new User(username, email, credential: null);
        user.LinkIdentityInternal(provider, providerSubject, providerEmail, displayName, DateTimeOffset.UtcNow, markModified: false);
        return user;
    }

    public void MarkEmailVerified()
    {
        if (Email is null)
        {
            throw new DomainException("Cannot verify email when no email is set.");
        }

        if (!IsEmailVerified)
        {
            IsEmailVerified = true;
            MarkAsModified();
        }
    }

    public void SetEmail(string email, bool markVerified = false)
    {
        var normalized = NormalizeEmail(email);
        if (!string.Equals(Email, normalized, StringComparison.OrdinalIgnoreCase) || markVerified != IsEmailVerified)
        {
            Email = normalized;
            IsEmailVerified = markVerified;
            MarkAsModified();
        }
    }

    public void ClearEmail()
    {
        if (Email is not null || IsEmailVerified)
        {
            Email = null;
            IsEmailVerified = false;
            MarkAsModified();
        }
    }

    public void Activate()
    {
        EnsureNotDeactivated();
        Status = UserStatus.Active;
        LockedUntil = null;
        MarkAsModified();
    }

    public void Suspend(string? reason = null)
    {
        EnsureNotDeactivated();
        Status = UserStatus.Suspended;
        LockedUntil = null;
        RequiresPasswordReset = false;
        MarkAsModified();
    }

    public void Deactivate()
    {
        Status = UserStatus.Deactivated;
        LockedUntil = null;
        RequiresPasswordReset = false;
        MarkAsModified();
    }

    public void Unlock()
    {
        if (Status == UserStatus.Locked)
        {
            Status = UserStatus.Active;
            LockedUntil = null;
            ConsecutiveFailedLoginCount = 0;
            MarkAsModified();
        }
    }

    public void GrantPermission(UserPermissionGrant grant)
    {
        ArgumentNullException.ThrowIfNull(grant);
        if (_permissionGrants.Add(grant))
        {
            MarkAsModified();
        }
    }

    public bool RevokePermission(string permissionIdentifier)
    {
        ArgumentException.ThrowIfNullOrEmpty(permissionIdentifier);
        var canonical = permissionIdentifier.Trim();
        var removed = _permissionGrants.RemoveWhere(grant => string.Equals(grant.Identifier, canonical, StringComparison.Ordinal)) > 0;
        if (removed)
        {
            MarkAsModified();
        }

        return removed;
    }

    public IReadOnlyCollection<string> GetPermissionIdentifiers()
        => _permissionGrants
            .Select(grant => grant.Identifier)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(identifier => identifier, StringComparer.Ordinal)
            .ToArray();

    public IReadOnlyCollection<string> BuildEffectivePermissions(IEnumerable<UserRoleResolution>? roleResolutions)
    {
        var identifiers = new HashSet<string>(StringComparer.Ordinal);
        foreach (var permission in GetPermissionIdentifiers())
        {
            identifiers.Add(permission);
        }

        if (roleResolutions is not null)
        {
            foreach (var resolution in roleResolutions)
            {
                if (resolution?.Role is null)
                {
                    continue;
                }

                foreach (var identifier in resolution.Role.ExpandPermissions(resolution.ParameterValues))
                {
                    identifiers.Add(identifier);
                }
            }
        }

        return identifiers.OrderBy(static id => id, StringComparer.Ordinal).ToArray();
    }

    public bool AssignRole(Guid roleId, IReadOnlyDictionary<string, string?>? parameterValues = null)
    {
        if (roleId == Guid.Empty)
        {
            throw new DomainException("Role identifier cannot be empty.");
        }

        var assignment = UserRoleAssignment.Create(roleId, parameterValues);
        if (_roleAssignments.Add(assignment))
        {
            MarkAsModified();
            return true;
        }

        return false;
    }

    public bool RemoveRole(Guid roleId)
    {
        if (roleId == Guid.Empty)
        {
            throw new DomainException("Role identifier cannot be empty.");
        }

        var removed = _roleAssignments.RemoveWhere(assignment => assignment.RoleId == roleId) > 0;
        if (removed)
        {
            MarkAsModified();
            return true;
        }

        return false;
    }

    public void ClearRoles()
    {
        if (_roleAssignments.Count > 0)
        {
            _roleAssignments.Clear();
            MarkAsModified();
        }
    }

    public UserIdentityLink LinkIdentity(
        string provider,
        string providerSubject,
        string? email = null,
        string? displayName = null,
        DateTimeOffset? linkedAt = null)
        => LinkIdentityInternal(provider, providerSubject, email, displayName, linkedAt ?? DateTimeOffset.UtcNow, markModified: true);

    public bool UnlinkIdentity(string provider, string providerSubject)
    {
        var key = BuildIdentityKey(UserIdentityLink.NormalizeProvider(provider), UserIdentityLink.NormalizeSubject(providerSubject));
        var removed = _identityLinks.Remove(key);
        if (removed)
        {
            MarkAsModified();
        }

        return removed;
    }

    public bool HasIdentity(string provider, string providerSubject)
    {
        var key = BuildIdentityKey(UserIdentityLink.NormalizeProvider(provider), UserIdentityLink.NormalizeSubject(providerSubject));
        return _identityLinks.ContainsKey(key);
    }

    public UserIdentityLink? GetIdentity(string provider, string providerSubject)
    {
        var key = BuildIdentityKey(UserIdentityLink.NormalizeProvider(provider), UserIdentityLink.NormalizeSubject(providerSubject));
        return _identityLinks.TryGetValue(key, out var identity) ? identity : null;
    }

    public void SetPasswordCredential(PasswordCredential credential, bool requireReset = false)
    {
        Credential = credential ?? throw new ArgumentNullException(nameof(credential));
        RequiresPasswordReset = requireReset;
        MarkAsModified();
    }

    public void RemovePasswordCredential()
    {
        if (Credential is not null)
        {
            Credential = null;
            RequiresPasswordReset = false;
            MarkAsModified();
        }
    }

    public void RecordSuccessfulLogin(DateTimeOffset timestamp)
    {
        LastLoginAt = timestamp;
        ConsecutiveFailedLoginCount = 0;
        LockedUntil = null;
        RequiresPasswordReset = false;
        if (Status == UserStatus.PendingActivation)
        {
            Status = UserStatus.Active;
        }

        MarkAsModified();
    }

    public void RecordFailedLogin(DateTimeOffset timestamp, int lockoutThreshold = DefaultLockoutThreshold)
    {
        ConsecutiveFailedLoginCount++;
        LastFailedLoginAt = timestamp;
        if (ConsecutiveFailedLoginCount >= Math.Max(1, lockoutThreshold))
        {
            Status = UserStatus.Locked;
            LockedUntil = timestamp + DefaultLockoutDuration;
        }

        MarkAsModified();
    }

    public bool CanAuthenticate(DateTimeOffset timestamp)
    {
        if (Status == UserStatus.Deactivated || Status == UserStatus.Suspended)
        {
            return false;
        }

        if (Status == UserStatus.Locked && LockedUntil is not null && timestamp < LockedUntil)
        {
            return false;
        }

        return Status is UserStatus.Active or UserStatus.PendingActivation;
    }

    public UserSession CreateSession(TimeSpan lifetime, DateTimeOffset? issuedAt = null, IEnumerable<string>? permissionIdentifiers = null, IEnumerable<string>? roleCodes = null)
    {
        if (lifetime <= TimeSpan.Zero)
        {
            throw new DomainException("Session lifetime must be positive.");
        }

        var timestamp = issuedAt ?? DateTimeOffset.UtcNow;
        if (!CanAuthenticate(timestamp))
        {
            throw new DomainException("User cannot authenticate in the current state.");
        }

        var permissions = permissionIdentifiers ?? GetPermissionIdentifiers();
        var codes = roleCodes ?? Array.Empty<string>();
        return UserSession.Create(Id, Username, permissions, timestamp, timestamp + lifetime, codes);
    }

    private UserIdentityLink LinkIdentityInternal(
        string provider,
        string providerSubject,
        string? email,
        string? displayName,
        DateTimeOffset linkedAt,
        bool markModified)
    {
        var identity = UserIdentityLink.Create(provider, providerSubject, email, displayName, linkedAt);
        var key = BuildIdentityKey(identity.Provider, identity.Subject);
        _identityLinks[key] = identity;
        if (markModified)
        {
            MarkAsModified();
        }

        return identity;
    }

    private static string BuildIdentityKey(string provider, string subject)
        => $"{provider}::{subject}";

    private void EnsureNotDeactivated()
    {
        if (Status == UserStatus.Deactivated)
        {
            throw new DomainException("Cannot change state on a deactivated user.");
        }
    }

    private static string NormalizeUsername(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new DomainException("Username cannot be null or empty.");
        }

        return username.Trim();
    }

    private static string NormalizeEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new DomainException("Email cannot be null or empty.");
        }

        return email.Trim().ToLowerInvariant();
    }
}
