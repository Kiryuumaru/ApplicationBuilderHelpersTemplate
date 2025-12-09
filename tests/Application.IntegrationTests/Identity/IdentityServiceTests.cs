using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Authorization.Models;
using Application.Identity.Extensions;
using Application.Identity.Interfaces;
using Application.Identity.Models;
using Domain.Authorization.Constants;
using Domain.Authorization.Models;
using Domain.Authorization.ValueObjects;
using Domain.Identity.Models;
using Domain.Identity.ValueObjects;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Authentication;
using Xunit;
using RolesConstants = Domain.Authorization.Constants.Roles;
using Infrastructure.EFCore.Extensions;
using Infrastructure.EFCore.Identity.Extensions;
using Infrastructure.EFCore.Sqlite.Extensions;
using Application.Authorization.Interfaces;

namespace Application.IntegrationTests.Identity;

/// <summary>
/// Integration tests for IIdentityService.
/// Uses real EF Core implementation via DI - persistence ignorant pattern.
/// </summary>
public class IdentityServiceTests
{
    [Fact]
    public async Task RegisterUserAsync_PersistsActivatedUserWithPermissions()
    {
        var fixture = CreateFixture();

        var request = new UserRegistrationRequest(
            Username: "alice",
            Password: "Password!23",
            Email: "alice@example.com",
            PermissionIdentifiers: [Permissions.RootReadIdentifier]);

        var user = await fixture.IdentityService.RegisterUserAsync(request, CancellationToken.None);

        Assert.Equal("alice", user.UserName);
        var stored = await fixture.IdentityService.GetByUsernameAsync("alice", CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Contains(Permissions.RootReadIdentifier, stored!.GetPermissionIdentifiers());

        var userRole = await fixture.RoleService.GetByCodeAsync(RolesConstants.User.Code, CancellationToken.None);
        Assert.NotNull(userRole);
        var assignment = Assert.Single(stored.RoleAssignments, a => a.RoleId == userRole!.Id);
        Assert.Equal(stored.Id.ToString(), assignment.ParameterValues[Domain.Authorization.Constants.RoleIds.User.RoleUserIdParameter]);

        var session = await fixture.IdentityService.AuthenticateAsync("alice", "Password!23", CancellationToken.None);
        Assert.Contains(RolesConstants.User.Code, session.RoleCodes);
    }

    [Fact]
    public async Task RegisterUserAsync_GrantsSelfScopedAccessByDefault()
    {
        var fixture = CreateFixture();

        var user = await fixture.IdentityService.RegisterUserAsync(new UserRegistrationRequest("solo", "pa55word"), CancellationToken.None);

        var stored = await fixture.IdentityService.GetByUsernameAsync("solo", CancellationToken.None);
        Assert.NotNull(stored);
        var userRole = await fixture.RoleService.GetByCodeAsync(RolesConstants.User.Code, CancellationToken.None);
        Assert.NotNull(userRole);
        var assignment = Assert.Single(stored!.RoleAssignments, a => a.RoleId == userRole!.Id);
        Assert.Equal(user.Id.ToString(), assignment.ParameterValues[Domain.Authorization.Constants.RoleIds.User.RoleUserIdParameter]);

        var session = await fixture.IdentityService.AuthenticateAsync("solo", "pa55word", CancellationToken.None);
        Assert.Contains(RolesConstants.User.Code, session.RoleCodes);
    }

    [Fact]
    public async Task AuthenticateAsync_ReturnsSessionWithRolePermissions()
    {
        var fixture = CreateFixture();

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

        await fixture.IdentityService.RegisterUserAsync(new UserRegistrationRequest("bob", "correct-horse"), CancellationToken.None);

        await Assert.ThrowsAsync<AuthenticationException>(() => fixture.IdentityService.AuthenticateAsync("bob", "wrong", CancellationToken.None));
    }

    [Fact]
    public async Task AssignRoleAsync_AddsRoleToExistingUser()
    {
        var fixture = CreateFixture();

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

        var user = await fixture.IdentityService.RegisterUserAsync(new UserRegistrationRequest("delta", "pass"), CancellationToken.None);

        await fixture.IdentityService.AssignRoleAsync(
            user.Id,
            new RoleAssignmentRequest(RolesConstants.User.Code),
            CancellationToken.None);

        var stored = await fixture.IdentityService.GetByUsernameAsync("delta", CancellationToken.None);
        Assert.NotNull(stored);
        var assignment = Assert.Single(stored!.RoleAssignments);
        Assert.Equal(user.Id.ToString(), assignment.ParameterValues[Domain.Authorization.Constants.RoleIds.User.RoleUserIdParameter]);
    }

    [Fact]
    public async Task AuthenticateAsync_ExpandsParameterizedRoleAssignments()
    {
        var fixture = CreateFixture();

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

        var request = new ExternalUserRegistrationRequest(
            Username: "oauth-alice",
            Provider: "github",
            ProviderSubject: "gh-123",
            ProviderEmail: "alice@github.com",
            ProviderDisplayName: "Alice GH",
            Email: "alice@sample.com",
            PermissionIdentifiers: [Permissions.RootReadIdentifier]);

        var user = await fixture.IdentityService.RegisterExternalAsync(request, CancellationToken.None);

        Assert.Null(user.PasswordHash);
        var stored = await fixture.IdentityService.GetByUsernameAsync("oauth-alice", CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Single(stored!.IdentityLinks);
        Assert.Equal("github", stored.IdentityLinks.Single().Provider);
        Assert.Equal("gh-123", stored.IdentityLinks.Single().Subject);

        var userRole = await fixture.RoleService.GetByCodeAsync(RolesConstants.User.Code, CancellationToken.None);
        Assert.NotNull(userRole);
        var defaultAssignment = Assert.Single(stored.RoleAssignments, assignment => assignment.RoleId == userRole!.Id);
        Assert.Equal(stored.Id.ToString(), defaultAssignment.ParameterValues[Domain.Authorization.Constants.RoleIds.User.RoleUserIdParameter]);
    }

    [Fact]
    public async Task RegisterExternalAsync_PreventsDuplicateProviderSubject()
    {
        var fixture = CreateFixture();

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

        await Assert.ThrowsAsync<ArgumentException>(() => fixture.IdentityService.RegisterExternalAsync(
            new ExternalUserRegistrationRequest("name", string.Empty, "subject"),
            CancellationToken.None));

        await Assert.ThrowsAsync<ArgumentException>(() => fixture.IdentityService.RegisterExternalAsync(
            new ExternalUserRegistrationRequest("name", "provider", ""),
            CancellationToken.None));
    }

    private static IdentityFixture CreateFixture()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIdentityServices();

        // Use shared memory database with EF Core
        var dbName = Guid.NewGuid().ToString();
        var connectionString = $"Data Source={dbName};Mode=Memory;Cache=Shared";
        
        // Register EF Core infrastructure (includes EFCoreDatabaseInitializationState)
        services.AddEFCoreInfrastructure();
        services.AddEFCoreSqlite(connectionString);
        services.AddEFCoreIdentity();
        
        // Relax password requirements for tests
        services.Configure<IdentityOptions>(options =>
        {
            options.Password.RequireDigit = false;
            options.Password.RequireLowercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireUppercase = false;
            options.Password.RequiredLength = 1;
            options.Password.RequiredUniqueChars = 0;
        });
        
        var provider = services.BuildServiceProvider();

        // Keep a connection open to preserve the in-memory database and run migrations
        var dbContextFactory = provider.GetRequiredService<Microsoft.EntityFrameworkCore.IDbContextFactory<Infrastructure.EFCore.EFCoreDbContext>>();
        var dbContext = dbContextFactory.CreateDbContext();
        dbContext.Database.EnsureCreated();
        
        // Signal that database is initialized (uses internal method via InternalsVisibleTo)
        var initState = provider.GetRequiredService<Infrastructure.EFCore.Services.EFCoreDatabaseInitializationState>();
        initState.MarkInitialized();
        
        var identityService = provider.GetRequiredService<IIdentityService>();
        var roleService = provider.GetRequiredService<IRoleService>();
        
        return new IdentityFixture(identityService, roleService, dbContext);
    }

    private sealed record IdentityFixture(IIdentityService IdentityService, IRoleService RoleService, IDisposable Connection) : IDisposable
    {
        public void Dispose()
        {
            Connection.Dispose();
        }
    }
}
