using Application.Authorization.Interfaces;
using Application.Authorization.Models;
using Application.Authorization.Services;
using Microsoft.IdentityModel.Tokens;
using System.Linq;
using System.Security.Claims;

namespace Application.UnitTests.Authorization;

public class PermissionServiceTests
{
    private const string AccountUpdatePermission = "api:portfolio:accounts:update;userId=user-123";
    private const string PositionClosePermission = "api:portfolio:positions:close;userId=user-123;positionId=pos-9";

    [Fact]
    public async Task GenerateTokenWithPermissionsAsync_NormalizesAndDelegatesScopes()
    {
        var jwtService = new RecordingJwtTokenService();
        var service = CreateService(jwtService);

        var permissions = new[]
        {
            "  " + AccountUpdatePermission + "  ",
            AccountUpdatePermission,
            PositionClosePermission
        };

        await service.GenerateTokenWithPermissionsAsync(
            userId: "user-123",
            username: "user@test",
            permissionIdentifiers: permissions,
            cancellationToken: CancellationToken.None);

        Assert.Equal([AccountUpdatePermission, PositionClosePermission], jwtService.LastGeneratedScopes);
        Assert.Equal("user-123", jwtService.LastGenerateTokenUserId);
        Assert.Equal("user@test", jwtService.LastGenerateTokenUsername);
    }

    [Fact]
    public void HasPermission_GrantsRootReadWhenLegacyToken()
    {
        var service = CreateService(new RecordingJwtTokenService());
        var principal = new ClaimsPrincipal(new ClaimsIdentity());

        var result = service.HasPermission(principal, PositionClosePermission);

        Assert.True(result);
    }

    [Fact]
    public async Task ValidatePermissionsAsync_AllowsAncestorScopedParameters()
    {
        var service = CreateService(new RecordingJwtTokenService());

        var result = await service.ValidatePermissionsAsync(["api:_read;userId=user-123"]);

        Assert.True(result);
    }

    [Fact]
    public async Task ValidatePermissionsAsync_RejectsUnknownParameters()
    {
        var service = CreateService(new RecordingJwtTokenService());

        var result = await service.ValidatePermissionsAsync(["api:_read;tenantId=abc"]);

        Assert.False(result);
    }

    [Fact]
    public void HasPermission_AllowsUserScopedRootGrant()
    {
        var service = CreateService(new RecordingJwtTokenService());
        // New directive format: allow;permission_path;param=value
        var identity = new ClaimsIdentity(
        [
            new Claim("scope", "allow;api:_read;userId=user-123"),
            new Claim("rbac_version", "2")
        ]);
        var principal = new ClaimsPrincipal(identity);

        // Requests carry parameters in semicolon notation
        var permitted = "api:portfolio:accounts:list;userId=user-123";
        var otherUser = "api:portfolio:accounts:list;userId=user-456";
        // Non-scoped request without userId - DENIED because scope requires userId
        var nonScoped = "api:market:assets:list";

        Assert.True(service.HasPermission(principal, permitted));
        Assert.False(service.HasPermission(principal, otherUser));
        Assert.False(service.HasPermission(principal, nonScoped)); // Scope requires userId, request has none
    }

    [Fact]
    public void HasPermission_DeniesWhenParameterMissingOnRequest()
    {
        var service = CreateService(new RecordingJwtTokenService());
        // Scope requires userId parameter
        var principal = BuildPrincipalWithScopes("allow;api:_read;userId=user-123");
        // Request has no userId parameter - the scope requires userId so this DENIES
        var permissionWithoutParameter = "api:market:assets:list";

        // The scope says "allow api:_read only for userId=user-123"
        // A request without userId doesn't satisfy this - so it's DENIED
        Assert.False(service.HasPermission(principal, permissionWithoutParameter));
    }

    [Fact]
    public void HasPermission_DeniesAccessToOtherUser()
    {
        var service = CreateService(new RecordingJwtTokenService());
        // Scope requires userId=user-123
        var principal = BuildPrincipalWithScopes("allow;api:_read;userId=user-123");

        // Attempt with userId=user-456 (different user)
        var attempt = "api:portfolio:accounts:list;userId=user-456";

        Assert.False(service.HasPermission(principal, attempt));
    }

    [Fact]
    public void HasAnyPermission_DeniesWhenOnlyOtherUserSpecified()
    {
        var service = CreateService(new RecordingJwtTokenService());
        // Scope only allows user-123
        var principal = BuildPrincipalWithScopes("allow;api:_read;userId=user-123");

        // Both requests are for user-456 (different user)
        var otherUser = "api:portfolio:accounts:list;userId=user-456";
        var otherUserPositions = "api:portfolio:positions:read;userId=user-456";

        Assert.False(service.HasAnyPermission(principal, [otherUser, otherUserPositions]));
    }

    [Fact]
    public void HasPermission_WriteScopeAllowsWriteOperations()
    {
        var service = CreateService(new RecordingJwtTokenService());
        // Use new directive format with _write scope
        var principal = BuildPrincipalWithScopes("allow;api:_write;userId=user-123");

        var result = service.HasPermission(principal, AccountUpdatePermission);

        Assert.True(result);
    }

    [Fact]
    public void HasPermission_ReadScopeDoesNotAllowWriteOperations()
    {
        var service = CreateService(new RecordingJwtTokenService());
        // Only read scope, no write
        var principal = BuildPrincipalWithScopes("allow;api:_read;userId=user-123");

        var result = service.HasPermission(principal, AccountUpdatePermission);

        Assert.False(result);
    }

    [Fact]
    public void HasAllPermissions_ReturnsFalseWhenAnyMissing()
    {
        var service = CreateService(new RecordingJwtTokenService());
        // Only allows AccountUpdatePermission path, not position close
        var identity = new ClaimsIdentity(
        [
            new Claim("scope", "allow;api:portfolio:accounts:update;userId=user-123"),
            new Claim("rbac_version", "2")
        ]);
        var principal = new ClaimsPrincipal(identity);

        var result = service.HasAllPermissions(principal, [AccountUpdatePermission, PositionClosePermission]);

        Assert.False(result);
    }

    [Fact]
    public async Task MutateTokenAsync_NormalizesPermissionsBeforeDelegation()
    {
        var jwtService = new RecordingJwtTokenService
        {
            ValidateTokenResult = BuildPrincipalWithScopes(AccountUpdatePermission, PositionClosePermission)
        };
        var service = CreateService(jwtService);

        var permissionsToAdd = new[] { "  " + PositionClosePermission + " ", PositionClosePermission };
        var permissionsToRemove = new[] { AccountUpdatePermission, "  " + AccountUpdatePermission };

        var result = await service.MutateTokenAsync(
            token: "token-value",
            permissionsToAdd: permissionsToAdd,
            permissionsToRemove: permissionsToRemove,
            claimsToAdd: [new Claim("tenant", "alpha")],
            claimsToRemove: [new Claim("tenant", "beta")],
            claimTypesToRemove: ["custom_type"],
            cancellationToken: CancellationToken.None);

        Assert.Equal("mutated-token", result);
        Assert.Null(jwtService.LastMutateScopesToAdd);
        Assert.Equal([AccountUpdatePermission], jwtService.LastMutateScopesToRemove);
        Assert.Equal("token-value", jwtService.LastMutatedToken);
    }

    [Fact]
    public async Task ValidateTokenAsync_PassesThroughUnderlyingService()
    {
        var principal = BuildPrincipalWithScopes(AccountUpdatePermission);
        var jwtService = new RecordingJwtTokenService { ValidateTokenResult = principal };
        var service = CreateService(jwtService);

        var result = await service.ValidateTokenAsync("token", CancellationToken.None);

        Assert.Same(principal, result);
    }

    private static ClaimsPrincipal BuildPrincipalWithScopes(params string[] scopes)
    {
        var scopeValue = string.Join(' ', scopes);
        var identity = new ClaimsIdentity(
        [
            new Claim("scope", scopeValue),
            new Claim("rbac_version", "2")
        ]);

        return new ClaimsPrincipal(identity);
    }

    private static ClaimsPrincipal BuildLegacyPrincipalWithScopes(params string[] scopes)
    {
        var scopeValue = string.Join(' ', scopes);
        var identity = new ClaimsIdentity(
        [
            new Claim("scope", scopeValue),
            new Claim("rbac_version", "1")
        ]);

        return new ClaimsPrincipal(identity);
    }

    private static PermissionService CreateService(RecordingJwtTokenService jwtTokenService)
        => new(cancellationToken => Task.FromResult<IJwtTokenService>(jwtTokenService));

    private sealed class RecordingJwtTokenService : IJwtTokenService
    {
        public string GenerateTokenResult { get; set; } = "token";
        public string GenerateApiKeyTokenResult { get; set; } = "api-token";
        public string MutateTokenResult { get; set; } = "mutated-token";
        public ClaimsPrincipal? ValidateTokenResult { get; set; }
        public TokenInfo? DecodeTokenResult { get; set; }

        public string? LastGenerateTokenUserId { get; private set; }
        public string? LastGenerateTokenUsername { get; private set; }
        public IReadOnlyCollection<string>? LastGeneratedScopes { get; private set; }
        public string? LastApiKeyName { get; private set; }
        public IReadOnlyCollection<string>? LastApiKeyScopes { get; private set; }
        public string? LastMutatedToken { get; private set; }
        public IReadOnlyCollection<string>? LastMutateScopesToAdd { get; private set; }
        public IReadOnlyCollection<string>? LastMutateScopesToRemove { get; private set; }

        public Task<string> GenerateToken(string userId, string username, IEnumerable<string>? scopes = null, IEnumerable<Claim>? additionalClaims = null, DateTimeOffset? expiration = null, CancellationToken cancellationToken = default)
        {
            LastGenerateTokenUserId = userId;
            LastGenerateTokenUsername = username;
            LastGeneratedScopes = scopes?.ToArray();
            return Task.FromResult(GenerateTokenResult);
        }

        public Task<string> GenerateApiKeyToken(string apiKeyName, IEnumerable<string>? scopes = null, IEnumerable<Claim>? additionalClaims = null, DateTimeOffset? expiration = null, CancellationToken cancellationToken = default)
        {
            LastApiKeyName = apiKeyName;
            LastApiKeyScopes = scopes?.ToArray();
            return Task.FromResult(GenerateApiKeyTokenResult);
        }

        public Task<ClaimsPrincipal?> ValidateToken(string token, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ValidateTokenResult);
        }

        public Task<TokenValidationParameters> GetTokenValidationParameters(CancellationToken cancellationToken = default)
            => Task.FromResult(new TokenValidationParameters());

        public Task<TokenInfo?> DecodeToken(string token, CancellationToken cancellationToken = default)
            => Task.FromResult(DecodeTokenResult);

        public Task<string> MutateToken(string token, IEnumerable<string>? scopesToAdd = null, IEnumerable<string>? scopesToRemove = null, IEnumerable<Claim>? claimsToAdd = null, IEnumerable<Claim>? claimsToRemove = null, IEnumerable<string>? claimTypesToRemove = null, DateTimeOffset? expiration = null, CancellationToken cancellationToken = default)
        {
            LastMutatedToken = token;
            LastMutateScopesToAdd = scopesToAdd?.ToArray();
            LastMutateScopesToRemove = scopesToRemove?.ToArray();
            return Task.FromResult(MutateTokenResult);
        }
    }
}
