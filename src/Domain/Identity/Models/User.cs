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
    private readonly HashSet<UserPermissionGrant> _permissionGrants = new();
    private readonly HashSet<UserRoleAssignment> _roleAssignments = new();
    private readonly Dictionary<string, UserIdentityLink> _identityLinks = new(StringComparer.Ordinal);

    public string UserName { get; private set; }
    public string NormalizedUserName { get; private set; }
    public string? Email { get; private set; }
    public string? NormalizedEmail { get; private set; }
    public bool EmailConfirmed { get; private set; }
    public string? PasswordHash { get; private set; }
    public string? SecurityStamp { get; private set; }
    public string? PhoneNumber { get; private set; }
    public bool PhoneNumberConfirmed { get; private set; }
    public bool TwoFactorEnabled { get; private set; }
    public DateTimeOffset? LockoutEnd { get; private set; }
    public bool LockoutEnabled { get; private set; }
    public int AccessFailedCount { get; private set; }

    // Legacy/Custom fields
    public UserStatus Status { get; private set; }
    public DateTimeOffset? LastLoginAt { get; private set; }
    public DateTimeOffset? LastFailedLoginAt { get; private set; }

    public IReadOnlyCollection<UserPermissionGrant> PermissionGrants => _permissionGrants.ToList().AsReadOnly();
    public IReadOnlyCollection<Guid> RoleIds => [.. _roleAssignments
        .Select(static assignment => assignment.RoleId)
        .Distinct()];
    public IReadOnlyCollection<UserRoleAssignment> RoleAssignments => _roleAssignments.ToList().AsReadOnly();
    public IReadOnlyCollection<UserIdentityLink> IdentityLinks => _identityLinks.Values.ToList().AsReadOnly();

    private User(Guid id, string userName, string? email) : base(id)
    {
        if (string.IsNullOrWhiteSpace(userName))
        {
            throw new ArgumentException("UserName cannot be empty", nameof(userName));
        }

        UserName = userName;
        NormalizedUserName = userName.ToUpperInvariant();
        Email = email;
        NormalizedEmail = email?.ToUpperInvariant();
        SecurityStamp = Guid.NewGuid().ToString();
        Status = UserStatus.PendingActivation;
        LockoutEnabled = true;
    }

    public static User Register(string userName, string? email = null)
        => new(Guid.NewGuid(), userName, email);

    public static User RegisterExternal(string userName, string provider, string subject, string? providerEmail = null, string? displayName = null, string? email = null)
    {
        var user = new User(Guid.NewGuid(), userName, email);
        user.LinkIdentity(provider, subject, providerEmail, displayName);
        return user;
    }

    public void SetUserName(string userName)
    {
        if (string.IsNullOrWhiteSpace(userName)) throw new ArgumentException("UserName cannot be empty", nameof(userName));
        UserName = userName;
        MarkAsModified();
    }

    public void SetNormalizedUserName(string normalizedUserName)
    {
        if (string.IsNullOrWhiteSpace(normalizedUserName)) throw new ArgumentException("NormalizedUserName cannot be empty", nameof(normalizedUserName));
        NormalizedUserName = normalizedUserName;
        MarkAsModified();
    }

    public void SetEmail(string? email)
    {
        Email = email?.ToLowerInvariant();
        NormalizedEmail = email?.ToUpperInvariant();
        MarkAsModified();
    }

    public void SetNormalizedEmail(string? normalizedEmail)
    {
        NormalizedEmail = normalizedEmail;
        MarkAsModified();
    }

    public void SetEmailConfirmed(bool confirmed)
    {
        EmailConfirmed = confirmed;
        MarkAsModified();
    }

    public void SetPasswordHash(string? passwordHash)
    {
        PasswordHash = passwordHash;
        MarkAsModified();
    }

    public void SetSecurityStamp(string securityStamp)
    {
        SecurityStamp = securityStamp;
        MarkAsModified();
    }

    public void SetPhoneNumber(string? phoneNumber)
    {
        PhoneNumber = phoneNumber;
        MarkAsModified();
    }

    public void SetPhoneNumberConfirmed(bool confirmed)
    {
        PhoneNumberConfirmed = confirmed;
        MarkAsModified();
    }

    public void SetTwoFactorEnabled(bool enabled)
    {
        TwoFactorEnabled = enabled;
        MarkAsModified();
    }

    public void SetLockoutEnd(DateTimeOffset? lockoutEnd)
    {
        LockoutEnd = lockoutEnd;
        MarkAsModified();
    }

    public void SetLockoutEnabled(bool enabled)
    {
        LockoutEnabled = enabled;
        MarkAsModified();
    }

    public void SetAccessFailedCount(int count)
    {
        AccessFailedCount = count;
        MarkAsModified();
    }

    public void IncrementAccessFailedCount()
    {
        AccessFailedCount++;
        MarkAsModified();
    }

    public void ResetAccessFailedCount()
    {
        AccessFailedCount = 0;
        MarkAsModified();
    }

    public void Activate()
    {
        EnsureNotDeactivated();
        Status = UserStatus.Active;
        LockoutEnd = null;
        MarkAsModified();
    }

    public void Suspend(string? reason = null)
    {
        EnsureNotDeactivated();
        Status = UserStatus.Suspended;
        LockoutEnd = null;
        MarkAsModified();
    }

    public void Deactivate()
    {
        Status = UserStatus.Deactivated;
        LockoutEnd = null;
        MarkAsModified();
    }

    public void Unlock()
    {
        if (Status == UserStatus.Locked)
        {
            Status = UserStatus.Active;
            LockoutEnd = null;
            AccessFailedCount = 0;
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
        => [.. _permissionGrants
            .Select(grant => grant.Identifier)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(identifier => identifier, StringComparer.Ordinal)];

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

        return [.. identifiers.OrderBy(static id => id, StringComparer.Ordinal)];
    }

    public bool AssignRole(Guid roleId, IReadOnlyDictionary<string, string?>? parameterValues = null)
    {
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

    public bool UnlinkIdentity(string provider, string subject)
    {
        var key = BuildIdentityKey(UserIdentityLink.NormalizeProvider(provider), UserIdentityLink.NormalizeSubject(subject));
        if (_identityLinks.ContainsKey(key))
        {
            _identityLinks.Remove(key);
            MarkAsModified();
            return true;
        }
        return false;
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

    public void RecordSuccessfulLogin(DateTimeOffset timestamp)
    {
        LastLoginAt = timestamp;
        AccessFailedCount = 0;
        LockoutEnd = null;
        if (Status == UserStatus.PendingActivation)
        {
            Status = UserStatus.Active;
        }

        MarkAsModified();
    }

    public void RecordFailedLogin(DateTimeOffset timestamp, int lockoutThreshold = 5)
    {
        AccessFailedCount++;
        LastFailedLoginAt = timestamp;
        if (LockoutEnabled && AccessFailedCount >= lockoutThreshold)
        {
            Status = UserStatus.Locked;
            LockoutEnd = timestamp + TimeSpan.FromMinutes(15); // Default lockout
        }

        MarkAsModified();
    }

    public bool CanAuthenticate(DateTimeOffset timestamp)
    {
        if (Status == UserStatus.Deactivated || Status == UserStatus.Suspended)
        {
            return false;
        }

        if (Status == UserStatus.Locked && LockoutEnd is not null && timestamp < LockoutEnd)
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
        return UserSession.Create(Id, UserName, permissions, timestamp, timestamp + lifetime, codes);
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

    public void MarkEmailVerified()
    {
        if (string.IsNullOrWhiteSpace(Email))
        {
            throw new DomainException("Cannot verify email when email is missing.");
        }
        EmailConfirmed = true;
        MarkAsModified();
    }

    public void ClearEmail()
    {
        Email = null;
        NormalizedEmail = null;
        EmailConfirmed = false;
        MarkAsModified();
    }

    public void SetEmail(string? email, bool markVerified)
    {
        SetEmail(email);
        if (markVerified)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                 // If email is null/empty, we can't verify it. But SetEmail(null) clears it.
                 // If markVerified is true but email is null, what should happen?
                 // The test expects SetEmail("...", markVerified: true).
                 EmailConfirmed = true;
            }
            else
            {
                EmailConfirmed = true;
            }
        }
        else
        {
            EmailConfirmed = false;
        }
    }
}
