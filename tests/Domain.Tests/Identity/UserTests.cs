using System.Linq;
using Domain.Authorization.Models;
using Domain.Authorization.ValueObjects;
using Domain.Identity.Exceptions;
using Domain.Identity.Interfaces;
using Domain.Identity.Models;
using Domain.Identity.Services;
using Domain.Identity.ValueObjects;
using Domain.Shared.Exceptions;

namespace Domain.Tests.Identity;

public class UserTests
{
    [Fact]
    public void GrantPermission_NormalizesIdentifier()
    {
        var user = CreateUser();
        var grant = UserPermissionGrant.Create(" api : portfolio : accounts : list ");

        user.GrantPermission(grant);

        var identifiers = user.GetPermissionIdentifiers();
        Assert.Single(identifiers);
        Assert.Equal("api:portfolio:accounts:list", identifiers.First());
    }

    [Fact]
    public void AuthenticationService_ReturnsSession_WhenSecretIsValid()
    {
        var user = CreateUser();
        user.Activate();
        user.GrantPermission(UserPermissionGrant.Create("api:portfolio:accounts:list"));

        var service = new UserAuthenticationService(new StubSecretValidator("secret"));
        var session = service.Authenticate(user, "secret".AsSpan(), DateTimeOffset.UtcNow);

        Assert.Equal(user.Id, session.UserId);
        Assert.Equal(user.Username, session.Username);
        Assert.NotEmpty(session.PermissionIdentifiers);
    }

    [Fact]
    public void AuthenticationService_Throws_WhenSecretIsInvalid()
    {
        var user = CreateUser();
        user.Activate();

        var service = new UserAuthenticationService(new StubSecretValidator("secret"));

        Assert.Throws<AuthenticationException>(() => service.Authenticate(user, "invalid".AsSpan(), DateTimeOffset.UtcNow));
    }

    [Fact]
    public void AssignRole_AddsRoleIdOnce()
    {
        var user = CreateUser();
        var roleId = Guid.NewGuid();

        Assert.True(user.AssignRole(roleId));
        Assert.Contains(roleId, user.RoleIds);
        Assert.False(user.AssignRole(roleId));
    }

    [Fact]
    public void BuildEffectivePermissions_IncludesRoleGrants()
    {
        var user = CreateUser();
        user.GrantPermission(UserPermissionGrant.Create("api:portfolio:accounts:list"));

        var role = Role.Create("admin", "Administrator");
        role.AssignPermission(RolePermissionTemplate.Create("api:portfolio:positions:close"));

        var identifiers = user.BuildEffectivePermissions([new UserRoleResolution(role)]);

        Assert.Contains("api:portfolio:accounts:list", identifiers);
        Assert.Contains("api:portfolio:positions:close", identifiers);
    }

    [Fact]
    public void AuthenticationService_AppendsRoleCodesToSession()
    {
        var user = CreateUser();
        user.Activate();
        user.GrantPermission(UserPermissionGrant.Create("api:portfolio:accounts:list"));

        var adminRole = Role.Create("admin", "Administrator");
        adminRole.AssignPermission(RolePermissionTemplate.Create("api:_write"));

        var service = new UserAuthenticationService(new StubSecretValidator("secret"), new StubRoleResolver(adminRole));
        var session = service.Authenticate(user, "secret".AsSpan(), DateTimeOffset.UtcNow);

        Assert.Contains("admin", session.RoleCodes);
        Assert.Contains("api:_write", session.PermissionIdentifiers);
    }

    [Fact]
    public void RegisterExternal_AllowsMissingEmail_AndCanBeSetLater()
    {
        var user = User.RegisterExternal("ext-only", "github", "gh-1");

        Assert.Null(user.Email);
        user.SetEmail("final@example.com");

        Assert.Equal("final@example.com", user.Email);
    }

    [Fact]
    public void SetEmail_NormalizesAndOptionallyVerifies()
    {
        var user = User.Register("alpha");
        user.SetEmail("MixedCase@Example.com");

        Assert.Equal("mixedcase@example.com", user.Email);
        Assert.False(user.IsEmailVerified);

        user.SetEmail("mixedcase@example.com", markVerified: true);

        Assert.True(user.IsEmailVerified);
    }

    [Fact]
    public void ClearEmail_RemovesVerification()
    {
        var user = User.Register("alpha", "user@example.com");
        user.MarkEmailVerified();

        user.ClearEmail();

        Assert.Null(user.Email);
        Assert.False(user.IsEmailVerified);
    }

    [Fact]
    public void MarkEmailVerified_ThrowsWhenEmailMissing()
    {
        var user = User.Register("alpha");

        Assert.Throws<DomainException>(() => user.MarkEmailVerified());
    }

    [Fact]
    public void AuthenticationService_Throws_WhenUserHasNoLocalCredential()
    {
        var user = User.Register("federated", "federated@example.com");
        user.Activate();

        var service = new UserAuthenticationService(new StubSecretValidator("secret"));

        Assert.Throws<AuthenticationException>(() => service.Authenticate(user, "secret".AsSpan(), DateTimeOffset.UtcNow));
    }

    [Fact]
    public void LinkIdentity_AddsOrUpdatesLink()
    {
        var user = User.Register("social", "social@example.com");

        var firstLink = user.LinkIdentity("google", "123", "social@example.com", "Trader");
        Assert.Equal("google", firstLink.Provider);
        Assert.Single(user.IdentityLinks);

        var updatedLink = user.LinkIdentity("google", "123", "alt@example.com", "Trader Pro");
        Assert.Equal("alt@example.com", updatedLink.Email);
        Assert.Single(user.IdentityLinks);
        Assert.Equal("Trader Pro", user.IdentityLinks.First().DisplayName);
    }

    [Fact]
    public void UnlinkIdentity_RemovesLink()
    {
        var user = User.RegisterExternal("federated", "github", "abc123", email: "federated@example.com");
        Assert.Single(user.IdentityLinks);

        var removed = user.UnlinkIdentity("github", "abc123");

        Assert.True(removed);
        Assert.Empty(user.IdentityLinks);
    }

    private static User CreateUser()
    {
        var credential = PasswordCredential.Create("argon2", "hash", "salt", 3);
        return User.Register("trader", "trader@example.com", credential);
    }

    private sealed class StubSecretValidator(string expected) : IUserSecretValidator
    {
        public bool Verify(PasswordCredential credential, ReadOnlySpan<char> secret)
        {
            return secret.SequenceEqual(expected.AsSpan());
        }
    }

    private sealed class StubRoleResolver(params Role[] roles) : IUserRoleResolver
    {
        private readonly IReadOnlyCollection<Role> _roles = roles;

        public IReadOnlyCollection<UserRoleResolution> ResolveRoles(User user)
        {
            return [.. _roles.Select(static role => new UserRoleResolution(role))];
        }
    }
}
