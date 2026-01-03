using Application.Authorization.Interfaces;
using Application.Authorization.Interfaces.Infrastructure;
using Application.Authorization.Models;
using Application.Authorization.Services;
using Domain.Authorization.Constants;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Application.UnitTests.Authorization;

/// <summary>
/// Tests for the new scope directive system using generated PermissionIds constants
/// and IPermissionService from the Application layer.
/// Format: "allow;path" or "allow;path;key=value" or "deny;path"
/// </summary>
public class PermissionServiceScopeDirectiveTests
{
    #region Scope Directive Format Tests (PermissionIds Generated API)

    [Fact]
    public void Allow_WithNoParameters_ReturnsCorrectFormat()
    {
        // Using generated PermissionIds root Read scope
        var directive = PermissionIds.Read.Allow();

        Assert.Equal("allow;_read", directive);
    }

    [Fact]
    public void Deny_WithNoParameters_ReturnsCorrectFormat()
    {
        var directive = PermissionIds.Write.Deny();

        Assert.Equal("deny;_write", directive);
    }

    [Fact]
    public void Allow_WithSingleParameter_ReturnsCorrectFormat()
    {
        // Using generated PermissionIds with userId parameter
        var directive = PermissionIds.Api.Users.Read.WithUserId("abc123").Allow();

        Assert.Equal("allow;api:users:_read;userId=abc123", directive);
    }

    [Fact]
    public void Allow_WithMultipleParameters_ReturnsCorrectFormat()
    {
        // Using generated PermissionIds with userId parameter (accountId no longer exists)
        var directive = PermissionIds.Api.Portfolio.Accounts.Read.WithUserId("user-1").Allow();

        Assert.Equal("allow;api:portfolio:accounts:_read;userId=user-1", directive);
    }

    [Fact]
    public void Deny_WithParameters_ReturnsCorrectFormat()
    {
        var directive = PermissionIds.Api.Users.ResetPassword.WithUserId("abc123").Deny();

        Assert.Equal("deny;api:users:reset_password;userId=abc123", directive);
    }

    [Fact]
    public void ScopeBuilder_Immutability_OriginalUnchanged()
    {
        var builder1 = PermissionIds.Api.Users.Read.WithUserId("user-1");
        var builder2 = builder1.WithUserId("user-2"); // Chaining should create new instance

        var directive1 = builder1.Allow();
        var directive2 = builder2.Allow();

        // Verify both produce valid distinct directives
        Assert.Contains("userId=user-1", directive1);
        Assert.Contains("userId=user-2", directive2);
        Assert.NotEqual(directive1, directive2);
    }

    [Fact]
    public void LeafPermission_Allow_ReturnsCorrectFormat()
    {
        // Test a leaf permission (not a scope)
        var directive = PermissionIds.Api.Users.ReadPermission.Allow();

        Assert.Equal("allow;api:users:read", directive);
    }

    [Fact]
    public void LeafPermission_Deny_ReturnsCorrectFormat()
    {
        var directive = PermissionIds.Api.Users.Update.Deny();

        Assert.Equal("deny;api:users:update", directive);
    }

    #endregion

    #region Permission Checking Tests (Using IPermissionService.HasPermission)

    [Fact]
    public void DirectiveExtraction_ExtractsScopeCorrectly()
    {
        // This test verifies the claims are being set up correctly
        var (service, principal) = CreateServiceWithPrincipal(
            "user-abc",
            PermissionIds.Api.Users.Read.WithUserId("user-abc").Allow()
        );

        var scopeClaims = principal.Claims
            .Where(c => c.Type == "scope")
            .Select(c => c.Value)
            .ToList();

        // The directive should be in the claims
        Assert.Contains("allow;api:users:_read;userId=user-abc", scopeClaims);
    }

    [Fact]
    public void DiagnosticTest_PermissionServiceWithScopeDirective()
    {
        // Create principal with rbac_version=2 and a scope directive
        var (service, principal) = CreateServiceWithPrincipal(
            "user-test",
            "allow;api:users:_read;userId=user-test"
        );

        // First verify the rbac_version is set
        var rbacVersion = principal.Claims.FirstOrDefault(c => c.Type == "rbac_version")?.Value;
        Assert.Equal("2", rbacVersion);

        // Verify the scope claim is set
        var scopeClaims = principal.Claims.Where(c => c.Type == "scope").Select(c => c.Value).ToList();
        Assert.Single(scopeClaims);
        Assert.Equal("allow;api:users:_read;userId=user-test", scopeClaims[0]);

        // Test without userId parameter first - this should NOT pass if scope has userId
        var canReadUsersWithoutUserId = service.HasPermission(principal, "api:users:read");
        Assert.False(canReadUsersWithoutUserId, "Should deny without userId when scope has userId parameter");

        // Now test WITH userId parameter - using strongly-typed Permission API
        var canReadUsersWithUserId = service.HasPermission(principal, PermissionIds.Api.Users.ReadPermission.Permission.WithUserId("user-test"));
        Assert.True(canReadUsersWithUserId, "Should allow with matching userId parameter");
    }

    [Fact]
    public void DiagnosticTest_PermissionServiceWithBroadScope()
    {
        // Create principal with rbac_version=2 and a BROAD scope (no userId parameter)
        var (service, principal) = CreateServiceWithPrincipal(
            "service-acct",
            "allow;api:users:_read"  // No parameters
        );

        // This should grant access to any api:users:read request
        var canReadUsers = service.HasPermission(principal, "api:users:read");
        Assert.True(canReadUsers, "Broad scope should grant access without parameters");

        var canReadUsersWithUserId = service.HasPermission(principal, PermissionIds.Api.Users.ReadPermission.Permission.WithUserId("any-user"));
        Assert.True(canReadUsersWithUserId, "Broad scope should grant access even with userId in request");
    }

    [Fact]
    public void HasPermission_UserWithOwnScope_CanAccessOwnData()
    {
        var (service, principal) = CreateServiceWithPrincipal(
            "user-abc",
            PermissionIds.Api.Users.Read.WithUserId("user-abc").Allow()
        );

        // Check permission for api:users:read with matching userId
        // Using strongly-typed Permission API for type safety
        var hasPermission = service.HasPermission(principal, PermissionIds.Api.Users.ReadPermission.Permission.WithUserId("user-abc"));

        Assert.True(hasPermission);
    }

    [Fact]
    public void HasPermission_UserWithDeny_CannotAccessDeniedResource()
    {
        var (service, principal) = CreateServiceWithPrincipal(
            "user-xyz",
            PermissionIds.Api.Users.Read.Allow(),   // Allow all user reads
            PermissionIds.Api.Users.Update.Deny()   // But deny updates
        );

        var canRead = service.HasPermission(principal, "api:users:read");
        var canUpdate = service.HasPermission(principal, "api:users:update");

        Assert.True(canRead);
        Assert.False(canUpdate);
    }

    [Fact]
    public void HasPermission_ScopeGrantsLeafPermissions()
    {
        var (service, principal) = CreateServiceWithPrincipal(
            "user-test",
            PermissionIds.Api.Users.Read.WithUserId("user-test").Allow() // _read scope
        );

        // The _read scope should grant the "read" leaf permission under users
        // Using strongly-typed Permission API
        var hasReadLeaf = service.HasPermission(principal, PermissionIds.Api.Users.ReadPermission.Permission.WithUserId("user-test"));

        Assert.True(hasReadLeaf);
    }

    [Fact]
    public void HasAnyPermission_WithMultipleScopes_MatchesAny()
    {
        var (service, principal) = CreateServiceWithPrincipal(
            "user-1",
            PermissionIds.Api.Users.Read.WithUserId("user-1").Allow()
        );

        var hasAny = service.HasAnyPermission(principal, new string[]
        {
            PermissionIds.Api.Portfolio.Accounts.List.Permission.WithUserId("user-1"), // Not granted
            PermissionIds.Api.Users.ReadPermission.Permission.WithUserId("user-1")  // Granted
        });

        Assert.True(hasAny);
    }

    [Fact]
    public void HasAllPermissions_RequiresAllMatches()
    {
        var (service, principal) = CreateServiceWithPrincipal(
            "user-2",
            PermissionIds.Api.Users.Read.WithUserId("user-2").Allow(),
            PermissionIds.Api.Users.Write.WithUserId("user-2").Allow()
        );

        var hasAll = service.HasAllPermissions(principal, new string[]
        {
            PermissionIds.Api.Users.ReadPermission.Permission.WithUserId("user-2"),
            PermissionIds.Api.Users.Update.Permission.WithUserId("user-2")
        });

        Assert.True(hasAll);
    }

    [Fact]
    public void HasAllPermissions_FailsWhenOneIsMissing()
    {
        var (service, principal) = CreateServiceWithPrincipal(
            "user-3",
            PermissionIds.Api.Users.Read.WithUserId("user-3").Allow()
            // Write scope not granted
        );

        var hasAll = service.HasAllPermissions(principal, new string[]
        {
            PermissionIds.Api.Users.ReadPermission.Permission.WithUserId("user-3"),
            PermissionIds.Api.Users.Update.Permission.WithUserId("user-3") // Not granted
        });

        Assert.False(hasAll);
    }

    #endregion

    #region Fine-Grained Access Tests

    [Fact]
    public void MultiAccountAccess_OnlyAllowsSpecificUsers()
    {
        var (service, principal) = CreateServiceWithPrincipal(
            "admin-user",
            PermissionIds.Api.Portfolio.Accounts.Read.WithUserId("user-1").Allow(),
            PermissionIds.Api.Portfolio.Accounts.Read.WithUserId("user-2").Allow()
        );

        // Using strongly-typed Permission API with userId only
        var canAccessUser1 = service.HasPermission(principal, PermissionIds.Api.Portfolio.Accounts.ReadPermission.Permission.WithUserId("user-1"));
        var canAccessUser2 = service.HasPermission(principal, PermissionIds.Api.Portfolio.Accounts.ReadPermission.Permission.WithUserId("user-2"));
        var canAccessUser3 = service.HasPermission(principal, PermissionIds.Api.Portfolio.Accounts.ReadPermission.Permission.WithUserId("user-3"));

        Assert.True(canAccessUser1);
        Assert.True(canAccessUser2);
        Assert.False(canAccessUser3); // Not granted
    }

    [Fact]
    public void ServiceAccount_HasNoUserIdScope_AccessesAll()
    {
        var (service, principal) = CreateServiceWithPrincipal(
            "service-acct",
            PermissionIds.Api.Users.Read.Allow() // No userId parameter = access all users
        );

        var canAccessUser1 = service.HasPermission(principal, PermissionIds.Api.Users.ReadPermission.Permission.WithUserId("any-user-1"));
        var canAccessUser2 = service.HasPermission(principal, PermissionIds.Api.Users.ReadPermission.Permission.WithUserId("any-user-2"));
        var canAccessNoUser = service.HasPermission(principal, PermissionIds.Api.Users.ReadPermission.Permission);

        Assert.True(canAccessUser1);
        Assert.True(canAccessUser2);
        Assert.True(canAccessNoUser);
    }

    [Fact]
    public void RootReadScope_GrantsAllReadLeafPermissions()
    {
        var (service, principal) = CreateServiceWithPrincipal(
            "admin-read",
            PermissionIds.Read.Allow() // Global _read scope
        );

        // Should grant access to any read permission
        var canReadUsers = service.HasPermission(principal, "api:users:read");
        var canReadAccounts = service.HasPermission(principal, "api:portfolio:accounts:list");
        var canReadFavorites = service.HasPermission(principal, "api:favorites:read");

        Assert.True(canReadUsers);
        Assert.True(canReadAccounts);
        Assert.True(canReadFavorites);

        // But NOT write permissions
        var canUpdateUsers = service.HasPermission(principal, "api:users:update");
        Assert.False(canUpdateUsers);
    }

    [Fact]
    public void NestedResourceAccess_RequiresMatchingParameters()
    {
        var (service, principal) = CreateServiceWithPrincipal(
            "user-nested",
            PermissionIds.Api.Portfolio.Accounts.Read.WithUserId("user-nested").Allow()
        );

        // Request with userId parameter - should match
        var withUserId = service.HasPermission(principal, PermissionIds.Api.Portfolio.Accounts.ReadPermission.Permission.WithUserId("user-nested"));
        Assert.True(withUserId);

        // Request with different userId - should not match
        var differentUserId = service.HasPermission(principal, PermissionIds.Api.Portfolio.Accounts.ReadPermission.Permission.WithUserId("other-user"));
        Assert.False(differentUserId);

        // Request with no params - scope has params, so should not match
        var noParams = service.HasPermission(principal, PermissionIds.Api.Portfolio.Accounts.ReadPermission.Permission);
        Assert.False(noParams);
    }

    #endregion

    #region Empty and Boundary Condition Tests

    [Fact]
    public void EmptyScope_DeniesAllPermissions()
    {
        var (service, principal) = CreateServiceWithPrincipal(
            "user-empty"
            // No scopes granted
        );

        var hasAnyPermission = service.HasPermission(principal, "api:user:profile:read");

        Assert.False(hasAnyPermission);
    }

    [Fact]
    public void SingleDenyOnly_AllowsEverythingExceptDenied()
    {
        var (service, principal) = CreateServiceWithPrincipal(
            "user-deny-only",
            PermissionIds.Api.Users.ResetPassword.Deny() // Only deny, no explicit allows
        );

        // With only deny directives and no allow directives, everything except denied should be allowed
        var canResetPassword = service.HasPermission(principal, "api:users:reset_password");
        var canReadUsers = service.HasPermission(principal, "api:users:read");

        Assert.False(canResetPassword); // Explicitly denied
        Assert.True(canReadUsers);     // Not denied, so allowed
    }

    [Fact]
    public void ConflictingAllowAndDeny_DenyWins()
    {
        var (service, principal) = CreateServiceWithPrincipal(
            "user-conflict",
            PermissionIds.Api.Users.Read.Allow(),  // Allow user reads
            PermissionIds.Api.Users.Read.Deny()    // Also deny user reads
        );

        var canRead = service.HasPermission(principal, "api:users:read");

        Assert.False(canRead); // Deny should take precedence
    }

    [Fact]
    public void GlobalAllowThenSpecificDeny_DenyBlocksSpecific()
    {
        var (service, principal) = CreateServiceWithPrincipal(
            "user-global-deny",
            PermissionIds.Read.Allow(),                    // Allow all reads
            PermissionIds.Api.Portfolio.Read.Deny()       // But deny portfolio read
        );

        var canReadUsers = service.HasPermission(principal, "api:users:read");
        var canReadPortfolio = service.HasPermission(principal, "api:portfolio:read");

        Assert.True(canReadUsers);   // Allowed by global _read
        Assert.False(canReadPortfolio); // Explicitly denied
    }

    #endregion

    #region Privilege Escalation Tests

    [Fact]
    public void PrivilegeEscalation_UserCannotAccessOtherUsersData()
    {
        var (service, principal) = CreateServiceWithPrincipal(
            "user-own",
            PermissionIds.Api.Users.Read.WithUserId("user-own").Allow()
        );

        // Using strongly-typed Permission API
        var canAccessOwn = service.HasPermission(principal, PermissionIds.Api.Users.ReadPermission.Permission.WithUserId("user-own"));
        var canAccessOther = service.HasPermission(principal, PermissionIds.Api.Users.ReadPermission.Permission.WithUserId("other-user"));

        Assert.True(canAccessOwn);
        Assert.False(canAccessOther); // Cannot access another user's data
    }

    [Fact]
    public void PathTraversal_CannotEscapeHierarchy()
    {
        var (service, principal) = CreateServiceWithPrincipal(
            "user-path",
            PermissionIds.Api.Users.Read.WithUserId("user-path").Allow()
        );

        // Having users:_read should not grant access to sibling portfolio
        // Using strongly-typed Permission API
        var canReadPortfolio = service.HasPermission(principal, PermissionIds.Api.Portfolio.ReadPermission.Permission.WithUserId("user-path"));

        Assert.False(canReadPortfolio);
    }

    [Fact]
    public void NestedScopeHierarchy_ChildDoesNotGrantParent()
    {
        var (service, principal) = CreateServiceWithPrincipal(
            "user-child",
            PermissionIds.Api.Users.Read.WithUserId("user-child").Allow()
        );

        // Child scope does not grant parent access
        // Using strongly-typed Permission API - _read scope doesn't have Permission since it's not a leaf
        var hasApiRead = service.HasPermission(principal, "api:_read;userId=user-child");

        Assert.False(hasApiRead); // Child doesn't grant parent
    }

    #endregion

    #region RBAC Version Backward Compatibility Tests

    [Fact]
    public void LegacyTokenWithoutRbacVersion_GrantsAdminAccess()
    {
        // Create a principal without rbac_version claim (legacy token)
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "legacy-user"),
            new Claim("name", "legacy@example.com"),
            new Claim("scope", "api:users:_read") // Old format
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);

        var service = CreatePermissionService();

        // Legacy tokens (no rbac_version) should get full admin access
        var hasPermission = service.HasPermission(principal, "api:users:read");

        Assert.True(hasPermission);
    }

    [Fact]
    public void LegacyTokenWithRbacVersion1_GrantsAdminAccess()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "v1-user"),
            new Claim("name", "v1@example.com"),
            new Claim("rbac_version", "1"),
            new Claim("scope", "api:users:_read") // Old format
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);

        var service = CreatePermissionService();

        // rbac_version="1" tokens should get full admin access
        var hasPermission = service.HasPermission(principal, "api:users:read");

        Assert.True(hasPermission);
    }

    [Fact]
    public void NewTokenWithRbacVersion2_RequiresExplicitScopes()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "v2-user"),
            new Claim("name", "v2@example.com"),
            new Claim("rbac_version", "2"),
            new Claim("scope", PermissionIds.Api.Users.Read.WithUserId("v2-user").Allow())
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);

        var service = CreatePermissionService();

        // With rbac_version="2", only explicit scopes are granted
        // Using strongly-typed Permission API
        var hasGranted = service.HasPermission(principal, PermissionIds.Api.Users.ReadPermission.Permission.WithUserId("v2-user"));
        var hasNotGranted = service.HasPermission(principal, PermissionIds.Api.Portfolio.Accounts.List.Permission.WithUserId("v2-user"));

        Assert.True(hasGranted);
        Assert.False(hasNotGranted);
    }

    #endregion

    #region Input Validation Tests

    [Fact]
    public void HasPermission_NullPrincipal_ThrowsArgumentNullException()
    {
        var service = CreatePermissionService();

        Assert.Throws<ArgumentNullException>(() => service.HasPermission(null!, "api:user:profile:read"));
    }

    [Fact]
    public void HasPermission_EmptyPermissionPath_ReturnsFalse()
    {
        var (service, principal) = CreateServiceWithPrincipal(
            "user-test",
            PermissionIds.Read.Allow()
        );

        var result = service.HasPermission(principal, "");

        Assert.False(result);
    }

    [Fact]
    public void HasPermission_WhitespacePermissionPath_ReturnsFalse()
    {
        var (service, principal) = CreateServiceWithPrincipal(
            "user-test",
            PermissionIds.Read.Allow()
        );

        var result = service.HasPermission(principal, "   ");

        Assert.False(result);
    }

    #endregion

    #region Generated PermissionIds API Tests

    [Fact]
    public void PermissionIds_All_ContainsExpectedPaths()
    {
        // Verify the generated PermissionIds.All collection exists and has content
        Assert.NotEmpty(PermissionIds.All);
        Assert.Contains("api:users:read", PermissionIds.All);
        Assert.Contains("api:users:update", PermissionIds.All);
        Assert.Contains("api:portfolio:accounts:list", PermissionIds.All);
    }

    [Fact]
    public void PermissionIds_AllParameters_ContainsExpectedParameters()
    {
        // Verify the generated AllParameters collection
        Assert.NotEmpty(PermissionIds.AllParameters);
        Assert.Contains("userId", PermissionIds.AllParameters);
    }

    [Fact]
    public void PermissionMetadata_RLeafPermissions_ContainsReadLeafs()
    {
        // Read leaf permissions should include read-category leaf nodes
        Assert.NotEmpty(PermissionMetadata.RLeafPermissions);
        Assert.Contains("api:users:read", PermissionMetadata.RLeafPermissions);
    }

    [Fact]
    public void PermissionMetadata_WLeafPermissions_ContainsWriteLeafs()
    {
        // Write leaf permissions should include write-category leaf nodes
        Assert.NotEmpty(PermissionMetadata.WLeafPermissions);
        Assert.Contains("api:users:update", PermissionMetadata.WLeafPermissions);
    }

    #endregion

    #region Helper Methods

    private static IPermissionService CreatePermissionService(JwtConfiguration? jwtConfig = null)
    {
        jwtConfig ??= new JwtConfiguration
        {
            Secret = "super-secret-key-value-which-is-long-enough-for-tests",
            Issuer = "https://test-issuer.example.com",
            Audience = "https://test-audience.example.com",
            DefaultExpiration = TimeSpan.FromHours(1),
            ClockSkew = TimeSpan.FromMinutes(5)
        };

        var lazyJwtFactory = new Lazy<Func<CancellationToken, Task<JwtConfiguration>>>(() => _ => Task.FromResult(jwtConfig));
        var jwtTokenService = new JwtTokenService(lazyJwtFactory);

        Func<CancellationToken, Task<IJwtTokenService>> jwtServiceFactory = _ => Task.FromResult<IJwtTokenService>(jwtTokenService);
        return new PermissionService(jwtServiceFactory);
    }

    /// <summary>
    /// Creates a ClaimsPrincipal directly with scope claims in the new directive format.
    /// This bypasses token generation validation to test HasPermission behavior.
    /// </summary>
    private static (IPermissionService Service, ClaimsPrincipal Principal) CreateServiceWithPrincipal(
        string userId,
        params string[] scopes)
    {
        var service = CreatePermissionService();

        var claimsList = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim("name", $"{userId}@example.com"),
            new Claim("rbac_version", "2") // Use new RBAC version to enable directive-based evaluation
        };

        foreach (var scope in scopes)
        {
            claimsList.Add(new Claim("scope", scope));
        }

        var identity = new ClaimsIdentity(claimsList, "Test");
        var principal = new ClaimsPrincipal(identity);

        return (service, principal);
    }

    #endregion
}
