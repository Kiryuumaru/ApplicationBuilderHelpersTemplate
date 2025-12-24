using Domain.Authorization.Constants;
using Domain.Authorization.Enums;
using Domain.Authorization.Models;
using Domain.Authorization.Services;
using Domain.Authorization.ValueObjects;

namespace Domain.UnitTests.Authorization;

/// <summary>
/// Tests for ScopeEvaluator to verify new directive-based scope evaluation works correctly.
/// </summary>
public class ScopeEvaluatorTests
{
    [Fact]
    public void HasPermission_GlobalReadScope_GrantsRLeafPermission()
    {
        var scope = new[]
        {
            ScopeDirective.Allow("_read")
        };

        var result = ScopeEvaluator.HasPermission(scope, "api:user:profile:read");

        Assert.True(result);
    }

    [Fact]
    public void HasPermission_GlobalReadScope_DoesNotGrantWLeafPermission()
    {
        var scope = new[]
        {
            ScopeDirective.Allow("_read")
        };

        var result = ScopeEvaluator.HasPermission(scope, "api:user:profile:update");

        Assert.False(result);
    }

    [Fact]
    public void HasPermission_ScopedReadScope_GrantsRLeafUnderParent()
    {
        var scope = new[]
        {
            ScopeDirective.Allow("api:user:profile:_read")
        };

        var result = ScopeEvaluator.HasPermission(scope, "api:user:profile:read");

        Assert.True(result);
    }

    [Fact]
    public void HasPermission_ScopedReadScope_DoesNotGrantSibling()
    {
        var scope = new[]
        {
            ScopeDirective.Allow("api:user:profile:_read")
        };

        var result = ScopeEvaluator.HasPermission(scope, "api:user:security:activity");

        Assert.False(result);
    }

    [Fact]
    public void HasPermission_WithMatchingParameters_Grants()
    {
        var scope = new[]
        {
            ScopeDirective.Allow("api:user:profile:_read", ("userId", "user-123"))
        };
        var requestParams = new Dictionary<string, string> { ["userId"] = "user-123" };

        var result = ScopeEvaluator.HasPermission(scope, "api:user:profile:read", requestParams);

        Assert.True(result);
    }

    [Fact]
    public void HasPermission_WithNonMatchingParameters_Denies()
    {
        var scope = new[]
        {
            ScopeDirective.Allow("api:user:profile:_read", ("userId", "user-123"))
        };
        var requestParams = new Dictionary<string, string> { ["userId"] = "other-user" };

        var result = ScopeEvaluator.HasPermission(scope, "api:user:profile:read", requestParams);

        Assert.False(result);
    }

    [Fact]
    public void HasPermission_ScopeWithParamsButNoRequestParams_Denies()
    {
        var scope = new[]
        {
            ScopeDirective.Allow("api:user:profile:_read", ("userId", "user-123"))
        };

        // Request has no parameters
        var result = ScopeEvaluator.HasPermission(scope, "api:user:profile:read", null);

        Assert.False(result);
    }

    [Fact]
    public void HasPermission_ScopeWithoutParamsButRequestHasParams_Grants()
    {
        // Broad scope - no userId restriction
        var scope = new[]
        {
            ScopeDirective.Allow("api:user:profile:_read")
        };
        var requestParams = new Dictionary<string, string> { ["userId"] = "any-user" };

        var result = ScopeEvaluator.HasPermission(scope, "api:user:profile:read", requestParams);

        Assert.True(result);
    }

    [Fact]
    public void HasPermission_DenyTakesPrecedence()
    {
        var scope = new[]
        {
            ScopeDirective.Allow("_read"),
            ScopeDirective.Deny("api:user:security:activity")
        };

        var canReadProfile = ScopeEvaluator.HasPermission(scope, "api:user:profile:read");
        var canReadSecurityActivity = ScopeEvaluator.HasPermission(scope, "api:user:security:activity");

        Assert.True(canReadProfile);
        Assert.False(canReadSecurityActivity);
    }

    [Fact]
    public void HasPermission_EmptyScope_DeniesAll()
    {
        var scope = Array.Empty<ScopeDirective>();

        var result = ScopeEvaluator.HasPermission(scope, "api:user:profile:read");

        Assert.False(result);
    }

    [Fact]
    public void HasPermission_DenyOnlyMode_AllowsEverythingExceptDenied()
    {
        var scope = new[]
        {
            ScopeDirective.Deny("api:user:security:change_password")
        };

        var canChangePassword = ScopeEvaluator.HasPermission(scope, "api:user:security:change_password");
        var canReadProfile = ScopeEvaluator.HasPermission(scope, "api:user:profile:read");

        Assert.False(canChangePassword);
        Assert.True(canReadProfile);
    }

    [Fact]
    public void DirectiveParsing_AllowFormat_ParsesCorrectly()
    {
        var directive = ScopeDirective.Parse("allow;api:user:profile:_read;userId=abc123");

        Assert.Equal(ScopeDirectiveType.Allow, directive.Type);
        Assert.Equal("api:user:profile:_read", directive.PermissionPath);
        Assert.Single(directive.Parameters);
        Assert.Equal("abc123", directive.Parameters["userId"]);
    }

    [Fact]
    public void DirectiveParsing_DenyFormat_ParsesCorrectly()
    {
        var directive = ScopeDirective.Parse("deny;api:user:security:change_password");

        Assert.Equal(ScopeDirectiveType.Deny, directive.Type);
        Assert.Equal("api:user:security:change_password", directive.PermissionPath);
        Assert.Empty(directive.Parameters);
    }

    [Fact]
    public void PermissionIds_GeneratedAllowDirective_ParsesCorrectly()
    {
        // Test the generated directive format
        var directiveString = PermissionIds.Api.User.Profile.Read.WithUserId("user-test").Allow();
        var directive = ScopeDirective.Parse(directiveString);

        Assert.Equal(ScopeDirectiveType.Allow, directive.Type);
        Assert.Equal("api:user:profile:_read", directive.PermissionPath);
        Assert.Equal("user-test", directive.Parameters["userId"]);
    }

    [Fact]
    public void Integration_ParsedDirective_WorksWithEvaluator()
    {
        var directiveString = PermissionIds.Api.User.Profile.Read.WithUserId("user-123").Allow();
        var directive = ScopeDirective.Parse(directiveString);
        var scope = new[] { directive };
        var requestParams = new Dictionary<string, string> { ["userId"] = "user-123" };

        var result = ScopeEvaluator.HasPermission(scope, "api:user:profile:read", requestParams);

        Assert.True(result);
    }

    [Fact]
    public void RLeafPermissions_ContainsExpectedPaths()
    {
        var rLeafs = ScopeEvaluator.GetRLeafPermissions();

        Assert.Contains("api:user:profile:read", rLeafs);
        Assert.Contains("api:user:security:activity", rLeafs);
        Assert.Contains("api:portfolio:accounts:list", rLeafs);
    }

    [Fact]
    public void WLeafPermissions_ContainsExpectedPaths()
    {
        var wLeafs = ScopeEvaluator.GetWLeafPermissions();

        Assert.Contains("api:user:profile:update", wLeafs);
        Assert.Contains("api:user:profile:avatar", wLeafs);
        Assert.Contains("api:user:security:change_password", wLeafs);
    }

    [Fact]
    public void PermissionParameterHierarchy_ContainsInheritedParams()
    {
        // Verify that leaf permissions inherit parameters from ancestors
        var allPermissions = Permissions.GetAll();
        var profileReadPermission = allPermissions.First(p => p.Path == "api:user:profile:read");

        var paramHierarchy = profileReadPermission.GetParameterHierarchy();

        Assert.Contains("userId", paramHierarchy);
    }

    [Fact]
    public void PermissionParsing_ExtractsParametersCorrectly_SemicolonFormat()
    {
        // Test that parsing NEW semicolon format api:user:profile:read;userId=abc works correctly
        var success = Permission.TryParseIdentifier("api:user:profile:read;userId=abc", out var parsed);

        Assert.True(success);
        Assert.Equal("api:user:profile:read", parsed.Canonical);
        Assert.True(parsed.Parameters.ContainsKey("userId"));
        Assert.Equal("abc", parsed.Parameters["userId"]);
    }

    [Fact]
    public void PermissionParsing_SemicolonFormat_WithMultipleParameters()
    {
        // Test that parsing semicolon format with multiple params works
        var success = Permission.TryParseIdentifier("api:portfolio:accounts:read;userId=user-1;accountId=acc-2", out var parsed);

        Assert.True(success);
        Assert.Equal("api:portfolio:accounts:read", parsed.Canonical);
        Assert.Equal("user-1", parsed.Parameters["userId"]);
        Assert.Equal("acc-2", parsed.Parameters["accountId"]);
    }

    [Fact]
    public void PermissionParsing_StronglyTypedApi_ProducesSemicolonFormat()
    {
        // Test that the strongly-typed Permission API produces correct semicolon format
        string permissionString = PermissionIds.Api.User.Profile.ReadPermission.Permission.WithUserId("abc");
        
        Assert.Equal("api:user:profile:read;userId=abc", permissionString);
        
        var success = Permission.TryParseIdentifier(permissionString, out var parsed);
        Assert.True(success);
        Assert.Equal("api:user:profile:read", parsed.Canonical);
        Assert.Equal("abc", parsed.Parameters["userId"]);
    }

    [Fact]
    public void ScopeEvaluator_WithParametrizedRequest_MatchesParametrizedScope()
    {
        // Test that ScopeEvaluator correctly matches parametrized requests to parametrized scopes
        var scopes = new[] { ScopeDirective.Allow("api:user:profile:_read", new Dictionary<string, string> { { "userId", "abc" } }) };
        var requestParams = new Dictionary<string, string> { { "userId", "abc" } };

        var hasPermission = ScopeEvaluator.HasPermission(scopes, "api:user:profile:read", requestParams);

        Assert.True(hasPermission);
    }
}
