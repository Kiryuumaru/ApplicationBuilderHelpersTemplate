using Domain.Authorization.Models;
using Domain.Identity.Exceptions;
using Domain.Identity.Interfaces;
using Domain.Identity.Models;
using Domain.Identity.ValueObjects;

namespace Domain.Identity.Services;

public sealed class UserAuthenticationService
{
    private readonly IPasswordVerifier _passwordVerifier;
    private readonly IUserRoleResolver? _roleResolver;
    private readonly TimeSpan _defaultLifetime;

    public UserAuthenticationService(IPasswordVerifier passwordVerifier, IUserRoleResolver? roleResolver = null, TimeSpan? defaultLifetime = null)
    {
        _passwordVerifier = passwordVerifier ?? throw new ArgumentNullException(nameof(passwordVerifier));
        _roleResolver = roleResolver;
        _defaultLifetime = defaultLifetime is { TotalSeconds: > 0 } lifetime ? lifetime : TimeSpan.FromHours(1);
    }

    public UserSession Authenticate(User user, string secret, DateTimeOffset? issuedAt = null, TimeSpan? lifetime = null)
    {
        ArgumentNullException.ThrowIfNull(user);

        var timestamp = issuedAt ?? DateTimeOffset.UtcNow;
        if (!user.CanAuthenticate(timestamp))
        {
            throw new AuthenticationException("User is not allowed to authenticate in the current state.");
        }

        if (user.PasswordHash is null)
        {
            throw new AuthenticationException("User does not have a password set.");
        }

        if (!_passwordVerifier.Verify(user.PasswordHash, secret))
        {
            user.RecordFailedLogin(timestamp);
            throw new AuthenticationException("Invalid credentials supplied.");
        }

        user.RecordSuccessfulLogin(timestamp);
        var effectiveLifetime = lifetime is { TotalSeconds: > 0 } provided ? provided : _defaultLifetime;
        var resolvedRoles = _roleResolver?.ResolveRoles(user) ?? Array.Empty<UserRoleResolution>();
        var roleCodes = resolvedRoles.Count == 0
            ? null
            : resolvedRoles.Select(static resolution => resolution.Role.Code);
        var permissions = user.BuildEffectivePermissions(resolvedRoles);
        return user.CreateSession(effectiveLifetime, timestamp, permissions, roleCodes);
    }
}
