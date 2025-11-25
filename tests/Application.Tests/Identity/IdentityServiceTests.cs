using System.Collections.Generic;
using System.Linq;
using Application.Authorization.Roles.Models;
using Application.Authorization.Roles.Services;
using Application.Identity.Models;
using Application.Identity.Services;
using Domain.Authorization.Constants;
using Domain.Identity.Exceptions;
using RolesConstants = Domain.Authorization.Constants.Roles;

namespace Application.Tests.Identity;

public class IdentityServiceTests
{
    [Fact]
    public async Task RegisterUserAsync_PersistsActivatedUserWithPermissions()
    {
        var fixture = CreateFixture();
        await fixture.RoleService.EnsureSystemRolesAsync(CancellationToken.None);

        var request = new UserRegistrationRequest(
            Username: "alice",
            Password: "Password!23",
            Email: "alice@example.com",
            PermissionIdentifiers: [Permissions.RootReadIdentifier]);

        var user = await fixture.IdentityService.RegisterUserAsync(request, CancellationToken.None);

        Assert.Equal("alice", user.Username);
        var stored = await fixture.IdentityService.GetByUsernameAsync("alice", CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Contains(Permissions.RootReadIdentifier, stored!.GetPermissionIdentifiers());

        var userRole = await fixture.RoleService.GetByCodeAsync(RolesConstants.User.Code, CancellationToken.None);
        Assert.NotNull(userRole);
        var assignment = Assert.Single(stored.RoleAssignments, a => a.RoleId == userRole!.Id);
        Assert.Equal(stored.Id.ToString(), assignment.ParameterValues[RoleIds.User.RoleUserIdParameter]);

        var session = await fixture.IdentityService.AuthenticateAsync("alice", "Password!23", CancellationToken.None);
        Assert.Contains(RolesConstants.User.Code, session.RoleCodes);
    }

    [Fact]
    public async Task RegisterUserAsync_GrantsSelfScopedAccessByDefault()
    {
        var fixture = CreateFixture();
        await fixture.RoleService.EnsureSystemRolesAsync(CancellationToken.None);

        var user = await fixture.IdentityService.RegisterUserAsync(new UserRegistrationRequest("solo", "pa55word"), CancellationToken.None);

        var stored = await fixture.IdentityService.GetByUsernameAsync("solo", CancellationToken.None);
        Assert.NotNull(stored);
        var userRole = await fixture.RoleService.GetByCodeAsync(RolesConstants.User.Code, CancellationToken.None);
        Assert.NotNull(userRole);
        var assignment = Assert.Single(stored!.RoleAssignments, a => a.RoleId == userRole!.Id);
        Assert.Equal(user.Id.ToString(), assignment.ParameterValues[RoleIds.User.RoleUserIdParameter]);

        var session = await fixture.IdentityService.AuthenticateAsync("solo", "pa55word", CancellationToken.None);
        Assert.Contains(RolesConstants.User.Code, session.RoleCodes);
    }

    [Fact]
    public async Task AuthenticateAsync_ReturnsSessionWithRolePermissions()
    {
        var fixture = CreateFixture();
        await fixture.RoleService.EnsureSystemRolesAsync(CancellationToken.None);

        var request = new UserRegistrationRequest(
            Username: "admin-user",
            Password: "Secret!123",
            RoleAssignments: [new RoleAssignmentRequest(RolesConstants.Admin.Code)]);

        await fixture.IdentityService.RegisterUserAsync(request, CancellationToken.None);

        var session = await fixture.IdentityService.AuthenticateAsync("admin-user", "Secret!123", CancellationToken.None);

        Assert.Contains(RolesConstants.Admin.Code, session.RoleCodes);
        Assert.Contains(Permissions.RootWriteIdentifier, session.PermissionIdentifiers);
    }

    [Fact]
    public async Task AuthenticateAsync_ThrowsForInvalidPassword()
    {
        var fixture = CreateFixture();
        await fixture.RoleService.EnsureSystemRolesAsync(CancellationToken.None);

        await fixture.IdentityService.RegisterUserAsync(new UserRegistrationRequest("bob", "correct-horse"), CancellationToken.None);

        await Assert.ThrowsAsync<AuthenticationException>(() => fixture.IdentityService.AuthenticateAsync("bob", "wrong", CancellationToken.None));
    }

    [Fact]
    public async Task AssignRoleAsync_AddsRoleToExistingUser()
    {
        var fixture = CreateFixture();
        await fixture.RoleService.EnsureSystemRolesAsync(CancellationToken.None);

        var user = await fixture.IdentityService.RegisterUserAsync(new UserRegistrationRequest("charlie", "pass"), CancellationToken.None);

        await fixture.IdentityService.AssignRoleAsync(
            user.Id,
            new RoleAssignmentRequest(RolesConstants.User.Code),
            CancellationToken.None);

        var session = await fixture.IdentityService.AuthenticateAsync("charlie", "pass", CancellationToken.None);

        Assert.Contains(RolesConstants.User.Code, session.RoleCodes);
    }

    [Fact]
    public async Task AuthenticateAsync_UserReceivesRoleAndDirectPermissions()
    {
        var fixture = CreateFixture();
        await fixture.RoleService.EnsureSystemRolesAsync(CancellationToken.None);

        const string additionalPermission = "api:market:assets:list";
        var request = new UserRegistrationRequest(
            Username: "mixed-user",
            Password: "Mixed!123",
            PermissionIdentifiers: [additionalPermission],
            RoleAssignments: [new RoleAssignmentRequest(RolesConstants.Admin.Code)]);

        await fixture.IdentityService.RegisterUserAsync(request, CancellationToken.None);

        var session = await fixture.IdentityService.AuthenticateAsync("mixed-user", "Mixed!123", CancellationToken.None);

        Assert.Contains(RolesConstants.Admin.Code, session.RoleCodes);
        Assert.Contains(Permissions.RootWriteIdentifier, session.PermissionIdentifiers); // from role grant
        Assert.Contains(additionalPermission, session.PermissionIdentifiers); // direct user grant
    }

    [Fact]
    public async Task AssignRoleAsync_FillsDefaultRoleParameters()
    {
        var fixture = CreateFixture();
        await fixture.RoleService.EnsureSystemRolesAsync(CancellationToken.None);

        var user = await fixture.IdentityService.RegisterUserAsync(new UserRegistrationRequest("delta", "pass"), CancellationToken.None);

        await fixture.IdentityService.AssignRoleAsync(
            user.Id,
            new RoleAssignmentRequest(RolesConstants.User.Code),
            CancellationToken.None);

        var stored = await fixture.IdentityService.GetByUsernameAsync("delta", CancellationToken.None);
        Assert.NotNull(stored);
        var assignment = Assert.Single(stored!.RoleAssignments);
        Assert.Equal(user.Id.ToString(), assignment.ParameterValues[RoleIds.User.RoleUserIdParameter]);
    }

    [Fact]
    public async Task AuthenticateAsync_ExpandsParameterizedRoleAssignments()
    {
        var fixture = CreateFixture();
        await fixture.RoleService.EnsureSystemRolesAsync(CancellationToken.None);

        var descriptor = new RoleDescriptor(
            Code: "portfolio_reader",
            Name: "Portfolio Reader",
            Description: "Reads a single portfolio",
            IsSystemRole: false,
            PermissionTemplates:
            [
                new RolePermissionTemplateDescriptor(
                    "api:portfolio:[portfolioId={portfolioId}]:positions:read",
                    ["portfolioId"])
            ]);

        await fixture.RoleService.CreateRoleAsync(descriptor, CancellationToken.None);

        var request = new UserRegistrationRequest(
            Username: "scoped-user",
            Password: "Scoped!123",
            RoleAssignments:
            [
                new RoleAssignmentRequest(
                    "portfolio_reader",
                    new Dictionary<string, string?> { ["portfolioId"] = "portfolio-123" })
            ]);

        await fixture.IdentityService.RegisterUserAsync(request, CancellationToken.None);

        var session = await fixture.IdentityService.AuthenticateAsync("scoped-user", "Scoped!123", CancellationToken.None);
        Assert.Contains("api:portfolio:positions:read", session.PermissionIdentifiers);

        var stored = await fixture.IdentityService.GetByUsernameAsync("scoped-user", CancellationToken.None);
        Assert.NotNull(stored);

        var portfolioRole = await fixture.RoleService.GetByCodeAsync("portfolio_reader", CancellationToken.None);
        Assert.NotNull(portfolioRole);
        var portfolioAssignment = Assert.Single(stored!.RoleAssignments, a => a.RoleId == portfolioRole!.Id);
        Assert.Equal("portfolio-123", portfolioAssignment.ParameterValues["portfolioId"]);

        var userRole = await fixture.RoleService.GetByCodeAsync(RolesConstants.User.Code, CancellationToken.None);
        Assert.NotNull(userRole);
        Assert.Contains(stored.RoleAssignments, assignment => assignment.RoleId == userRole!.Id);
    }

    [Fact]
    public async Task AssignRoleAsync_ThrowsWhenRequiredParametersMissing()
    {
        var fixture = CreateFixture();
        await fixture.RoleService.EnsureSystemRolesAsync(CancellationToken.None);

        var descriptor = new RoleDescriptor(
            Code: "portfolio_reader",
            Name: "Portfolio Reader",
            Description: "Reads a single portfolio",
            IsSystemRole: false,
            PermissionTemplates:
            [
                new RolePermissionTemplateDescriptor(
                    "api:portfolio:[portfolioId={portfolioId}]:positions:read",
                    ["portfolioId"])
            ]);

        await fixture.RoleService.CreateRoleAsync(descriptor, CancellationToken.None);
        var user = await fixture.IdentityService.RegisterUserAsync(new UserRegistrationRequest("epsilon", "pass"), CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.IdentityService.AssignRoleAsync(
            user.Id,
            new RoleAssignmentRequest("portfolio_reader"),
            CancellationToken.None));
    }

    [Fact]
    public async Task RegisterExternalAsync_PersistsIdentityLinkAndDefaultRole()
    {
        var fixture = CreateFixture();
        await fixture.RoleService.EnsureSystemRolesAsync(CancellationToken.None);

        var request = new ExternalUserRegistrationRequest(
            Username: "oauth-alice",
            Provider: "github",
            ProviderSubject: "gh-123",
            ProviderEmail: "alice@github.com",
            ProviderDisplayName: "Alice GH",
            Email: "alice@sample.com",
            PermissionIdentifiers: [Permissions.RootReadIdentifier]);

        var user = await fixture.IdentityService.RegisterExternalAsync(request, CancellationToken.None);

        Assert.False(user.HasPasswordCredential);
        var stored = await fixture.IdentityService.GetByUsernameAsync("oauth-alice", CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Single(stored!.IdentityLinks);
        Assert.Equal("github", stored.IdentityLinks.Single().Provider);
        Assert.Equal("gh-123", stored.IdentityLinks.Single().Subject);

        var userRole = await fixture.RoleService.GetByCodeAsync(RolesConstants.User.Code, CancellationToken.None);
        Assert.NotNull(userRole);
        var defaultAssignment = Assert.Single(stored.RoleAssignments, assignment => assignment.RoleId == userRole!.Id);
        Assert.Equal(stored.Id.ToString(), defaultAssignment.ParameterValues[RoleIds.User.RoleUserIdParameter]);
    }

    [Fact]
    public async Task RegisterExternalAsync_PreventsDuplicateProviderSubject()
    {
        var fixture = CreateFixture();
        await fixture.RoleService.EnsureSystemRolesAsync(CancellationToken.None);

        var request = new ExternalUserRegistrationRequest(
            Username: "oauth-bob",
            Provider: "google",
            ProviderSubject: "google-42");

        await fixture.IdentityService.RegisterExternalAsync(request, CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.IdentityService.RegisterExternalAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task RegisterExternalAsync_ThrowsWhenProviderOrSubjectMissing()
    {
        var fixture = CreateFixture();
        await fixture.RoleService.EnsureSystemRolesAsync(CancellationToken.None);

        await Assert.ThrowsAsync<ArgumentException>(() => fixture.IdentityService.RegisterExternalAsync(
            new ExternalUserRegistrationRequest("name", string.Empty, "subject"),
            CancellationToken.None));

        await Assert.ThrowsAsync<ArgumentException>(() => fixture.IdentityService.RegisterExternalAsync(
            new ExternalUserRegistrationRequest("name", "provider", ""),
            CancellationToken.None));
    }

    private static IdentityFixture CreateFixture()
    {
        var roleRepository = new InMemoryRoleRepository();
        var roleService = new RoleService(roleRepository);
        var userStore = new InMemoryUserStore();
        var credentialFactory = new Pbkdf2PasswordCredentialFactory();
        var secretValidator = new Pbkdf2UserSecretValidator();
        var roleResolver = new UserRoleResolver(roleRepository);
        var identityService = new IdentityService(userStore, roleRepository, credentialFactory, secretValidator, roleResolver);
        return new IdentityFixture(identityService, roleService);
    }

    private sealed record IdentityFixture(IdentityService IdentityService, RoleService RoleService);
}
